using BasketballArchetypeCollector.Models;
using BasketballArchetypeCollector.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballArchetypeCollector.ViewModels;

public partial class PlayerDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly GameStateService _gameStateService;
    private readonly PlayerDataService _playerDataService;
    private readonly AppwriteService _appwriteService;

    [ObservableProperty]
    private Player? _player;

    [ObservableProperty]
    private bool _isOwned;

    [ObservableProperty]
    private int _coins;

    [ObservableProperty]
    private ArchetypeData? _archetype;

    [ObservableProperty]
    private bool _hasArchetype;

    [ObservableProperty]
    private string _archetypeName = string.Empty;

    [ObservableProperty]
    private string _archetypeDescription = string.Empty;

    [ObservableProperty]
    private string? _crestImageUrl;

    [ObservableProperty]
    private string _debugInfo = string.Empty;

    [ObservableProperty]
    private bool _isRetryingCrest;

    // Show retry button only when: owned AND no crest AND not currently retrying
    public bool CanRetryCrest => IsOwned && !HasArchetype && !IsRetryingCrest;

    public PlayerDetailViewModel(
        GameStateService gameStateService,
        PlayerDataService playerDataService,
        AppwriteService appwriteService)
    {
        _gameStateService = gameStateService;
        _playerDataService = playerDataService;
        _appwriteService = appwriteService;

        _gameStateService.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (Player != null)
        {
            IsOwned = _gameStateService.OwnsCard(Player.Id);
            Coins = _gameStateService.CurrentState.Coins;
            OnPropertyChanged(nameof(CanRetryCrest));
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("playerId", out var playerIdObj) && playerIdObj is string playerId)
        {
            LoadPlayer(playerId);
        }
    }

    private async void LoadPlayer(string playerId)
    {
        try
        {
            // Ensure player data is loaded first (critical for Collection and Pack views)
            await _playerDataService.LoadPlayersAsync();

            Player = _playerDataService.GetPlayerById(playerId);
            if (Player != null)
            {
                Title = Player.FullName;
                IsOwned = _gameStateService.OwnsCard(Player.Id);
                Coins = _gameStateService.CurrentState.Coins;

                // Notify UI about rarity-dependent properties
                OnPropertyChanged(nameof(RarityColor));
                OnPropertyChanged(nameof(RarityTextColor));
                OnPropertyChanged(nameof(RarityBackgroundColor));
                OnPropertyChanged(nameof(EraColor));

                // Check for cached archetype locally first
                var cacheCount = _gameStateService.ArchetypeCache.Count;
                var cacheKeys = string.Join(",", _gameStateService.ArchetypeCache.Keys.TakeLast(5));
                DebugInfo = $"ID: {Player.Id} | Cache: {cacheCount} | Last5: {cacheKeys}";

                var cached = _gameStateService.GetCachedArchetype(Player.Id);
                if (cached != null)
                {
                    DebugInfo = $"✓ Local | {cached.ArchetypeName}";
                    SetArchetype(cached);
                    return;
                }

                DebugInfo = $"✗ ID: {Player.Id} NOT in cache | Keys: {cacheKeys}";

                // Try to fetch from Appwrite if not cached locally
                try
                {
                    DebugInfo = $"ID: {Player.Id} | Checking Appwrite...";
                    var cloudArchetype = await _appwriteService.GetCachedArchetype(Player.Id);
                    if (cloudArchetype != null)
                    {
                        DebugInfo = $"✓ Appwrite | {cloudArchetype.ArchetypeName}";
                        SetArchetype(cloudArchetype);
                        // Cache it locally for next time
                        await _gameStateService.CacheArchetype(cloudArchetype);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    DebugInfo = $"✗ Appwrite error: {ex.Message}";
                }

                // No archetype found - show retry button if owned
                DebugInfo = $"✗ No archetype | ID: {Player.Id} | Cache: {cacheCount} | Owned: {IsOwned}";
                OnPropertyChanged(nameof(CanRetryCrest));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerDetailViewModel] Player not found: {playerId}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerDetailViewModel] LoadPlayer error: {ex.Message}");
        }
    }

    private void SetArchetype(ArchetypeData archetype)
    {
        Archetype = archetype;
        HasArchetype = true;
        ArchetypeName = archetype.ArchetypeName;
        ArchetypeDescription = archetype.Description;
        CrestImageUrl = archetype.CrestImageUrl;
        OnPropertyChanged(nameof(CanRetryCrest)); // Hide retry button when crest is set
    }

    [RelayCommand]
    private async Task RetryGenerateCrestAsync()
    {
        if (Player == null || !IsOwned || HasArchetype || IsRetryingCrest) return;

        IsRetryingCrest = true;
        OnPropertyChanged(nameof(CanRetryCrest));

        try
        {
            DebugInfo = "Generating crest...";

            var archetype = await _appwriteService.GenerateArchetype(Player);

            if (archetype != null && archetype.HasCrestImage)
            {
                await _gameStateService.CacheArchetype(archetype);
                SetArchetype(archetype);
                DebugInfo = $"✓ Generated | {archetype.ArchetypeName}";
            }
            else
            {
                // Generation returned but no crest image - try fetching from DB
                await Task.Delay(500);
                var cloudArchetype = await _appwriteService.GetCachedArchetype(Player.Id);
                if (cloudArchetype != null && cloudArchetype.HasCrestImage)
                {
                    await _gameStateService.CacheArchetype(cloudArchetype);
                    SetArchetype(cloudArchetype);
                    DebugInfo = $"✓ Fetched | {cloudArchetype.ArchetypeName}";
                }
                else
                {
                    DebugInfo = "✗ Crest generation failed. Try again later.";
                    await Shell.Current.DisplayAlert("Generation Failed",
                        "Could not generate crest. Please try again later.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerDetail] Retry crest error: {ex.Message}");
            DebugInfo = $"✗ Error: {ex.Message}";
            await Shell.Current.DisplayAlert("Error",
                "Failed to generate crest. Please try again.", "OK");
        }
        finally
        {
            IsRetryingCrest = false;
            OnPropertyChanged(nameof(CanRetryCrest));
        }
    }

    [RelayCommand]
    private async Task SellCardAsync()
    {
        if (Player == null || !IsOwned) return;

        var sellValue = RarityConfig.GetSellValue(Player.Rarity);
        var confirm = await Shell.Current.DisplayAlert(
            "Sell Card",
            $"Sell {Player.FullName} for {sellValue} coins?",
            "Sell", "Cancel");

        if (confirm)
        {
            try
            {
                await _gameStateService.SellCard(Player.Id);
                IsOwned = false;
                Coins = _gameStateService.CurrentState.Coins;
                OnPropertyChanged(nameof(CanRetryCrest)); // Update retry button visibility
                await Shell.Current.DisplayAlert("Sold!", $"You received {sellValue} coins.", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    public string RarityColor
    {
        get
        {
            var rarity = Player?.Rarity ?? Rarity.Common;
            return RarityConfig.Info.TryGetValue(rarity, out var info) ? info.Color : "#6b7280";
        }
    }

    public string RarityTextColor
    {
        get
        {
            var rarity = Player?.Rarity ?? Rarity.Common;
            return RarityConfig.Info.TryGetValue(rarity, out var info) ? info.TextColor : "#FFFFFF";
        }
    }

    public string RarityBackgroundColor
    {
        get
        {
            var rarity = Player?.Rarity ?? Rarity.Common;
            return rarity switch
            {
                Rarity.Goat => "#1a0a0d",      // Deep crimson/black
                Rarity.Legendary => "#1a1408", // Deep gold/black
                Rarity.Epic => "#140f1f",      // Deep purple/black
                Rarity.Rare => "#0a1220",      // Deep blue/black
                Rarity.Uncommon => "#0a1510",  // Deep green/black
                Rarity.Common => "#0f172a",    // Default dark blue
                _ => "#0f172a"
            };
        }
    }

    public string EraColor => EraConfig.GetColor(Player?.Era ?? Era.Modern);
}
