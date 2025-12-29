namespace BasketballArchetypeCollector.Helpers;

public static class OverallCalculator
{
    // Hardcoded ratings for all-time greats
    private static readonly Dictionary<string, int> HardcodedRatings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "michael jordan", 99 },
        { "lebron james", 99 },
        { "kareem abdul-jabbar", 98 },
        { "nikola jokic", 98 },
        { "wilt chamberlain", 98 },
        { "tim duncan", 98 }
    };

    public static int Calculate(string firstName, string lastName, float ppg, float rpg, float apg,
        float spg, float bpg, float fgPct, int games, string? draftRound, string? draftNumber)
    {
        var fullName = $"{firstName} {lastName}";

        // Check hardcoded ratings first
        if (HardcodedRatings.TryGetValue(fullName, out var hardcodedRating))
        {
            return hardcodedRating;
        }

        // For players with insufficient stats, use draft position
        if (games < 10 || ppg == 0)
        {
            return CalculateFromDraftPosition(draftRound, draftNumber);
        }

        // Calculate from stats
        var ptsScore = Math.Min(100, (ppg / 25f) * 100f);
        var rebScore = Math.Min(100, (rpg / 10f) * 100f);
        var astScore = Math.Min(100, (apg / 8f) * 100f);
        var defScore = Math.Min(100, ((spg + bpg) / 2.5f) * 100f);
        var effScore = fgPct > 0 ? Math.Min(100, (fgPct / 0.50f) * 100f) : 50f;

        var longevityBonus = Math.Min(3f, (games / 1200f) * 3f);

        var rawScore = (ptsScore * 0.40f) + (rebScore * 0.15f) + (astScore * 0.18f) +
                       (defScore * 0.12f) + (effScore * 0.15f);

        var overall = 52 + (rawScore * 0.47f) + longevityBonus;

        // Clamp to range [60, 98] (99 reserved for hardcoded GOATs)
        return (int)Math.Max(60, Math.Min(98, Math.Round(overall)));
    }

    private static int CalculateFromDraftPosition(string? draftRound, string? draftNumber)
    {
        if (!int.TryParse(draftRound, out var round))
            return 62; // Undrafted/Unknown

        if (!int.TryParse(draftNumber, out var pick))
            pick = 30;

        if (round == 1)
        {
            if (pick <= 3) return 75;
            if (pick <= 10) return 72;
            return 68;
        }

        if (round == 2) return 65;

        return 62;
    }

    public static int GetSortTiebreaker(string firstName, string lastName)
    {
        var fn = (firstName ?? "").ToLowerInvariant();
        var ln = (lastName ?? "").ToLowerInvariant();

        if (fn == "michael" && ln == "jordan") return 2;
        if (fn == "lebron" && ln == "james") return 1;
        return 0;
    }
}
