namespace BasketballArchetypeCollector.Models;

public enum Era
{
    Unknown,
    Classic,
    Eighties,
    Nineties,
    TwoThousands,
    TwentyTens,
    Modern
}

public static class EraConfig
{
    public static readonly Dictionary<Era, EraInfo> Info = new()
    {
        { Era.Modern, new EraInfo("Modern", "#2ECC71") },
        { Era.TwentyTens, new EraInfo("2010s", "#3498DB") },
        { Era.TwoThousands, new EraInfo("2000s", "#9B59B6") },
        { Era.Nineties, new EraInfo("90s", "#E74C3C") },
        { Era.Eighties, new EraInfo("80s", "#F39C12") },
        { Era.Classic, new EraInfo("Classic", "#1ABC9C") },
        { Era.Unknown, new EraInfo("Unknown", "#7F8C8D") }
    };

    public static Era GetEra(string? draftYear)
    {
        if (string.IsNullOrEmpty(draftYear) || !int.TryParse(draftYear, out var year))
            return Era.Unknown;

        if (year >= 2020) return Era.Modern;
        if (year >= 2010) return Era.TwentyTens;
        if (year >= 2000) return Era.TwoThousands;
        if (year >= 1990) return Era.Nineties;
        if (year >= 1980) return Era.Eighties;
        return Era.Classic;
    }

    public static string GetColor(Era era)
    {
        return Info.TryGetValue(era, out var info) ? info.Color : "#7F8C8D";
    }

    public static string GetLabel(Era era)
    {
        return Info.TryGetValue(era, out var info) ? info.Label : "Unknown";
    }
}

public class EraInfo
{
    public string Label { get; }
    public string Color { get; }

    public EraInfo(string label, string color)
    {
        Label = label;
        Color = color;
    }
}
