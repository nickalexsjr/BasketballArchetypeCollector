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
    [NotifyPropertyChangedFor(nameof(ShowCardsView))]
    [NotifyPropertyChangedFor(nameof(ShowLoadingView))]
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCardsView))]
    [NotifyPropertyChangedFor(nameof(ShowLoadingView))]
    private bool _showLiveCreation;

    // Computed property to check if we have cards to show
    public bool HasCards => Cards.Count > 0;

    // Show cards view when: has cards AND (not opening OR user chose to view live)
    public bool ShowCardsView => HasCards && (!IsOpening || ShowLiveCreation);

    // Show loading view when: opening AND not viewing live creation
    public bool ShowLoadingView => IsOpening && !ShowLiveCreation;

    // Show empty state when: no pack selected and not opening and no cards
    public bool ShowEmptyState => Pack == null && !IsOpening && !HasCards;

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
        OnPropertyChanged(nameof(HasCards));
        OnPropertyChanged(nameof(ShowCardsView));
        Pack = null;
        CurrentCard = null;
        CurrentCardIndex = 0;
        AllRevealed = false;
        IsOpening = false;
        IsRevealing = false;
        ShowLiveCreation = false;
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    [RelayCommand]
    private void ViewLiveCreation()
    {
        ShowLiveCreation = true;
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
                OnPropertyChanged(nameof(HasCards));
        OnPropertyChanged(nameof(ShowCardsView));
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
        ShowLiveCreation = false;

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

            // Add all cards immediately (without crests) so user can see what they got
            foreach (var packResult in packResults)
            {
                Cards.Add(new CardItem(packResult.Player, null, packResult.IsDuplicate, packResult.DuplicateCoins));
            }
            OnPropertyChanged(nameof(HasCards));
        OnPropertyChanged(nameof(ShowCardsView));

            // Count how many need crest generation
            var needsCrestCount = 0;
            foreach (var packResult in packResults)
            {
                var cached = _gameStateService.GetCachedArchetype(packResult.Player.Id);
                if (cached == null || !cached.HasCrestImage)
                {
                    needsCrestCount++;
                }
            }

            LoadingMessage = needsCrestCount > 0
                ? "Creating crests..."
                : "Loading crests...";
            LoadingProgress = 40;
            ProgressBarWidth = 100;

            // Mark that we're generating crests (prevents sign out)
            if (needsCrestCount > 0)
            {
                _gameStateService.SetGeneratingCrests(true);
            }

            // Generate crests sequentially and update cards as they complete
            var totalCards = packResults.Count;
            for (int i = 0; i < totalCards; i++)
            {
                var packResult = packResults[i];
                var player = packResult.Player;
                var progressPercent = 40 + (int)((i + 1) / (float)totalCards * 50); // 40% to 90%

                string? crestUrl = null;

                // Check if already has cached crest
                var cached = _gameStateService.GetCachedArchetype(player.Id);
                if (cached != null && cached.HasCrestImage)
                {
                    crestUrl = cached.CrestImageUrl;
                    System.Diagnostics.Debug.WriteLine($"[PackOpening] Using cached crest for {player.FullName}");
                }
                else
                {
                    // Keep message as "Creating crests..." - don't show individual player names
                    LoadingProgress = progressPercent;
                    ProgressBarWidth = progressPercent * 2.5;

                    // Generate new crest
                    ArchetypeData? archetype = null;
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[PackOpening] Calling GenerateArchetype for {player.FullName}...");
                        archetype = await _appwriteService.GenerateArchetype(player);
                    }
                    catch (Exception crestEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PackOpening] Crest generation error for {player.FullName}: {crestEx.Message}");
                    }

                    // If generation failed, try to fetch from Appwrite DB
                    if (archetype == null)
                    {
                        try
                        {
                            await Task.Delay(500);
                            archetype = await _appwriteService.GetCachedArchetype(player.Id);
                        }
                        catch (Exception dbEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PackOpening] DB fetch error: {dbEx.Message}");
                        }
                    }

                    if (archetype != null)
                    {
                        await _gameStateService.CacheArchetype(archetype);
                        crestUrl = archetype.CrestImageUrl;
                        System.Diagnostics.Debug.WriteLine($"[PackOpening] SUCCESS: {player.FullName} -> {archetype.ArchetypeName}");
                    }
                }

                // Update the card with crest URL
                Cards[i].CrestImageUrl = crestUrl;
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
            _gameStateService.SetGeneratingCrests(false);
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
