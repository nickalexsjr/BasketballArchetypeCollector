using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BasketballArchetypeCollector.Services;
using BasketballArchetypeCollector.Models;

namespace BasketballArchetypeCollector.ViewModels;

public partial class StatsViewModel : BaseViewModel
{
    private readonly GameStateService _gameStateService;
    private readonly PlayerDataService _playerDataService;

    [ObservableProperty]
    private int _coins;

    [ObservableProperty]
    private int _packsOpened;

    [ObservableProperty]
    private int _cardsCollected;

    [ObservableProperty]
    private int _crestsGenerated;

    [ObservableProperty]
    private int _goatCount;

    [ObservableProperty]
    private int _legendaryCount;

    [ObservableProperty]
    private int _epicCount;

    [ObservableProperty]
    private int _rareCount;

    [ObservableProperty]
    private int _uncommonCount;

    [ObservableProperty]
    private int _commonCount;

    [ObservableProperty]
    private int _totalPlayers;

    [ObservableProperty]
    private double _collectionPercent;

    public StatsViewModel(GameStateService gameStateService, PlayerDataService playerDataService)
    {
        _gameStateService = gameStateService;
        _playerDataService = playerDataService;
        Title = "Stats";
    }

    [RelayCommand]
    private async Task LoadStats()
    {
        var gameState = _gameStateService.CurrentState;
        await _playerDataService.LoadPlayersAsync();
        var allPlayers = _playerDataService.Players;

        Coins = gameState.Coins;
        PacksOpened = gameState.Stats.PacksOpened;
        CardsCollected = gameState.Collection.Count;

        // Count crests generated (from game state)
        CrestsGenerated = gameState.Stats.CrestsGenerated;

        TotalPlayers = allPlayers.Count;

        if (TotalPlayers > 0)
        {
            CollectionPercent = (double)CardsCollected / TotalPlayers * 100;
        }

        // Count by rarity in collection
        var collectedPlayers = allPlayers.Where(p => gameState.Collection.Contains(p.Id)).ToList();

        GoatCount = collectedPlayers.Count(p => p.Rarity == Rarity.Goat);
        LegendaryCount = collectedPlayers.Count(p => p.Rarity == Rarity.Legendary);
        EpicCount = collectedPlayers.Count(p => p.Rarity == Rarity.Epic);
        RareCount = collectedPlayers.Count(p => p.Rarity == Rarity.Rare);
        UncommonCount = collectedPlayers.Count(p => p.Rarity == Rarity.Uncommon);
        CommonCount = collectedPlayers.Count(p => p.Rarity == Rarity.Common);
    }
}
