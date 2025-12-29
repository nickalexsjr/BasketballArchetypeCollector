namespace BasketballArchetypeCollector;

/// <summary>
/// App configuration template.
/// Copy this file to AppConfig.cs and fill in your API keys.
/// AppConfig.cs is gitignored - never commit real API keys!
/// </summary>
public static class AppConfig
{
    // Appwrite Configuration (Sydney region)
    public const string AppwriteEndpoint = "https://cloud.appwrite.io/v1";
    public const string AppwriteProjectId = "YOUR_APPWRITE_PROJECT_ID";

    // App Settings
    public const int StartingCoins = 1000;
    public const int MaxCrestImageSizeMb = 5;

    // Appwrite Database ID
    public const string DatabaseId = "basketball-archetypes";

    // Appwrite Collection IDs
    public const string PlayersCollection = "players";
    public const string ArchetypesCollection = "archetypes";
    public const string UserCollectionsCollection = "user_collections";
    public const string PackPurchasesCollection = "pack_purchases";

    // Appwrite Storage Bucket ID
    public const string CrestsBucketId = "crests";

    // Appwrite Function IDs
    public const string GenerateArchetypeFunctionId = "generate-archetype";
    public const string FetchDocumentsFunctionId = "fetch-documents";
}
