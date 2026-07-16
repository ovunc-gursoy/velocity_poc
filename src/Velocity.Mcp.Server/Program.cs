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

        // ponytail: audience validation is off unless Clerk:Audience is configured, because Clerk
        // does not document RFC 8707 resource binding and we have not yet confirmed what it puts in
        // `aud`. This is a real gap, not a simplification: without audience binding, a token minted
        // for a DIFFERENT MCP server on the same Clerk instance is accepted here (confused deputy).
        // Tolerable only because this instance is a single-tenant dev PoC on localhost. Before any
        // deployment: inspect a real token, set Clerk:Audience, and delete this branch.
        options.TokenValidationParameters.ValidateAudience = audience is not null;
        if (audience is not null)
        {
            options.TokenValidationParameters.ValidAudience = audience;
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
