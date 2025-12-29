namespace BasketballArchetypeCollector.Models;

public class ArchetypeData
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty; // high, medium, low
    public string PlayStyleSummary { get; set; } = string.Empty;
    public string Archetype { get; set; } = string.Empty;
    public string SubArchetype { get; set; } = string.Empty;
    public string CrestSeed { get; set; } = string.Empty;
    public CrestDesign? CrestDesign { get; set; }
    public string ImagePrompt { get; set; } = string.Empty;
    public string? CrestImageUrl { get; set; }
    public string? CrestImageFileId { get; set; } // Appwrite Storage file ID
    public DateTime CreatedAt { get; set; }

    public bool HasCrestImage => !string.IsNullOrEmpty(CrestImageUrl);

    // Aliases for UI
    public string ArchetypeName => Archetype;
    public string Description => PlayStyleSummary;
}

public class CrestDesign
{
    public string CoreShape { get; set; } = string.Empty;
    public string PrimaryMotif { get; set; } = string.Empty;
    public List<string> SecondaryMotifs { get; set; } = new();
    public string PatternLanguage { get; set; } = string.Empty;
    public List<string> Materials { get; set; } = new();
    public string ColorStory { get; set; } = string.Empty;
    public string NegativeSpaceRule { get; set; } = string.Empty;
    public List<string> DoNotInclude { get; set; } = new();
}
