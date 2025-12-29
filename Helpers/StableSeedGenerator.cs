namespace BasketballArchetypeCollector.Helpers;

public static class StableSeedGenerator
{
    /// <summary>
    /// Generates a stable seed from text (same input always produces same output).
    /// Used for consistent archetype crest generation.
    /// </summary>
    public static string Generate(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "0";

        int hash = 0;
        foreach (char c in text)
        {
            hash = ((hash << 5) - hash) + c;
            hash &= hash; // Convert to 32-bit integer
        }

        return Math.Abs(hash).ToString("X").Substring(0, Math.Min(16, Math.Abs(hash).ToString("X").Length));
    }
}
