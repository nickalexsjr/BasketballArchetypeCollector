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

    public string RarityColor => RarityConfig.GetInfo(Player.Rarity).PrimaryColor;

    public CardItem(Player player, string? crestImageUrl = null)
    {
        Player = player;
        CrestImageUrl = crestImageUrl;
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

    // Card detail modal properties
    [ObservableProperty]
    private bool _showCardDetail;

    [ObservableProperty]
    private CardItem? _selectedCardForDetail;

    // Track if we've already opened a pack in this session (prevents re-opening on navigation back)
    private bool _hasOpenedPack;

    public PackOpeningViewModel(GameStateService gameStateService, PlayerDataService playerDataService, AppwriteService appwriteService)
    {
        _gameStateService = gameStateService;
        _playerDataService = playerDataService;
        _appwriteService = appwriteService;
        Title = "Open Pack";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("packId", out var packIdObj) && packIdObj is string packId)
        {
            // Reset state for new pack
            _hasOpenedPack = false;
            Cards.Clear();
            Pack = PackConfig.GetPackById(packId);
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

            // Open the pack and get cards
            var players = await _gameStateService.OpenPack(Pack);

            LoadingMessage = "Checking rarities...";
            LoadingProgress = 30;
            ProgressBarWidth = 75;
            await Task.Delay(200);

            // Calculate sell all value
            SellAllValue = 0;
            foreach (var player in players)
            {
                SellAllValue += RarityConfig.GetSellValue(player.Rarity);
            }

            // Generate crests for each card (like the HTML version) and add to Cards collection
            var totalCards = players.Count;
            for (int i = 0; i < totalCards; i++)
            {
                var player = players[i];
                var progressPercent = 30 + (int)((i + 1) / (float)totalCards * 60); // 30% to 90%

                LoadingMessage = $"Creating crest {i + 1} of {totalCards}...";
                LoadingProgress = progressPercent;
                ProgressBarWidth = progressPercent * 2.5;

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
                    // Generate new crest - fail silently, don't block pack opening
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[PackOpening] Calling GenerateArchetype for {player.FullName}...");
                        var archetype = await _appwriteService.GenerateArchetype(player);
                        if (archetype != null)
                        {
                            await _gameStateService.CacheArchetype(archetype);
                            crestUrl = archetype.CrestImageUrl;
                            System.Diagnostics.Debug.WriteLine($"[PackOpening] SUCCESS: Generated crest for {player.FullName}: {archetype.ArchetypeName}, URL: {crestUrl ?? "null"}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[PackOpening] GenerateArchetype returned null for {player.FullName}");
                        }
                    }
                    catch (Exception crestEx)
                    {
                        // Crest generation fails silently - don't show error to user
                        System.Diagnostics.Debug.WriteLine($"[PackOpening] Crest error for {player.FullName}: {crestEx.Message}");
                    }

                    // Small delay between crest generations to avoid rate limits
                    await Task.Delay(300);
                }

                // Add to Cards collection with crest URL
                Cards.Add(new CardItem(player, crestUrl));
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
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void ViewCard(CardItem cardItem)
    {
        if (cardItem?.Player == null) return;

        // Show card detail as modal overlay instead of navigating away
        // This prevents the issue of losing pack results when navigating back
        SelectedCardForDetail = cardItem;
        ShowCardDetail = true;
    }

    [RelayCommand]
    private void CloseCardDetail()
    {
        ShowCardDetail = false;
        SelectedCardForDetail = null;
    }

    [RelayCommand]
    private async Task ViewFullDetail()
    {
        // For when user wants to navigate to the full detail page (e.g., to sell)
        if (SelectedCardForDetail?.Player == null) return;
        ShowCardDetail = false;
        await Shell.Current.GoToAsync($"playerdetail?playerId={SelectedCardForDetail.Player.Id}");
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
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}
