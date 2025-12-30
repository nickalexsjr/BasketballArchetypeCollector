using System.Collections.ObjectModel;
using BasketballArchetypeCollector.Models;
using BasketballArchetypeCollector.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballArchetypeCollector.ViewModels;

// Wrapper to hold player + crest URL for display
public partial class CardItem : ObservableObject
{
    [ObservableProperty]
    private Player _player = null!;

    [ObservableProperty]
    private string? _crestImageUrl;

    [ObservableProperty]
    private bool _isDuplicate;

    [ObservableProperty]
    private int _duplicateCoins;

    public string RarityColor => RarityConfig.GetInfo(Player.Rarity).PrimaryColor;

    public CardItem(Player player, string? crestImageUrl = null, bool isDuplicate = false, int duplicateCoins = 0)
    {
        Player = player;
        CrestImageUrl = crestImageUrl;
        IsDuplicate = isDuplicate;
        DuplicateCoins = duplicateCoins;
    }
}

public partial class PackOpeningViewModel : BaseViewModel, IQueryAttributable
{
    private readonly GameStateService _gameStateService;
    private readonly PlayerDataService _playerDataService;
    private readonly AppwriteService _appwriteService;

    [ObservableProperty]
    private Pack? _pack;

    [ObservableProperty]
    private ObservableCollection<CardItem> _cards = new();

    [ObservableProperty]
    private int _currentCardIndex;

    [ObservableProperty]
    private CardItem? _currentCard;

    [ObservableProperty]
    private bool _isOpening;

    [ObservableProperty]
    private bool _isRevealing;

    [ObservableProperty]
    private bool _allRevealed;

    [ObservableProperty]
    private int _coins;

    [ObservableProperty]
    private bool _isNewCard;

    [ObservableProperty]
    private string _loadingMessage = "Opening pack...";

    [ObservableProperty]
    private double _loadingProgress;

    [ObservableProperty]
    private double _progressBarWidth;

    [ObservableProperty]
    private int _sellAllValue;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    // Track if we've already opened a pack in this session (prevents re-opening on navigation back)
    private bool _hasOpenedPack;

    // Track the last opened pack ID to know when to reset
    private string? _lastPackId;

    public PackOpeningViewModel(GameStateService gameStateService, PlayerDataService playerDataService, AppwriteService appwriteService)
    {
        _gameStateService = gameStateService;
        _playerDataService = playerDataService;
        _appwriteService = appwriteService;
        Title = "Open Pack";

        // Subscribe to state cleared event to clear pack state when user signs out/in
        _gameStateService.StateCleared += OnStateCleared;
    }

    private void OnStateCleared(object? sender, EventArgs e)
    {
        // When game state is cleared (sign out/in), clear the pack opening state
        // This prevents opened pack cards from persisting across user sessions
        // Use MainThread to ensure UI-bound properties are updated safely
        MainThread.BeginInvokeOnMainThread(ClearPackState);
    }

