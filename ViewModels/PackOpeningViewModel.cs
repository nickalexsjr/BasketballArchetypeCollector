using System.Collections.ObjectModel;
using BasketballArchetypeCollector.Models;
using BasketballArchetypeCollector.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballArchetypeCollector.ViewModels;

public partial class PackOpeningViewModel : BaseViewModel, IQueryAttributable
{
    private readonly GameStateService _gameStateService;
    private readonly PlayerDataService _playerDataService;

    [ObservableProperty]
    private Pack? _pack;

    [ObservableProperty]
    private ObservableCollection<Player> _cards = new();

    [ObservableProperty]
    private int _currentCardIndex;

    [ObservableProperty]
    private Player? _currentCard;

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

    public PackOpeningViewModel(GameStateService gameStateService, PlayerDataService playerDataService)
    {
        _gameStateService = gameStateService;
        _playerDataService = playerDataService;
        Title = "Open Pack";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("packId", out var packIdObj) && packIdObj is string packId)
        {
            Pack = PackConfig.GetPackById(packId);
        }
    }

    [RelayCommand]
    private async Task OpenPackAsync()
    {
        if (Pack == null || IsOpening) return;

        IsOpening = true;
        Cards.Clear();
        CurrentCardIndex = 0;
        AllRevealed = false;
        LoadingProgress = 0;
        ProgressBarWidth = 0;
        LoadingMessage = "Shuffling the deck...";

        try
        {
            // Ensure players are loaded first
            await _playerDataService.LoadPlayersAsync();

            // Animated loading sequence
            await AnimateLoading();

            var cards = await _gameStateService.OpenPack(Pack);

            // Calculate sell all value
            SellAllValue = 0;
            foreach (var card in cards)
            {
                Cards.Add(card);
                SellAllValue += RarityConfig.GetSellValue(card.Rarity);
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
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsOpening = false;
        }
    }

    private async Task AnimateLoading()
    {
        var messages = new[]
        {
            ("Shuffling the deck...", 15),
            ("Selecting cards...", 35),
            ("Checking rarities...", 55),
            ("Applying luck bonus...", 75),
            ("Revealing your cards...", 90)
        };

        foreach (var (message, progress) in messages)
        {
            LoadingMessage = message;
            LoadingProgress = progress;
            ProgressBarWidth = progress * 2.5; // 250px max width
            await Task.Delay(400);
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
        IsNewCard = _gameStateService.OwnsCard(CurrentCard.Id);

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
    private async Task SkipToEnd()
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

        await OpenPackAsync();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ViewCard(Player player)
    {
        if (player == null) return;
        await Shell.Current.GoToAsync($"playerdetail?playerId={player.Id}");
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
                foreach (var card in Cards.ToList())
                {
                    if (_gameStateService.OwnsCard(card.Id))
                    {
                        await _gameStateService.SellCard(card.Id);
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
