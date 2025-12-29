using Appwrite;
using Appwrite.Services;
using Appwrite.Models;
using BasketballArchetypeCollector.Models;
using AppUser = BasketballArchetypeCollector.Models.User;

namespace BasketballArchetypeCollector.Services;

public class SessionInfo
{
    public string UserId { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
}

public class AppwriteService
{
    private readonly Client _client;
    private readonly Account _account;
    private readonly Databases _databases;
    private readonly Storage _storage;
    private readonly Functions _functions;

    // Appwrite default limit is 25 - always use 100 to get all documents
    private const int MaxQueryLimit = 100;

    public AppwriteService()
    {
        System.Diagnostics.Debug.WriteLine($"[AppwriteService] Initializing with Endpoint: {AppConfig.AppwriteEndpoint}");
        System.Diagnostics.Debug.WriteLine($"[AppwriteService] Project ID: {AppConfig.AppwriteProjectId}");

        _client = new Client()
            .SetEndpoint(AppConfig.AppwriteEndpoint)
            .SetProject(AppConfig.AppwriteProjectId);

        _account = new Account(_client);
        _databases = new Databases(_client);
        _storage = new Storage(_client);
        _functions = new Functions(_client);

        System.Diagnostics.Debug.WriteLine("[AppwriteService] Client initialized successfully");
    }

    #region Authentication

    // Session persistence keys
    private const string SessionUserIdKey = "bac_user_id";
    private const string SessionEmailKey = "bac_email";
    private const string SessionDisplayNameKey = "bac_display_name";

    public async Task<AppUser?> GetCurrentUser()
    {
        try
        {
            // First, try to get active session from Appwrite
            var session = await _account.GetSession("current");
            if (session != null)
            {
                var user = await _account.Get();

                // Persist session info locally for app restart scenarios
                await PersistUserSession(user.Id, user.Email, user.Name);

                return new AppUser
                {
                    Id = user.Id,
                    Email = user.Email,
                    DisplayName = user.Name
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetSession error: {ex.Message}");
            // Session doesn't exist or expired - user needs to log in again
            ClearCachedUser();
        }

        return null;
    }

    private async Task PersistUserSession(string userId, string email, string displayName)
    {
        try
        {
            await SecureStorage.SetAsync(SessionUserIdKey, userId);
            await SecureStorage.SetAsync(SessionEmailKey, email ?? "");
            await SecureStorage.SetAsync(SessionDisplayNameKey, displayName ?? "User");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to persist session: {ex.Message}");
        }
    }

    private void ClearCachedUser()
    {
        try
        {
            SecureStorage.Remove(SessionUserIdKey);
            SecureStorage.Remove(SessionEmailKey);
            SecureStorage.Remove(SessionDisplayNameKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear cached user: {ex.Message}");
        }
    }

    public async Task<AppUser> SignInWithEmail(string email, string password)
    {
        await _account.CreateEmailPasswordSession(email, password);
        var user = await _account.Get();

        // Persist session for app restart
        await PersistUserSession(user.Id, user.Email, user.Name);

        // Ensure user document exists
        await EnsureUserDocumentExists(user.Id, user.Name, user.Email);

        return new AppUser
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.Name
        };
    }

    public async Task<AppUser> SignUpWithEmail(string email, string password, string displayName)
    {
        var user = await _account.Create(
            userId: ID.Unique(),
            email: email,
            password: password,
            name: displayName
        );

        await _account.CreateEmailPasswordSession(email, password);

        // Create user document
        await _databases.CreateDocument(
            databaseId: AppConfig.DatabaseId,
            collectionId: AppConfig.UserCollectionsCollection,
            documentId: user.Id,
            data: new Dictionary<string, object>
            {
                { "userId", user.Id },
                { "displayName", displayName },
                { "email", email },
                { "createdAt", DateTime.UtcNow.ToString("o") }
            }
        );

        // Persist session for app restart
        await PersistUserSession(user.Id, user.Email, user.Name);

        return new AppUser
        {
            Id = user.Id,
            Email = email,
            DisplayName = displayName
        };
    }

    private async Task EnsureUserDocumentExists(string userId, string displayName, string email)
    {
        try
        {
            await _databases.GetDocument(
                databaseId: AppConfig.DatabaseId,
                collectionId: AppConfig.UserCollectionsCollection,
                documentId: userId
            );
        }
        catch
        {
            // User document doesn't exist, create it
            await _databases.CreateDocument(
                databaseId: AppConfig.DatabaseId,
                collectionId: AppConfig.UserCollectionsCollection,
                documentId: userId,
                data: new Dictionary<string, object>
                {
                    { "userId", userId },
                    { "displayName", displayName ?? "User" },
                    { "email", email ?? "" },
                    { "createdAt", DateTime.UtcNow.ToString("o") }
                }
            );
        }
    }

    public async Task SignOut()
    {
        try
        {
            await _account.DeleteSession("current");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SignOut error: {ex.Message}");
        }
        finally
        {
            // Always clear cached user on sign out
            ClearCachedUser();
        }
    }

    // Simplified auth methods for ViewModels - return error message or null on success
    public async Task<string?> SignUp(string email, string password)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] SignUp attempt for: {email}");
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] Endpoint: {AppConfig.AppwriteEndpoint}");
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] ProjectId: {AppConfig.AppwriteProjectId}");

