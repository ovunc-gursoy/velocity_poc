using ModelContextProtocol;

internal static class Expect
{
    /// <summary>Throws when <paramref name="condition"/> is false. Unlike Debug.Assert this survives a Release build.</summary>
    public static void That(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/>, requires it to throw <see cref="McpException"/>, and returns the message.
    /// The type matters: the MCP SDK only propagates McpException messages to the caller and replaces
    /// every other exception with a generic string, so a tool that throws anything else is unusable to an agent.
    /// </summary>
    public static string Throws(Action action)
    {
        try
        {
            action();
        }
        catch (McpException ex)
        {
            return ex.Message;
        }
        catch (Exception ex)
        {
            throw new Exception($"expected an McpException so the message reaches the agent, but got {ex.GetType().Name}: {ex.Message}");
        }
        throw new Exception("expected a rejection, but the call succeeded");
    }

    /// <inheritdoc cref="Throws"/>
    public static async Task<string> ThrowsAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (McpException ex)
        {
            return ex.Message;
        }
        catch (Exception ex)
        {
            throw new Exception($"expected an McpException so the message reaches the agent, but got {ex.GetType().Name}: {ex.Message}");
        }
        throw new Exception("expected a rejection, but the call succeeded");
    }
}
