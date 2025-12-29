using System.Text.Json;
using BasketballArchetypeCollector.Models;

namespace BasketballArchetypeCollector.Services;

public class GameStateService
{
    private readonly AppwriteService _appwriteService;
    private readonly PlayerDataService _playerDataService;

    private const string LocalGameStateKey = "bac_game_state";
    private const string LocalArchetypeCacheKey = "bac_archetype_cache";

    private GameState _currentState = new();
    private Dictionary<string, ArchetypeData> _archetypeCache = new();
    private string? _currentUserId;

    public GameState CurrentState => _currentState;
    public IReadOnlyDictionary<string, ArchetypeData> ArchetypeCache => _archetypeCache;
    public int CrestImageCount => _archetypeCache.Values.Count(a => a.HasCrestImage);

    public event EventHandler? StateChanged;

    public GameStateService(AppwriteService appwriteService, PlayerDataService playerDataService)
    {
        _appwriteService = appwriteService;
        _playerDataService = playerDataService;
    }

    public async Task InitializeAsync(string? userId)
    {
        _currentUserId = userId;

        // Load local state first (for fast startup)
        await LoadLocalState();

        // Load local archetype cache
        await LoadLocalArchetypeCache();

        // If user is logged in, sync with Appwrite
        if (!string.IsNullOrEmpty(userId))
        {
            await SyncWithCloud(userId);
        }
    }

    private async Task LoadLocalState()
    {
        try
        {
            var json = await SecureStorage.GetAsync(LocalGameStateKey);
            if (!string.IsNullOrEmpty(json))
            {
                _currentState = JsonSerializer.Deserialize<GameState>(json) ?? new GameState();
                System.Diagnostics.Debug.WriteLine($"[GameStateService] Loaded local state: {_currentState.Coins} coins, {_currentState.Collection.Count} cards");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameStateService] Error loading local state: {ex.Message}");
            _currentState = new GameState();
        }
    }

