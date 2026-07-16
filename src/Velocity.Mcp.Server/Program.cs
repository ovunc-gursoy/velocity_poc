using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using Velocity.Mcp.Core;

var builder = WebApplication.CreateBuilder(args);

// Remote MCP is the only surface behind a network + OAuth boundary (see the architecture diagram).
// The MCP client runs the OAuth 2.1 + PKCE flow against Clerk; this host never sees credentials and
// only validates the resulting access token's signature against Clerk's JWKS.
var authority = builder.Configuration["Clerk:Authority"]
    ?? throw new InvalidOperationException(
        "Clerk:Authority is not configured. Set it to your Clerk instance issuer, e.g. " +
        "https://verb-noun-00.clerk.accounts.dev — 'dotnet user-secrets set Clerk:Authority <url>'.");

var audience = builder.Configuration["Clerk:Audience"];
var resource = builder.Configuration["Mcp:Resource"] ?? "http://localhost:5199";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        // Challenges go through the MCP scheme so a 401 carries the WWW-Authenticate header
        // pointing at our resource metadata — that document is how an MCP client discovers Clerk.
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidIssuer = authority;
        options.TokenValidationParameters.ValidateLifetime = true;

        // Audience validation is OFF, and cannot currently be turned on. Measured, not assumed:
        // a real Clerk token from the PKCE flow carries no `aud` claim at all. Its claims are
        // client_id, exp, iat, iss, jti, nbf, scope, sub. Clerk advertises no RFC 8707 resource
        // indicators, so nothing binds a token to *this* resource server.
        //
        // Consequence: any token this Clerk instance issues is accepted here, whoever it was
        // issued to. Dynamic client registration is public, so anyone can register a client and,
        // with a user's consent, call these tools as that user — the consent screen says "profile
        // and email", not "Velocity". That is a confused deputy, and it is open by design of the
        // upstream, not by our choice.
        //
        // Safe only because: single-tenant dev instance, bound to localhost, read-only tools over
        // public data. Do NOT deploy on this basis. Closing it needs one of:
        //   - Clerk gaining resource indicators / a configurable audience, then set Clerk:Audience;
        //   - disabling DCR, pre-registering one client, and allowlisting its client_id (costs
        //     compatibility with MCP clients that require DCR);
        //   - a gateway in front that enforces its own audience.
        // Setting Clerk:Audience today rejects every token, since there is no aud to match.
        options.TokenValidationParameters.ValidateAudience = audience is not null;
        if (audience is not null)
        {
            options.TokenValidationParameters.ValidAudience = audience;
        }

        // Development-only: report what the authorization server actually puts in the token, so the
        // audience decision above is made from evidence rather than assumption. Logs claim names and
        // the aud/azp values only — never the raw token, which is a live credential.
        if (builder.Environment.IsDevelopment())
        {
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var claims = context.Principal?.Claims.ToList() ?? [];
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Velocity.TokenDiagnostics");
                    logger.LogInformation(
                        "Token accepted. aud=[{Aud}] azp={Azp} iss={Iss} scopes={Scopes} allClaims=[{Names}]",
                        string.Join(", ", claims.Where(c => c.Type is "aud").Select(c => c.Value)),
                        claims.FirstOrDefault(c => c.Type is "azp")?.Value ?? "(none)",
                        claims.FirstOrDefault(c => c.Type is "iss")?.Value ?? "(none)",
                        claims.FirstOrDefault(c => c.Type is "scope" or "scp")?.Value ?? "(none)",
                        string.Join(", ", claims.Select(c => c.Type).Distinct()));
                    return Task.CompletedTask;
                }
            };
        }
    })
    .AddMcp(options =>
    {
        options.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = resource,
            AuthorizationServers = { authority },
            ScopesSupported = ["openid", "profile", "email"],
            ResourceName = "Velocity MCP",
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddVelocityCore();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<WorldCupTools>()
    .WithTools<CurrencyTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp().RequireAuthorization();

// Anonymous on purpose: a health probe that needs a token cannot report that auth is broken.
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

if (!app.Environment.IsDevelopment() && audience is null)
{
    app.Logger.LogWarning(
        "Clerk:Audience is not set, so access tokens are accepted regardless of who they were issued for. " +
        "Do not run this outside a single-tenant dev instance.");
}

app.Run();
