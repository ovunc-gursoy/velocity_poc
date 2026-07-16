using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velocity.Mcp.Core;

var builder = Host.CreateApplicationBuilder(args);

// stdout IS the protocol channel for stdio transport. Anything else written there corrupts
// the JSON-RPC stream and the client drops the connection, so logs go to stderr instead.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddVelocityCore();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WorldCupTools>()
    .WithTools<CurrencyTools>();

await builder.Build().RunAsync();
