namespace BasketballArchetypeCollector.Models;

public class Player
{
    // From CSV
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string TeamAbbreviation { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
    public string DraftYear { get; set; } = string.Empty;
    public string DraftRound { get; set; } = string.Empty;
    public string DraftNumber { get; set; } = string.Empty;
    public int Games { get; set; }
    public float Ppg { get; set; }
    public float Rpg { get; set; }
    public float Apg { get; set; }
    public float Spg { get; set; }
    public float Bpg { get; set; }
    public float FgPct { get; set; }
    public string ScrapeStatus { get; set; } = string.Empty;

    // Computed properties
    public int Overall { get; set; }
    public int? Rank { get; set; }
    public Rarity Rarity { get; set; }
    public Era Era { get; set; }
    public bool HasStats { get; set; }
    public bool IsActive { get; set; }
    public int SortTiebreaker { get; set; }

    // Display helpers
    public string FullName => $"{FirstName} {LastName}";
    public string DisplayTeam => string.IsNullOrEmpty(TeamAbbreviation) ? "FA" : TeamAbbreviation;
    public string DisplayPosition => string.IsNullOrEmpty(Position) ? "N/A" : Position;
    public string DisplayPpg => Ppg > 0 ? Ppg.ToString("F1") : "-";
    public string DisplayRpg => Rpg > 0 ? Rpg.ToString("F1") : "-";
    public string DisplayApg => Apg > 0 ? Apg.ToString("F1") : "-";
    public string DisplayGames => Games > 0 ? Games.ToString() : "-";
}
