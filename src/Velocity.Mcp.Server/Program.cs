using Velocity.Mcp.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddVelocityCore();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<WorldCupTools>()
    .WithTools<CurrencyTools>();

var app = builder.Build();

app.MapMcp();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