    /// <summary>
    /// Clears all pack opening state. Called when user signs out or switches accounts.
    /// </summary>
    public void ClearPackState()
    {
        System.Diagnostics.Debug.WriteLine("[PackOpening] Clearing pack state due to account change");
        _lastPackId = null;
        _hasOpenedPack = false;
        Cards.Clear();
        Pack = null;
        CurrentCard = null;
        CurrentCardIndex = 0;
        AllRevealed = false;
        IsOpening = false;
        IsRevealing = false;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("packId", out var packIdObj) && packIdObj is string packId)
        {
            // Only reset if this is a DIFFERENT pack than what we had
            if (packId != _lastPackId)
            {
                System.Diagnostics.Debug.WriteLine($"[PackOpening] New pack: {packId}, resetting state");
                _lastPackId = packId;
                _hasOpenedPack = false;
                Cards.Clear();
                Pack = PackConfig.GetPackById(packId);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PackOpening] Same pack: {packId}, preserving {Cards.Count} cards");
            }
        }
    }

    [RelayCommand]
    private async Task OpenPackAsync()
    {
        // Don't open if: no pack, already opening, or already opened this pack
        if (Pack == null || IsOpening || _hasOpenedPack) return;

        _hasOpenedPack = true;
        IsOpening = true;
        Cards.Clear();
        CurrentCardIndex = 0;
        AllRevealed = false;
        LoadingProgress = 0;
        ProgressBarWidth = 0;
        LoadingMessage = "Shuffling the deck...";
        ErrorMessage = null;
        HasError = false;

        try
        {
            // Ensure players are loaded first
            await _playerDataService.LoadPlayersAsync();

            // Initial loading animation
            LoadingMessage = "Shuffling the deck...";
            LoadingProgress = 10;
            ProgressBarWidth = 25;
            await Task.Delay(300);

            LoadingMessage = "Selecting cards...";
            LoadingProgress = 20;
            ProgressBarWidth = 50;
            await Task.Delay(300);

            // Open the pack and get cards with duplicate info
            var packResults = await _gameStateService.OpenPackWithDuplicateInfo(Pack);

            LoadingMessage = "Checking rarities...";
            LoadingProgress = 30;
            ProgressBarWidth = 75;
            await Task.Delay(200);

            // Calculate sell all value (only for non-duplicates since duplicates are auto-sold)
            SellAllValue = 0;
            foreach (var result in packResults)
            {
                if (!result.IsDuplicate)
                {
                    SellAllValue += RarityConfig.GetSellValue(result.Player.Rarity);
                }
            }

            // Separate cards into cached vs needs-generation
            var cachedCards = new List<(GameStateService.PackOpenResult result, string crestUrl)>();
            var needsGeneration = new List<GameStateService.PackOpenResult>();

            foreach (var packResult in packResults)
            {
                var cached = _gameStateService.GetCachedArchetype(packResult.Player.Id);
                if (cached != null && cached.HasCrestImage)
                {
                    cachedCards.Add((packResult, cached.CrestImageUrl!));
                    System.Diagnostics.Debug.WriteLine($"[PackOpening] Using cached crest for {packResult.Player.FullName}");
                }
                else
                {
                    needsGeneration.Add(packResult);
                }
            }

            LoadingMessage = $"Found {cachedCards.Count} cached, generating {needsGeneration.Count}...";
            LoadingProgress = 40;
            ProgressBarWidth = 100;

            // Generate crests for uncached cards IN PARALLEL (v4)
            var generatedCrests = new Dictionary<string, string?>();
            if (needsGeneration.Count > 0)
            {
                LoadingMessage = $"Generating {needsGeneration.Count} crests in parallel...";
                System.Diagnostics.Debug.WriteLine($"[PackOpening] Starting parallel generation for {needsGeneration.Count} players");

                try
                {
                    var players = needsGeneration.Select(r => r.Player);
                    var results = await _appwriteService.GenerateArchetypesParallel(players);

                    foreach (var (playerId, archetype) in results)
                    {
                        if (archetype != null)
                        {
                            await _gameStateService.CacheArchetype(archetype);
                            generatedCrests[playerId] = archetype.CrestImageUrl;
                            System.Diagnostics.Debug.WriteLine($"[PackOpening] SUCCESS: {archetype.PlayerName} -> {archetype.ArchetypeName}");
                        }
                        else
                        {
                            generatedCrests[playerId] = null;
                            System.Diagnostics.Debug.WriteLine($"[PackOpening] No archetype for player {playerId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PackOpening] Parallel generation error: {ex.Message}");
                }
            }

            LoadingProgress = 80;
            ProgressBarWidth = 200;
            LoadingMessage = "Preparing cards...";

            // Add all cards to collection in original order
            foreach (var packResult in packResults)
            {
                string? crestUrl = null;

                // Check if it was cached
                var cachedItem = cachedCards.FirstOrDefault(c => c.result.Player.Id == packResult.Player.Id);
                if (cachedItem.crestUrl != null)
                {
                    crestUrl = cachedItem.crestUrl;
                }
                else if (generatedCrests.TryGetValue(packResult.Player.Id, out var generatedUrl))
                {
                    crestUrl = generatedUrl;
                }

                Cards.Add(new CardItem(packResult.Player, crestUrl, packResult.IsDuplicate, packResult.DuplicateCoins));
            }

            Coins = _gameStateService.CurrentState.Coins;

            // Final loading message
            LoadingMessage = "Cards ready!";
            LoadingProgress = 100;
            ProgressBarWidth = 250;
            await Task.Delay(300);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PackOpening] CRITICAL Error: {ex.Message}\n{ex.StackTrace}");
            HasError = true;
            ErrorMessage = $"Pack opening failed: {ex.Message}";
            await Shell.Current.DisplayAlert("Error", ErrorMessage, "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsOpening = false;
        }
    }

    [RelayCommand]
    private async Task RevealNextCard()
    {
        if (CurrentCardIndex >= Cards.Count)
        {
            AllRevealed = true;
            return;
        }

        IsRevealing = true;
        CurrentCard = Cards[CurrentCardIndex];
        IsNewCard = _gameStateService.OwnsCard(CurrentCard.Player.Id);

        // Animation delay
        await Task.Delay(500);

        IsRevealing = false;
        CurrentCardIndex++;

        if (CurrentCardIndex >= Cards.Count)
        {
            AllRevealed = true;
        }
    }

    [RelayCommand]
    private void SkipToEnd()
    {
        if (Cards.Count == 0) return;

        CurrentCardIndex = Cards.Count;
        AllRevealed = true;
        CurrentCard = Cards.LastOrDefault();
    }

    [RelayCommand]
    private async Task OpenAnother()
    {
        if (Pack == null) return;

        if (!_gameStateService.CanAfford(Pack.Cost))
        {
            await Shell.Current.DisplayAlert("Not Enough Coins",
                $"You need {Pack.Cost} coins. You have {Coins} coins.",
                "OK");
            return;
        }

        // Reset flag to allow opening another pack
        _hasOpenedPack = false;
        await OpenPackAsync();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        // Clear state when explicitly going back to pack store
        _lastPackId = null;
        _hasOpenedPack = false;
        Cards.Clear();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ViewCard(CardItem cardItem)
    {
        if (cardItem?.Player == null) return;

        // Block viewing duplicates - they're already auto-sold
        if (cardItem.IsDuplicate) return;

        // Navigate directly to player detail page
        // The cards will be preserved because we're using the same ViewModel instance
        await Shell.Current.GoToAsync($"playerdetail?playerId={cardItem.Player.Id}");
    }

    [RelayCommand]
    private async Task SellAll()
    {
        if (Cards.Count == 0) return;

        var confirm = await Shell.Current.DisplayAlert(
            "Sell All Cards",
            $"Sell all {Cards.Count} cards for {SellAllValue} coins?",
            "Sell All", "Cancel");

        if (confirm)
        {
            try
            {
                foreach (var cardItem in Cards.ToList())
                {
                    if (_gameStateService.OwnsCard(cardItem.Player.Id))
                    {
                        await _gameStateService.SellCard(cardItem.Player.Id);
                    }
                }

                Coins = _gameStateService.CurrentState.Coins;
                await Shell.Current.DisplayAlert("Sold!", $"You received {SellAllValue} coins.", "OK");

                // Clear state when leaving
                _lastPackId = null;
                _hasOpenedPack = false;
                Cards.Clear();
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}
