using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Velocity.Mcp.Server;

/// <summary>
/// Resolves the caller's email from Clerk's OIDC userinfo endpoint.
/// </summary>
/// <remarks>
/// Clerk's OAuth access tokens carry no email claim (only sub, client_id, scope, …), even when the
/// email scope is granted — verified against a real token. So per-user authorization has to resolve
/// the email out of band. userinfo takes the access token we already hold and needs no secret key.
/// Cached by subject because a single MCP session makes many requests and the email does not change
/// within a token's lifetime.
/// </remarks>
public sealed class ClerkUserInfo(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<ClerkUserInfo> logger)
{
    public const string HttpClientName = "clerk";

    public async Task<string?> GetEmailAsync(string subject, string accessToken, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<string?>(CacheKey(subject), out var cached))
        {
            return cached;
        }

        string? email = null;
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, "oauth/userinfo");
            request.Headers.Authorization = new("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (doc.RootElement.TryGetProperty("email", out var emailElement))
                {
                    email = emailElement.GetString();
                }
            }
            else
            {
                logger.LogWarning("Clerk userinfo returned {Status}; treating caller as having no verified email.", (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Fail closed for the privileged tool: an unresolved email means no full access, never
            // an open door. The World Cup tool is unaffected, so the caller degrades rather than breaks.
            logger.LogWarning(ex, "Could not resolve caller email from Clerk userinfo; treating as no verified email.");
        }

        // Cache negatives too (briefly) so a Clerk outage can't be turned into a per-request DoS on userinfo.
        cache.Set(CacheKey(subject), email, TimeSpan.FromMinutes(email is null ? 1 : 10));
        return email;
    }

    private static string CacheKey(string subject) => $"clerk-email:{subject}";
}
