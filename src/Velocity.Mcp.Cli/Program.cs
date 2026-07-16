using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using Velocity.Mcp.Cli;
using Velocity.Mcp.Core;

// ponytail: hand-rolled arg parsing. Four commands do not earn a System.CommandLine dependency
// and its parser API churn. Swap it in if this grows tab completion, or more than ~6 commands.

const string Usage = """
    velocity — Velocity MCP tools from the command line

    Usage:
      velocity worldcup [--year N] [--team NAME] [--host NAME]
      velocity convert <amount> <from> <to> [--date yyyy-MM-dd]
      velocity mcp install [--config PATH] [--name NAME] [--server-path PATH]
      velocity mcp remove  [--config PATH] [--name NAME]

    Options:
      --config PATH   .mcp.json to modify. Default: ./.mcp.json
      --name NAME     Server name to register. Default: velocity
      --json          Emit raw JSON instead of a table.

    Examples:
      velocity worldcup --team Brazil
      velocity convert 100 USD EUR
      velocity convert 100 USD EUR --date 2024-01-15
      velocity mcp install
    """;

var argv = args.ToList();
if (argv.Count == 0 || argv[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Usage);
    return 0;
}

var json = Flag("--json");

try
{
    return argv[0] switch
    {
        "worldcup" => WorldCup(),
        "convert" => await Convert(),
        "mcp" => Mcp(),
        var other => Fail($"Unknown command '{other}'. Run 'velocity --help'.")
    };
}
catch (McpException ex)
{
    // The tools' own validation messages: written to be read, so print them as-is.
    return Fail(ex.Message);
}
catch (InvalidOperationException ex)
{
    return Fail(ex.Message);
}

int WorldCup()
{
    using var services = new ServiceCollection().AddVelocityCore().BuildServiceProvider();
    var results = WorldCupTools.GetTournaments(
        services.GetRequiredService<WorldCupDb>(),
        year: OptionInt("--year"),
        team: Option("--team"),
        host: Option("--host"));

    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    if (results.Count == 0)
    {
        Console.Error.WriteLine("No tournaments matched.");
        return 1;
    }

    foreach (var t in results)
    {
        Console.WriteLine($"{t.Year}  {t.Hosts,-32}  {t.Winner ?? "not yet played",-14}{(t.Score is null ? "" : $"{t.Score} v {t.RunnerUp}")}");
    }
    return 0;
}

async Task<int> Convert()
{
    if (argv.Count < 4)
    {
        return Fail("Usage: velocity convert <amount> <from> <to> [--date yyyy-MM-dd]");
    }
    if (!decimal.TryParse(argv[1], out var amount))
    {
        return Fail($"'{argv[1]}' is not a number.");
    }

    using var services = new ServiceCollection().AddVelocityCore().BuildServiceProvider();
    var result = await CurrencyTools.ConvertAsync(
        services.GetRequiredService<IHttpClientFactory>(),
        amount, argv[2], argv[3], Option("--date"));

    Console.WriteLine(json
        ? JsonSerializer.Serialize(result)
        : $"{result.Amount} {result.From} = {result.Converted} {result.To}  (rate {result.Rate}, ECB {result.RateDate:yyyy-MM-dd})");
    return 0;
}

int Mcp()
{
    var action = argv.ElementAtOrDefault(1);
    var config = Option("--config") ?? ".mcp.json";
    var name = Option("--name") ?? "velocity";

    switch (action)
    {
        case "install":
            var serverPath = Option("--server-path") ?? DefaultServerPath();
            if (!File.Exists(serverPath))
            {
                return Fail($"No local MCP server at '{serverPath}'. Build Velocity.Mcp.Local, or pass --server-path.");
            }
            var added = McpConfig.Install(config, name, Path.GetFullPath(serverPath));
            Console.WriteLine($"{(added ? "Added" : "Updated")} '{name}' in {Path.GetFullPath(config)}");
            return 0;

        case "remove":
            Console.WriteLine(McpConfig.Remove(config, name)
                ? $"Removed '{name}' from {Path.GetFullPath(config)}"
                : $"No '{name}' entry in {Path.GetFullPath(config)}; nothing to do.");
            return 0;

        default:
            return Fail("Usage: velocity mcp <install|remove> [--config PATH] [--name NAME]");
    }
}

// The stdio server sits beside this tool in both the dotnet-tool layout and a local build.
string DefaultServerPath()
{
    var exe = OperatingSystem.IsWindows() ? "velocity-mcp-local.exe" : "velocity-mcp-local";
    return Path.Combine(AppContext.BaseDirectory, exe);
}

string? Option(string flag)
{
    var i = argv.IndexOf(flag);
    return i >= 0 && i + 1 < argv.Count ? argv[i + 1] : null;
}

int? OptionInt(string flag)
{
    var raw = Option(flag);
    if (raw is null)
    {
        return null;
    }
    return int.TryParse(raw, out var value) ? value : throw new InvalidOperationException($"'{raw}' is not a whole number for {flag}.");
}

bool Flag(string flag) => argv.Remove(flag);

int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
