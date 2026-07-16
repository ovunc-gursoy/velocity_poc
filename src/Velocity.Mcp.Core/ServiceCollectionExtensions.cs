using Microsoft.Extensions.DependencyInjection;

namespace Velocity.Mcp.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the Velocity tools need. Every surface calls this and nothing else,
    /// so the tools behave identically regardless of which surface invoked them.
    /// </summary>
    public static IServiceCollection AddVelocityCore(this IServiceCollection services)
    {
        services.AddSingleton<WorldCupDb>();
        services.AddHttpClient(CurrencyTools.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.frankfurter.dev/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }
}