            await SignUpWithEmail(email, password, email.Split('@')[0]);
            System.Diagnostics.Debug.WriteLine("[AppwriteService] SignUp SUCCESS");
            return null; // Success
        }
        catch (Appwrite.AppwriteException aex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] SignUp AppwriteException: {aex.Message}, Code: {aex.Code}, Type: {aex.Type}");
            return aex.Message;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] SignUp error: {ex.GetType().Name}: {ex.Message}");
            return ex.Message;
        }
    }

    public async Task<string?> Login(string email, string password)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] Login attempt for: {email}");
            await SignInWithEmail(email, password);
            System.Diagnostics.Debug.WriteLine("[AppwriteService] Login SUCCESS");
            return null; // Success
        }
        catch (Appwrite.AppwriteException aex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] Login AppwriteException: {aex.Message}, Code: {aex.Code}, Type: {aex.Type}");
            return aex.Message;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] Login error: {ex.GetType().Name}: {ex.Message}");
            return ex.Message;
        }
    }

    public async Task Logout() => await SignOut();

    public async Task<SessionInfo?> GetCurrentSession()
    {
        var user = await GetCurrentUser();
        if (user == null) return null;

        return new SessionInfo
        {
            UserId = user.Id,
            UserEmail = user.Email
        };
    }

    #endregion

    #region Game State (Collection & Coins)

    public async Task<GameState?> GetUserGameState(string userId)
    {
        try
        {
            var doc = await _databases.GetDocument(
                databaseId: AppConfig.DatabaseId,
                collectionId: AppConfig.UserCollectionsCollection,
                documentId: userId
            );

            return new GameState
            {
                Coins = doc.Data.ContainsKey("coins") && int.TryParse(doc.Data["coins"]?.ToString(), out var coins) ? coins : AppConfig.StartingCoins,
                Collection = ParseStringList(doc.Data.GetValueOrDefault("playerIds")),
                Stats = ParseGameStats(doc.Data)
            };
        }
        catch
        {
            // No game state exists yet
            return null;
        }
    }

    public async Task SaveUserGameState(string userId, GameState state)
    {
        var data = new Dictionary<string, object>
        {
            { "userId", userId },
            { "coins", state.Coins },
            { "playerIds", state.Collection },
            { "packsOpened", state.Stats.PacksOpened },
            { "cardsCollected", state.Stats.CardsCollected },
            { "goatCount", state.Stats.GoatCount },
            { "legendaryCount", state.Stats.LegendaryCount },
            { "epicCount", state.Stats.EpicCount },
            { "rareCount", state.Stats.RareCount },
            { "updatedAt", DateTime.UtcNow.ToString("o") }
        };

        try
        {
            // Try to update existing document
            await _databases.UpdateDocument(
                databaseId: AppConfig.DatabaseId,
                collectionId: AppConfig.UserCollectionsCollection,
                documentId: userId,
                data: data
            );
        }
        catch
        {
            // Document doesn't exist, create it
            await _databases.CreateDocument(
                databaseId: AppConfig.DatabaseId,
                collectionId: AppConfig.UserCollectionsCollection,
                documentId: userId,
                data: data
            );
        }
    }

    private List<string> ParseStringList(object? value)
    {
        if (value == null) return new List<string>();

        if (value is List<object> list)
        {
            return list.Select(x => x?.ToString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        if (value is string str)
        {
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(str);
                return parsed ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        return new List<string>();
    }

    private GameStats ParseGameStats(IDictionary<string, object?> data)
    {
        return new GameStats
        {
            PacksOpened = data.ContainsKey("packsOpened") && int.TryParse(data["packsOpened"]?.ToString(), out var po) ? po : 0,
            CardsCollected = data.ContainsKey("cardsCollected") && int.TryParse(data["cardsCollected"]?.ToString(), out var cc) ? cc : 0,
            GoatCount = data.ContainsKey("goatCount") && int.TryParse(data["goatCount"]?.ToString(), out var gc) ? gc : 0,
            LegendaryCount = data.ContainsKey("legendaryCount") && int.TryParse(data["legendaryCount"]?.ToString(), out var lc) ? lc : 0,
            EpicCount = data.ContainsKey("epicCount") && int.TryParse(data["epicCount"]?.ToString(), out var ec) ? ec : 0,
            RareCount = data.ContainsKey("rareCount") && int.TryParse(data["rareCount"]?.ToString(), out var rc) ? rc : 0
        };
    }

    #endregion

    #region Archetype Cache

    public async Task<ArchetypeData?> GetCachedArchetype(string playerId)
    {
        try
        {
            // Query by playerId field
            var result = await _databases.ListDocuments(
                databaseId: AppConfig.DatabaseId,
                collectionId: AppConfig.ArchetypesCollection,
                queries: new List<string> { Query.Equal("playerId", playerId), Query.Limit(1) }
            );

            if (result.Documents.Count > 0)
            {
                return MapArchetypeFromDocument(result.Documents[0]);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCachedArchetype error: {ex.Message}");
        }

        return null;
    }

    public async Task SaveArchetypeToCache(ArchetypeData archetype)
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                { "playerId", archetype.PlayerId },
                { "playerName", archetype.PlayerName },
                { "archetype", archetype.Archetype },
                { "subArchetype", archetype.SubArchetype ?? "" },
                { "playStyleSummary", archetype.PlayStyleSummary ?? "" },
                { "crestImageUrl", archetype.CrestImageUrl ?? "" },
                { "crestImageFileId", archetype.CrestImageFileId ?? "" },
                { "confidence", archetype.Confidence ?? "" },
                { "imagePrompt", archetype.ImagePrompt ?? "" },
                { "createdAt", DateTime.UtcNow.ToString("o") }
            };

            await _databases.CreateDocument(
                databaseId: AppConfig.DatabaseId,
                collectionId: AppConfig.ArchetypesCollection,
                documentId: ID.Unique(),
                data: data
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveArchetypeToCache error: {ex.Message}");
        }
    }

    public async Task<Dictionary<string, ArchetypeData>> GetAllCachedArchetypes()
    {
        var cache = new Dictionary<string, ArchetypeData>();

        try
        {
            var docs = await FetchAllDocuments(AppConfig.ArchetypesCollection);

            foreach (var doc in docs)
            {
                var archetype = MapArchetypeFromDocument(doc);
                if (!string.IsNullOrEmpty(archetype.PlayerId))
                {
                    cache[archetype.PlayerId] = archetype;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[AppwriteService] Loaded {cache.Count} cached archetypes");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetAllCachedArchetypes error: {ex.Message}");
        }

        return cache;
    }

    private ArchetypeData MapArchetypeFromDocument(Document doc)
    {
        return new ArchetypeData
        {
            PlayerId = doc.Data.GetValueOrDefault("playerId")?.ToString() ?? "",
            PlayerName = doc.Data.GetValueOrDefault("playerName")?.ToString() ?? "",
            Archetype = doc.Data.GetValueOrDefault("archetype")?.ToString() ?? "",
            SubArchetype = doc.Data.GetValueOrDefault("subArchetype")?.ToString() ?? "",
            PlayStyleSummary = doc.Data.GetValueOrDefault("playStyleSummary")?.ToString() ?? "",
            CrestImageUrl = doc.Data.GetValueOrDefault("crestImageUrl")?.ToString(),
            CrestImageFileId = doc.Data.GetValueOrDefault("crestImageFileId")?.ToString(),
            Confidence = doc.Data.GetValueOrDefault("confidence")?.ToString() ?? "",
            ImagePrompt = doc.Data.GetValueOrDefault("imagePrompt")?.ToString() ?? "",
            CreatedAt = TryParseDateTime(doc.Data.GetValueOrDefault("createdAt")?.ToString())
        };
    }

    #endregion

    #region Archetype Generation (Appwrite Function - like SwishPot)

    public async Task<ArchetypeData?> GenerateArchetype(string playerId, string playerName, string statHints)
    {
        System.Diagnostics.Debug.WriteLine($"[AppwriteService] GenerateArchetype called for: {playerName} (ID: {playerId})");

        try
        {
            // Create a fresh Functions instance like SwishPot does
            var functions = new Appwrite.Services.Functions(_client);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                playerId,
                playerName,
                statHints
            });

            System.Diagnostics.Debug.WriteLine($"[AppwriteService] Calling function '{AppConfig.GenerateArchetypeFunctionId}' with payload: {payload}");

            var execution = await functions.CreateExecution(
                functionId: AppConfig.GenerateArchetypeFunctionId,
                body: payload,
                xasync: false,
                method: Appwrite.Enums.ExecutionMethod.POST
            );

            System.Diagnostics.Debug.WriteLine($"[AppwriteService] Function execution status: {execution.Status}");
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] Function response length: {execution.ResponseBody?.Length ?? 0}");

            if (!string.IsNullOrEmpty(execution.ResponseBody))
            {
                System.Diagnostics.Debug.WriteLine($"[AppwriteService] ResponseBody: {execution.ResponseBody.Substring(0, Math.Min(500, execution.ResponseBody.Length))}");
            }

            if (string.IsNullOrEmpty(execution.ResponseBody))
            {
                System.Diagnostics.Debug.WriteLine("[AppwriteService] Empty response from generate-archetype function");
                return null;
            }

            var response = System.Text.Json.JsonDocument.Parse(execution.ResponseBody);

            if (response.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                if (response.RootElement.TryGetProperty("data", out var data))
                {
                    var archetype = new ArchetypeData
                    {
                        PlayerId = playerId,
                        PlayerName = playerName,
                        Archetype = data.TryGetProperty("archetype", out var arch) ? arch.GetString() ?? "" : "",
                        SubArchetype = data.TryGetProperty("subArchetype", out var sub) ? sub.GetString() ?? "" : "",
                        PlayStyleSummary = data.TryGetProperty("playStyleSummary", out var summary) ? summary.GetString() ?? "" : "",
                        CrestImageUrl = data.TryGetProperty("crestImageUrl", out var url) ? url.GetString() : null,
                        Confidence = data.TryGetProperty("confidence", out var conf) ? conf.GetString() ?? "" : "",
                        CreatedAt = DateTime.UtcNow
                    };

                    System.Diagnostics.Debug.WriteLine($"[AppwriteService] SUCCESS: Archetype={archetype.Archetype}, CrestUrl={archetype.CrestImageUrl ?? "null"}");
                    return archetype;
                }
            }

            // Check for cached response
            if (response.RootElement.TryGetProperty("cached", out var cached) && cached.GetBoolean())
            {
                if (response.RootElement.TryGetProperty("data", out var cachedData))
                {
                    var archetype = new ArchetypeData
                    {
                        PlayerId = playerId,
                        PlayerName = playerName,
                        Archetype = cachedData.TryGetProperty("archetype", out var arch) ? arch.GetString() ?? "" : "",
                        SubArchetype = cachedData.TryGetProperty("subArchetype", out var sub) ? sub.GetString() ?? "" : "",
                        PlayStyleSummary = cachedData.TryGetProperty("playStyleSummary", out var summary) ? summary.GetString() ?? "" : "",
                        CrestImageUrl = cachedData.TryGetProperty("crestImageUrl", out var url) ? url.GetString() : null,
                        Confidence = cachedData.TryGetProperty("confidence", out var conf) ? conf.GetString() ?? "" : "",
                        CreatedAt = DateTime.UtcNow
                    };

                    System.Diagnostics.Debug.WriteLine($"[AppwriteService] CACHED: Archetype={archetype.Archetype}, CrestUrl={archetype.CrestImageUrl ?? "null"}");
                    return archetype;
                }
            }

            var errorMsg = response.RootElement.TryGetProperty("error", out var err) ? err.GetString() : "Unknown error";
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] generate-archetype error: {errorMsg}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] GenerateArchetype EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    // Overload that takes a Player object
    public async Task<ArchetypeData?> GenerateArchetype(Player player)
    {
        var statHints = $"PPG: {player.Ppg}, RPG: {player.Rpg}, APG: {player.Apg}, Games: {player.Games}, Position: {player.Position}";
        return await GenerateArchetype(player.Id, player.FullName, statHints);
    }

    #endregion

    #region Crest Image Storage

    public async Task<string?> UploadCrestImage(string playerId, Stream imageStream, string fileName)
    {
        try
        {
            if (imageStream == null || imageStream.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[AppwriteService] UploadCrestImage: Stream is null or empty");
                return null;
            }

            // Reset stream position if seekable
            if (imageStream.CanSeek && imageStream.Position > 0)
            {
                imageStream.Position = 0;
            }

            // Check file size (5MB max)
            if (imageStream.Length > AppConfig.MaxCrestImageSizeMb * 1024 * 1024)
            {
                System.Diagnostics.Debug.WriteLine("[AppwriteService] UploadCrestImage: File too large");
                return null;
            }

            var fileId = ID.Unique();
            var safeFileName = $"crest_{playerId}_{DateTime.UtcNow.Ticks}.png";

            var file = await _storage.CreateFile(
                bucketId: AppConfig.CrestsBucketId,
                fileId: fileId,
                file: InputFile.FromStream(imageStream, safeFileName, "image/png")
            );

            // Construct the file URL
            var url = $"{AppConfig.AppwriteEndpoint}/storage/buckets/{AppConfig.CrestsBucketId}/files/{file.Id}/view?project={AppConfig.AppwriteProjectId}";

            System.Diagnostics.Debug.WriteLine($"[AppwriteService] Uploaded crest image: {url}");
            return url;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppwriteService] UploadCrestImage error: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Fetch All Documents (Bypass 25 limit)

    /// <summary>
    /// Fetches ALL documents from a collection using the fetch-documents Appwrite function.
    /// This bypasses the 25 document limit by using server-side pagination.
    /// </summary>
    private async Task<List<Document>> FetchAllDocuments(string collectionName)
    {
        try
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new { collection = collectionName });

            var execution = await _functions.CreateExecution(
                functionId: AppConfig.FetchDocumentsFunctionId,
                body: payload,
                xasync: false,
                method: Appwrite.Enums.ExecutionMethod.POST
            );

            if (string.IsNullOrEmpty(execution.ResponseBody))
            {
                System.Diagnostics.Debug.WriteLine($"[FetchAllDocuments] Empty response for {collectionName}");
                throw new Exception($"Empty response from fetch-documents for {collectionName}");
            }

            var response = System.Text.Json.JsonDocument.Parse(execution.ResponseBody);

            // Check for error
            if (response.RootElement.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
                var errorMsg = response.RootElement.TryGetProperty("error", out var err) ? err.GetString() : "Unknown error";
                throw new Exception($"fetch-documents error for {collectionName}: {errorMsg}");
            }

            var documents = new List<Document>();
            if (response.RootElement.TryGetProperty("documents", out var docsElement))
            {
                foreach (var doc in docsElement.EnumerateArray())
                {
                    var docData = new Dictionary<string, object>();
                    foreach (var prop in doc.EnumerateObject())
                    {
                        if (prop.Name.StartsWith("$")) continue;

                        // Handle different JSON value types properly
                        object value;
                        switch (prop.Value.ValueKind)
                        {
                            case System.Text.Json.JsonValueKind.String:
                                value = prop.Value.GetString() ?? "";
                                break;
                            case System.Text.Json.JsonValueKind.Number:
                                if (prop.Value.TryGetInt64(out var longVal))
                                    value = longVal.ToString();
                                else
                                    value = prop.Value.GetDouble().ToString();
                                break;
                            case System.Text.Json.JsonValueKind.True:
                                value = "true";
                                break;
                            case System.Text.Json.JsonValueKind.False:
                                value = "false";
                                break;
                            case System.Text.Json.JsonValueKind.Null:
                                value = "";
                                break;
                            default:
                                value = prop.Value.GetRawText();
                                break;
                        }
                        docData[prop.Name] = value;
                    }
                    documents.Add(new Document(
                        doc.GetProperty("$id").GetString()!,
                        doc.TryGetProperty("$collectionId", out var colId) ? colId.GetString()! : collectionName,
                        doc.TryGetProperty("$databaseId", out var dbId) ? dbId.GetString()! : AppConfig.DatabaseId,
                        doc.TryGetProperty("$createdAt", out var created) ? created.GetString()! : DateTime.UtcNow.ToString("o"),
                        doc.TryGetProperty("$updatedAt", out var updated) ? updated.GetString()! : DateTime.UtcNow.ToString("o"),
                        new List<string>(),
                        docData
                    ));
                }
            }

            System.Diagnostics.Debug.WriteLine($"[FetchAllDocuments] Fetched {documents.Count} from {collectionName}");
            return documents;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FetchAllDocuments] Error for {collectionName}: {ex.Message}");
            // Fallback to direct query (will only get 25 docs but better than nothing)
            System.Diagnostics.Debug.WriteLine($"[FetchAllDocuments] Falling back to ListDocuments for {collectionName}");
            var fallback = await _databases.ListDocuments(
                databaseId: AppConfig.DatabaseId,
                collectionId: collectionName
            );
            System.Diagnostics.Debug.WriteLine($"[FetchAllDocuments] Fallback got {fallback.Documents.Count}/{fallback.Total} from {collectionName}");
            return fallback.Documents.ToList();
        }
    }

    #endregion

    #region Helpers

    private static DateTime TryParseDateTime(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return DateTime.UtcNow;

        if (DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt;

        return DateTime.UtcNow;
    }

    #endregion
}
