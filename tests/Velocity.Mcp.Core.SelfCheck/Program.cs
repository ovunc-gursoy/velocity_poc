using static Expect;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using Velocity.Mcp.Cli;
using Microsoft.Extensions.DependencyInjection;
using Velocity.Mcp.Core;

// ponytail: asserts in a console app, not a test framework. One command, no fixtures, no runner.
// Promote to xunit when there are enough cases that naming and filtering them starts to matter.

var services = new ServiceCollection().AddVelocityCore().BuildServiceProvider();
var db = services.GetRequiredService<WorldCupDb>();
var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();

var failures = new List<string>();

void Check(string name, Action assertion)
{
    try
    {
        assertion();
        Console.WriteLine($"  ok   {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"  FAIL {name}\n         {ex.Message}");
    }
}

Console.WriteLine("world cup (sqlite, offline)");

Check("filters by year", () =>
{
    var final = WorldCupTools.GetTournaments(db, year: 1966).Single();
    That(final.Winner == "England", $"expected England, got {final.Winner}");
    That(final.RunnerUp == "West Germany", $"expected West Germany, got {final.RunnerUp}");
});

Check("filters by team, both winners and runners-up", () =>
{
    var brazil = WorldCupTools.GetTournaments(db, team: "brazil");
    That(brazil.Count(t => t.Winner == "Brazil") == 5, "Brazil should have 5 titles");
    That(brazil.Any(t => t.RunnerUp == "Brazil"), "Brazil should appear as a runner-up too");
});

Check("host filter matches co-hosts on substring", () =>
{
    var japan = WorldCupTools.GetTournaments(db, host: "Japan").Single();
    That(japan.Year == 2002, $"expected 2002, got {japan.Year}");
});

Check("unplayed tournament has no winner", () =>
{
    var next = WorldCupTools.GetTournaments(db, year: 2026).Single();
    That(next.Winner is null, "2026 must not report a winner");
});

Check("blank filters mean no filter, not match-empty", () =>
{
    That(WorldCupTools.GetTournaments(db, team: "  ").Count == 23, "blank team should list all 23 tournaments");
});

Check("a wartime gap year is empty, not an error", () =>
{
    That(WorldCupTools.GetTournaments(db, year: 1943).Count == 0, "1943 had no tournament, so the result should be empty");
});

Check("rejects a year outside the competition entirely, readably", () =>
{
    var error = Throws(() => WorldCupTools.GetTournaments(db, year: 5));
    That(error.Contains("1930"), $"the message should tell the agent the valid range, got: {error}");
});

Check("SKILL.md's Germany claim still matches the data", () =>
{
    // The skill tells agents: four titles total, only 2014 under "Germany", the rest under
    // "West Germany". Got this wrong once when writing it, so pin it — the skill is the agent's
    // only warning about the name split, and a silently wrong one is worse than none.
    var germany = WorldCupTools.GetTournaments(db, team: "Germany").Where(x => x.Winner == "Germany").ToList();
    var westGermany = WorldCupTools.GetTournaments(db, team: "West Germany").Where(x => x.Winner == "West Germany").ToList();
    That(germany.Count == 1 && germany[0].Year == 2014, $"expected exactly 2014 under 'Germany', got {germany.Count}");
    That(westGermany.Count == 3, $"expected 3 titles under 'West Germany', got {westGermany.Count}");
    That(germany.Count + westGermany.Count == 4, "the skill says four German titles in total");
});

Console.WriteLine("currency (live ECB rates via frankfurter.dev)");

async Task CheckAsync(string name, Func<Task> assertion)
{
    try
    {
        await assertion();
        Console.WriteLine($"  ok   {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"  FAIL {name}\n         {ex.Message}");
    }
}

await CheckAsync("converts at a historical rate", async () =>
{
    var result = await CurrencyTools.ConvertAsync(httpClientFactory, 100m, "usd", "EUR", "2024-01-15");
    That(result.From == "USD", "lowercase input should normalise to USD");
    That(result.Rate == 0.91366m, $"expected the published 2024-01-15 rate, got {result.Rate}");
    That(result.Converted == 91.37m, $"expected 91.37, got {result.Converted}");
});

await CheckAsync("weekend date falls back to the previous working day", async () =>
{
    // 2024-01-14 was a Sunday; the ECB last published on the Friday.
    var result = await CurrencyTools.ConvertAsync(httpClientFactory, 1m, "USD", "EUR", "2024-01-14");
    That(result.RateDate == new DateOnly(2024, 1, 12), $"expected a 2024-01-12 rate, got {result.RateDate}");
});

await CheckAsync("same currency short-circuits without a call", async () =>
{
    var result = await CurrencyTools.ConvertAsync(httpClientFactory, 42.5m, "GBP", "GBP");
    That(result.Rate == 1m && result.Converted == 42.5m, "same-currency conversion must be identity");
});

await CheckAsync("rejects a malformed currency code, readably", async () =>
{
    var error = await ThrowsAsync(() => CurrencyTools.ConvertAsync(httpClientFactory, 1m, "DOLLARS", "EUR"));
    That(error.Contains("ISO 4217") && error.Contains("from"), $"the message should name the bad parameter and the expected format, got: {error}");
});

await CheckAsync("rejects a non-positive amount, readably", async () =>
{
    var error = await ThrowsAsync(() => CurrencyTools.ConvertAsync(httpClientFactory, 0m, "USD", "EUR"));
    That(error.Contains("greater than zero"), $"the message should state the constraint, got: {error}");
});

await CheckAsync("reports an unknown-but-well-formed code clearly", async () =>
{
    var error = await ThrowsAsync(() => CurrencyTools.ConvertAsync(httpClientFactory, 1m, "USD", "ZZZ"));
    That(error.Contains("ZZZ"), $"the message should name the offending currency, got: {error}");
});

Console.WriteLine("cli .mcp.json merge");

var tempDir = Path.Combine(Path.GetTempPath(), "velocity-selfcheck-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
try
{
    Check("install preserves a pre-existing unrelated server", () =>
    {
        var config = Path.Combine(tempDir, ".mcp.json");
        File.WriteAllText(config, """
            {
              "mcpServers": {
                "someone-elses": { "command": "other.exe", "args": ["--keep-me"] }
              },
              "unknownTopLevelKey": { "preserve": true }
            }
            """);

        That(McpConfig.Install(config, "velocity", "C:/bin/velocity-mcp-local.exe"), "should report the entry as added");

        var root = JsonNode.Parse(File.ReadAllText(config))!;
        var servers = root["mcpServers"]!;
        That(servers["someone-elses"]?["command"]?.GetValue<string>() == "other.exe", "the unrelated server must survive");
        That(servers["someone-elses"]?["args"]?[0]?.GetValue<string>() == "--keep-me", "its args must survive");
        That(root["unknownTopLevelKey"]?["preserve"]?.GetValue<bool>() == true, "unknown top-level keys must survive");
        That(servers["velocity"]?["command"]?.GetValue<string>()!.EndsWith("velocity-mcp-local.exe") == true, "velocity should be registered");
    });

    Check("install is idempotent and reports a replacement", () =>
    {
        var config = Path.Combine(tempDir, "idempotent.json");
        That(McpConfig.Install(config, "velocity", "a.exe"), "first install should report added");
        That(!McpConfig.Install(config, "velocity", "b.exe"), "second install should report replaced, not added");
        var servers = JsonNode.Parse(File.ReadAllText(config))!["mcpServers"]!;
        That(servers["velocity"]?["command"]?.GetValue<string>() == "b.exe", "the entry should be updated in place");
        That(servers.AsObject().Count == 1, "re-installing must not duplicate the entry");
    });

    Check("remove reverses install and leaves other servers alone", () =>
    {
        var config = Path.Combine(tempDir, "remove.json");
        File.WriteAllText(config, """{ "mcpServers": { "someone-elses": { "command": "other.exe" } } }""");
        McpConfig.Install(config, "velocity", "v.exe");
        That(McpConfig.Remove(config, "velocity"), "remove should report it removed something");

        var servers = JsonNode.Parse(File.ReadAllText(config))!["mcpServers"]!.AsObject();
        That(!servers.ContainsKey("velocity"), "velocity should be gone");
        That(servers.ContainsKey("someone-elses"), "the unrelated server must remain");
        That(!McpConfig.Remove(config, "velocity"), "a second remove should report nothing to do");
    });

    Check("install creates the file when absent", () =>
    {
        var config = Path.Combine(tempDir, "nested", "new.json");
        That(McpConfig.Install(config, "velocity", "v.exe"), "should create and add");
        That(File.Exists(config), "the file should exist");
    });

    Check("refuses to touch a malformed config rather than clobber it", () =>
    {
        var config = Path.Combine(tempDir, "broken.json");
        var original = "{ this is not json";
        File.WriteAllText(config, original);
        try
        {
            McpConfig.Install(config, "velocity", "v.exe");
            throw new Exception("expected a refusal on malformed JSON");
        }
        catch (InvalidOperationException) { }
        That(File.ReadAllText(config) == original, "the malformed file must be left exactly as it was");
    });
}
finally
{
    Directory.Delete(tempDir, recursive: true);
}

Console.WriteLine();

if (failures.Count > 0)
{
    Console.WriteLine($"{failures.Count} check(s) failed.");
    return 1;
}
Console.WriteLine("All checks passed.");
return 0;
