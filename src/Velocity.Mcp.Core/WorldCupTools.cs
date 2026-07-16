using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Velocity.Mcp.Core;

[McpServerToolType]
public sealed class WorldCupTools
{
    [McpServerTool(Name = "get_world_cup_tournaments")]
    [Description("""
        Look up FIFA World Cup tournaments (1930-2026): host nation, winner, runner-up, final
        score, venue and attendance. Filters combine with AND; call with no filters to list all
        tournaments. Note that the 2026 tournament has not been played yet, so it has no winner.
        """)]
    public static IReadOnlyList<Tournament> GetTournaments(
        WorldCupDb db,
        [Description("Tournament year, e.g. 1966. World Cups are held every 4 years from 1930, excluding 1942 and 1946.")]
        int? year = null,
        [Description("Team that reached the final, as winner or runner-up, e.g. 'Brazil'. Case-insensitive, must match the full name. Use 'West Germany' for 1954-1990.")]
        string? team = null,
        [Description("Host nation, e.g. 'Mexico'. Matches on substring, so 'Japan' finds the 2002 tournament co-hosted by South Korea and Japan.")]
        string? host = null)
    {
        if (year is < 1930 or > 2026)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "The World Cup has only been held between 1930 and 2026.");
        }

        return db.Query(year, Clean(team), Clean(host));

        // Treat whitespace-only filters as absent; an agent passing "" means "no filter", not "match empty".
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
