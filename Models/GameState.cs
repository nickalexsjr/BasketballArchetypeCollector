namespace BasketballArchetypeCollector.Models;

public class GameState
{
    public int Coins { get; set; } = 1000;
    public List<string> Collection { get; set; } = new();
    public GameStats Stats { get; set; } = new();

    // Daily rewards
    public int DailyStreak { get; set; } = 0;
    public DateTime? LastDailyClaimUtc { get; set; }

    // Mini-game cooldowns
    public DateTime? LastLuckySpinUtc { get; set; }      // 3 hour cooldown
    public DateTime? LastMysteryBoxUtc { get; set; }     // 6 hour cooldown
    public DateTime? LastCoinFlipUtc { get; set; }       // 4 hour cooldown
    public DateTime? LastTriviaUtc { get; set; }         // 2 hour cooldown
}

public class GameStats
{
    public int PacksOpened { get; set; }
    public int CardsCollected { get; set; }
    public int CrestsGenerated { get; set; }
    public int GoatCount { get; set; }
    public int LegendaryCount { get; set; }
    public int EpicCount { get; set; }
    public int RareCount { get; set; }

    public void IncrementRarityCount(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Goat:
                GoatCount++;
                break;
            case Rarity.Legendary:
                LegendaryCount++;
                break;
            case Rarity.Epic:
                EpicCount++;
                break;
            case Rarity.Rare:
                RareCount++;
                break;
        }
    }
}

public class DatabaseMeta
{
    public int TotalPlayers { get; set; }
    public int PlayersWithStats { get; set; }
    public int ActivePlayers { get; set; }
}
