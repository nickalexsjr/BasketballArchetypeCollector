namespace BasketballArchetypeCollector.Models;

public class Pack
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Cards { get; set; }
    public int Cost { get; set; }
    public string Description { get; set; } = string.Empty;
    public string GradientStart { get; set; } = string.Empty;
    public string GradientEnd { get; set; } = string.Empty;
    public string Color => GradientStart; // Primary color for UI
    public string Icon { get; set; } = string.Empty;
    public Rarity? Guaranteed { get; set; }
    public Dictionary<Rarity, float> Boosts { get; set; } = new();
}

public static class PackConfig
{
    public static List<Pack> AllPacks => AvailablePacks;

    public static Pack? GetPackById(string id) =>
        AvailablePacks.FirstOrDefault(p => p.Id == id);

    public static readonly List<Pack> AvailablePacks = new()
    {
        new Pack
        {
            Id = "standard",
            Name = "Standard Pack",
            Cards = 3,
            Cost = 100,
            Description = "3 random cards",
            GradientStart = "#475569",
            GradientEnd = "#1e293b",
            Icon = "pack_standard.png",
            Guaranteed = null,
            Boosts = new()
        },
        new Pack
        {
            Id = "premium",
            Name = "Premium Pack",
            Cards = 5,
            Cost = 250,
            Description = "Better odds",
            GradientStart = "#2563eb",
            GradientEnd = "#3730a3",
            Icon = "pack_premium.png",
            Guaranteed = null,
            Boosts = new()
            {
                { Rarity.Rare, 1.5f },
                { Rarity.Epic, 1.3f },
                { Rarity.Legendary, 1.2f }
            }
        },
        new Pack
        {
            Id = "elite",
            Name = "Elite Pack",
            Cards = 5,
            Cost = 500,
            Description = "Guaranteed Rare+",
            GradientStart = "#9333ea",
            GradientEnd = "#be185d",
            Icon = "pack_elite.png",
            Guaranteed = Rarity.Rare,
            Boosts = new()
            {
                { Rarity.Epic, 2f },
                { Rarity.Legendary, 1.5f }
            }
        },
        new Pack
        {
            Id = "legendary",
            Name = "Legendary Pack",
            Cards = 3,
            Cost = 1000,
            Description = "Guaranteed Epic+",
            GradientStart = "#f59e0b",
            GradientEnd = "#c2410c",
            Icon = "pack_legendary.png",
            Guaranteed = Rarity.Epic,
            Boosts = new()
            {
                { Rarity.Legendary, 3f }
            }
        }
    };
}
