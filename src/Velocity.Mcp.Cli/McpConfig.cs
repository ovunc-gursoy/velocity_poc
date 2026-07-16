using System.Text.Json;
using System.Text.Json.Nodes;

namespace Velocity.Mcp.Cli;

/// <summary>
/// Reads and writes the <c>.mcp.json</c> entry for the local stdio server.
/// </summary>
public static class McpConfig
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Adds or replaces one server entry, leaving every other entry and unknown field untouched.</summary>
    /// <returns>True if the entry was added, false if an existing entry was replaced.</returns>
    public static bool Install(string configPath, string name, string command, IEnumerable<string>? args = null)
    {
        // Parsed as a JsonNode rather than a typed model so that servers we know nothing about,
        // and any sibling keys the file happens to carry, survive the round-trip untouched.
        // A typed model would silently drop what it can't represent, which is someone else's config.
        var root = Read(configPath);

        if (root["mcpServers"] is not JsonObject servers)
        {
            servers = new JsonObject();
            root["mcpServers"] = servers;
        }

        var existed = servers.ContainsKey(name);

        var entry = new JsonObject { ["command"] = command };
        if (args is not null)
        {
            var argArray = new JsonArray();
            foreach (var arg in args)
            {
                argArray.Add(arg);
            }
            if (argArray.Count > 0)
            {
                entry["args"] = argArray;
            }
        }

        servers[name] = entry;
        Write(configPath, root);
        return !existed;
    }

    /// <summary>Removes one server entry. Other entries survive.</summary>
    /// <returns>True if an entry was removed, false if there was nothing to remove.</returns>
    public static bool Remove(string configPath, string name)
    {
        if (!File.Exists(configPath))
        {
            return false;
        }

        var root = Read(configPath);
        if (root["mcpServers"] is not JsonObject servers || !servers.Remove(name))
        {
            return false;
        }

        // Leave an empty mcpServers object rather than deleting the key or the file: the file may
        // be checked in, and a tool that deletes a user's config is a worse surprise than an empty map.
        Write(configPath, root);
        return true;
    }

    private static JsonObject Read(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return new JsonObject();
        }

        var text = File.ReadAllText(configPath);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(text) as JsonObject
                ?? throw new InvalidOperationException($"{configPath} does not contain a JSON object.");
        }
        catch (JsonException ex)
        {
            // Refuse rather than overwrite: this file is the user's, and it may hold other servers.
            throw new InvalidOperationException($"{configPath} is not valid JSON, so it will not be modified. Fix or remove it first. ({ex.Message})");
        }
    }

    private static void Write(string configPath, JsonObject root)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(configPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(configPath, root.ToJsonString(WriteOptions) + Environment.NewLine);
    }
}
