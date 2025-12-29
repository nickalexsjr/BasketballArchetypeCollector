namespace BasketballArchetypeCollector.Models;

/// <summary>
/// Represents a single pack purchase transaction.
/// Stored in the Appwrite 'pack_purchases' collection.
/// </summary>
public class PackPurchase
{
    /// <summary>Document ID from Appwrite</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User who made the purchase</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Pack type ID (standard, premium, elite, legendary)</summary>
    public string PackId { get; set; } = string.Empty;

    /// <summary>Coins spent on this purchase</summary>
    public int Cost { get; set; }

    /// <summary>When the pack was purchased (ISO 8601 format)</summary>
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Player IDs received from this pack (JSON array stored as string)</summary>
    public List<string> PlayersReceived { get; set; } = new();
}

/// <summary>
/// Aggregated pack purchase statistics for the Stats page.
/// </summary>
public class PackPurchaseStats
{
    public int StandardPacksBought { get; set; }
    public int PremiumPacksBought { get; set; }
    public int ElitePacksBought { get; set; }
    public int LegendaryPacksBought { get; set; }

    public int TotalCoinsSpent { get; set; }

    /// <summary>Most recently purchased pack type</summary>
    public string? LastPackType { get; set; }

    /// <summary>Favorite pack (most purchased)</summary>
    public string? FavoritePackType { get; set; }

    public int TotalPacks => StandardPacksBought + PremiumPacksBought + ElitePacksBought + LegendaryPacksBought;
}
