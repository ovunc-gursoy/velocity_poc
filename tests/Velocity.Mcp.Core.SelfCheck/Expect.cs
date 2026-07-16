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
}