    private async Task SaveLocalState()
    {
        try
        {
            var json = JsonSerializer.Serialize(_currentState);
            await SecureStorage.SetAsync(LocalGameStateKey, json);
            System.Diagnostics.Debug.WriteLine($"[GameStateService] Saved local state: {_currentState.Coins} coins, {_currentState.Collection.Count} cards");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameStateService] Error saving local state: {ex.Message}");
            // Re-throw to surface the error to the caller
            throw new InvalidOperationException($"Failed to save game state: {ex.Message}", ex);
        }
    }

    private async Task LoadLocalArchetypeCache()
    {
        try
        {
            var json = await SecureStorage.GetAsync(LocalArchetypeCacheKey);
            if (!string.IsNullOrEmpty(json))
            {
                _archetypeCache = JsonSerializer.Deserialize<Dictionary<string, ArchetypeData>>(json)
                    ?? new Dictionary<string, ArchetypeData>();
                System.Diagnostics.Debug.WriteLine($"[GameStateService] Loaded {_archetypeCache.Count} cached archetypes locally");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameStateService] Error loading local archetype cache: {ex.Message}");
            _archetypeCache = new Dictionary<string, ArchetypeData>();
        }
    }

    private async Task SaveLocalArchetypeCache()
    {
        try
        {
            var json = JsonSerializer.Serialize(_archetypeCache);
            await SecureStorage.SetAsync(LocalArchetypeCacheKey, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameStateService] Error saving local archetype cache: {ex.Message}");
        }
    }

    private async Task SyncWithCloud(string userId)
    {
        try
        {
            // Get cloud state
            var cloudState = await _appwriteService.GetUserGameState(userId);
            if (cloudState != null)
            {
                // Merge: use cloud state but keep higher values
                _currentState.Coins = Math.Max(_currentState.Coins, cloudState.Coins);

                // Merge collections
                foreach (var id in cloudState.Collection)
                {
                    if (!_currentState.Collection.Contains(id))
                    {
                        _currentState.Collection.Add(id);
                    }
                }

                // Use higher stats
                _currentState.Stats.PacksOpened = Math.Max(_currentState.Stats.PacksOpened, cloudState.Stats.PacksOpened);
                _currentState.Stats.CardsCollected = Math.Max(_currentState.Stats.CardsCollected, cloudState.Stats.CardsCollected);
                _currentState.Stats.GoatCount = Math.Max(_currentState.Stats.GoatCount, cloudState.Stats.GoatCount);
                _currentState.Stats.LegendaryCount = Math.Max(_currentState.Stats.LegendaryCount, cloudState.Stats.LegendaryCount);
                _currentState.Stats.EpicCount = Math.Max(_currentState.Stats.EpicCount, cloudState.Stats.EpicCount);
                _currentState.Stats.RareCount = Math.Max(_currentState.Stats.RareCount, cloudState.Stats.RareCount);

                await SaveLocalState();
                System.Diagnostics.Debug.WriteLine("[GameStateService] Synced with cloud state");
            }

            // Load cloud archetype cache
            var cloudArchetypes = await _appwriteService.GetAllCachedArchetypes();
            foreach (var kvp in cloudArchetypes)
            {
                _archetypeCache[kvp.Key] = kvp.Value;
            }
            await SaveLocalArchetypeCache();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameStateService] Error syncing with cloud: {ex.Message}");
        }
    }

    public bool CanAfford(int cost) => _currentState.Coins >= cost;

    public bool OwnsCard(string playerId) => _currentState.Collection.Contains(playerId);

    public ArchetypeData? GetCachedArchetype(string playerId)
    {
        return _archetypeCache.TryGetValue(playerId, out var archetype) ? archetype : null;
    }

    public async Task<List<Player>> OpenPack(Pack pack)
    {
        if (!CanAfford(pack.Cost))
        {
            throw new InvalidOperationException("Not enough coins");
        }

        // Deduct coins
        _currentState.Coins -= pack.Cost;
        _currentState.Stats.PacksOpened++;

        var cards = new List<Player>();
        var random = new Random();

        for (int i = 0; i < pack.Cards; i++)
        {
            var player = GetRandomPlayer(pack, random);
            cards.Add(player);

            if (!_currentState.Collection.Contains(player.Id))
            {
                // New card
                _currentState.Collection.Add(player.Id);
                _currentState.Stats.CardsCollected++;
                _currentState.Stats.IncrementRarityCount(player.Rarity);
            }
            else
            {
                // Duplicate - give half coin value
                var sellValue = RarityConfig.GetSellValue(player.Rarity);
                _currentState.Coins += sellValue;
            }
        }

        await SaveAndSync();
        return cards;
    }

    private Player GetRandomPlayer(Pack pack, Random random)
    {
        var targetRarity = RollRarity(pack, random);

        // Get all players of this rarity
        var playersOfRarity = _playerDataService.GetPlayersByRarity(targetRarity);

        if (playersOfRarity.Count == 0)
        {
            // Fallback to common if no players of target rarity
            playersOfRarity = _playerDataService.GetPlayersByRarity(Rarity.Common);
        }

        // Final safety check
        if (playersOfRarity.Count == 0)
        {
            throw new InvalidOperationException("No players available. Please ensure player data is loaded.");
        }

        return playersOfRarity[random.Next(playersOfRarity.Count)];
    }

    private Rarity RollRarity(Pack pack, Random random)
    {
        var rand = random.NextDouble() * 100;
        var cumulative = 0f;

        var rarityOrder = new[] { Rarity.Goat, Rarity.Legendary, Rarity.Epic, Rarity.Rare, Rarity.Uncommon, Rarity.Common };

        foreach (var rarity in rarityOrder)
        {
            var rarityInfo = RarityConfig.GetInfo(rarity);
            var baseChance = rarityInfo.Chance;
            var boost = pack.Boosts.TryGetValue(rarity, out var b) ? b : 1f;

            // GOAT inherits legendary boost * 0.5 if no specific goat boost
            if (rarity == Rarity.Goat && boost == 1f && pack.Boosts.TryGetValue(Rarity.Legendary, out var legBoost))
            {
                boost = legBoost * 0.5f;
            }

            cumulative += baseChance * boost;

            if (rand <= cumulative)
            {
                // Check guaranteed minimum
                if (pack.Guaranteed.HasValue)
                {
                    var guaranteedIndex = Array.IndexOf(rarityOrder, pack.Guaranteed.Value);
                    var currentIndex = Array.IndexOf(rarityOrder, rarity);
                    if (currentIndex > guaranteedIndex)
                    {
                        return pack.Guaranteed.Value;
                    }
                }
                return rarity;
            }
        }

        return pack.Guaranteed ?? Rarity.Common;
    }

    public async Task<int> SellCard(string playerId)
    {
        if (!_currentState.Collection.Contains(playerId))
        {
            throw new InvalidOperationException("Player not in collection");
        }

        var player = _playerDataService.GetPlayerById(playerId);
        if (player == null)
        {
            throw new InvalidOperationException("Player not found");
        }

        var sellValue = RarityConfig.GetSellValue(player.Rarity);
        _currentState.Collection.Remove(playerId);
        _currentState.Coins += sellValue;

        await SaveAndSync();
        return sellValue;
    }

    public async Task AddCoins(int amount)
    {
        _currentState.Coins += amount;
        await SaveAndSync();
    }

    public async Task CacheArchetype(ArchetypeData archetype)
    {
        _archetypeCache[archetype.PlayerId] = archetype;
        await SaveLocalArchetypeCache();

        // Save to Appwrite if logged in
        if (!string.IsNullOrEmpty(_currentUserId))
        {
            await _appwriteService.SaveArchetypeToCache(archetype);
        }
    }

    private async Task SaveAndSync()
    {
        await SaveLocalState();
        StateChanged?.Invoke(this, EventArgs.Empty);

        // Sync to cloud if logged in
        if (!string.IsNullOrEmpty(_currentUserId))
        {
            try
            {
                await _appwriteService.SaveUserGameState(_currentUserId, _currentState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameStateService] Cloud sync error: {ex.Message}");
            }
        }
    }

    public List<Player> GetCollectionPlayers()
    {
        return _currentState.Collection
            .Select(id => _playerDataService.GetPlayerById(id))
            .Where(p => p != null)
            .Cast<Player>()
            .ToList();
    }
}
