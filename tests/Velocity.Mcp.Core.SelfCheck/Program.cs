using static Expect;
using ModelContextProtocol;
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

Console.WriteLine();
if (failures.Count > 0)
{
    Console.WriteLine($"{failures.Count} check(s) failed.");
    return 1;
}
Console.WriteLine("All checks passed.");
return 0;
