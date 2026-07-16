using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Velocity.Mcp.Core;

/// <summary>
/// The World Cup reference dataset. Register as a singleton.
/// </summary>
public sealed class WorldCupDb : IDisposable
{
    // ponytail: in-memory, seeded from worldcup.sql on construction. The dataset is static,
    // read-only and ~23 rows, so there is nothing to persist and no migration story to own.
    // Move to a file-backed Data Source if the data ever outgrows seed-on-boot or gains writes.
    private readonly SqliteConnection _connection;

    public WorldCupDb()
    {
        _connection = new SqliteConnection("Data Source=worldcup;Mode=Memory;Cache=Shared");
        _connection.Open();

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Velocity.Mcp.Core.worldcup.sql")
            ?? throw new InvalidOperationException("Embedded worldcup.sql is missing from the assembly.");
        using var reader = new StreamReader(stream);

        using var command = _connection.CreateCommand();
        command.CommandText = reader.ReadToEnd();
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<Tournament> Query(int? year, string? team, string? host)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT year, hosts, winner, runner_up, score, venue, city, attendance, notes
            FROM tournaments
            WHERE ($year IS NULL OR year = $year)
              AND ($team IS NULL OR winner = $team COLLATE NOCASE OR runner_up = $team COLLATE NOCASE)
              AND ($host IS NULL OR hosts LIKE '%' || $host || '%')
            ORDER BY year
            """;
        command.Parameters.AddWithValue("$year", (object?)year ?? DBNull.Value);
        command.Parameters.AddWithValue("$team", (object?)team ?? DBNull.Value);
        command.Parameters.AddWithValue("$host", (object?)host ?? DBNull.Value);

        var results = new List<Tournament>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new Tournament(
                Year: reader.GetInt32(0),
                Hosts: reader.GetString(1),
                Winner: reader.IsDBNull(2) ? null : reader.GetString(2),
                RunnerUp: reader.IsDBNull(3) ? null : reader.GetString(3),
                Score: reader.IsDBNull(4) ? null : reader.GetString(4),
                Venue: reader.IsDBNull(5) ? null : reader.GetString(5),
                City: reader.IsDBNull(6) ? null : reader.GetString(6),
                Attendance: reader.IsDBNull(7) ? null : reader.GetInt32(7),
                Notes: reader.IsDBNull(8) ? null : reader.GetString(8)));
        }
        return results;
    }

    public void Dispose() => _connection.Dispose();
}

/// <param name="Winner">Null when the final has not been played yet.</param>
public sealed record Tournament(
    int Year,
    string Hosts,
    string? Winner,
    string? RunnerUp,
    string? Score,
    string? Venue,
    string? City,
    int? Attendance,
    string? Notes);
