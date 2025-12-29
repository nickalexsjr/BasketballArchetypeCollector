namespace BasketballArchetypeCollector.Models;

public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Goat
}

public static class RarityConfig
{
    public static readonly Dictionary<Rarity, RarityInfo> Info = new()
    {
        { Rarity.Goat, new RarityInfo("GOAT", 0.5f, 2000, 99, "#DC143C", "#8B0000") },
        { Rarity.Legendary, new RarityInfo("LEGENDARY", 2f, 500, 94, "#FFD700", "#FF8C00") },
        { Rarity.Epic, new RarityInfo("EPIC", 8f, 200, 88, "#9B59B6", "#8E44AD") },
        { Rarity.Rare, new RarityInfo("RARE", 15f, 75, 80, "#3498DB", "#2980B9") },
        { Rarity.Uncommon, new RarityInfo("UNCOMMON", 25f, 30, 72, "#2ECC71", "#27AE60") },
        { Rarity.Common, new RarityInfo("COMMON", 50f, 10, 0, "#7F8C8D", "#95A5A6") }
    };

    public static Rarity DetermineRarity(int overall, string firstName, string lastName)
    {
        var fn = (firstName ?? "").ToLowerInvariant();
        var ln = (lastName ?? "").ToLowerInvariant();

        // GOAT rarity for MJ and LeBron only
        if ((fn == "michael" && ln == "jordan") || (fn == "lebron" && ln == "james"))
        {
            return Rarity.Goat;
        }

        if (overall >= 94) return Rarity.Legendary;
        if (overall >= 88) return Rarity.Epic;
        if (overall >= 80) return Rarity.Rare;
        if (overall >= 72) return Rarity.Uncommon;
        return Rarity.Common;
    }

    public static int GetSellValue(Rarity rarity) =>
        Info.TryGetValue(rarity, out var info) ? info.CoinValue / 2 : 5; // Fallback to 5 coins

    public static string GetLabel(Rarity rarity) =>
        Info.TryGetValue(rarity, out var info) ? info.Label : "COMMON";

    public static RarityInfo GetInfo(Rarity rarity) =>
        Info.TryGetValue(rarity, out var info) ? info : Info[Rarity.Common];
}

public class RarityInfo
{
    public string Label { get; }
    public float Chance { get; }
    public int CoinValue { get; }
    public int MinOverall { get; }
    public string PrimaryColor { get; }
    public string SecondaryColor { get; }
    public string Color => PrimaryColor; // Alias for convenience

    public RarityInfo(string label, float chance, int coinValue, int minOverall, string primaryColor, string secondaryColor)
    {
        Label = label;
        Chance = chance;
        CoinValue = coinValue;
        MinOverall = minOverall;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
    }
}
