using System.Collections.ObjectModel;
using BasketballArchetypeCollector.Models;
using BasketballArchetypeCollector.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballArchetypeCollector.ViewModels;

public partial class PackStoreViewModel : BaseViewModel
{
    private readonly GameStateService _gameStateService;

    [ObservableProperty]
    private ObservableCollection<Pack> _packs = new();

    [ObservableProperty]
    private int _coins;

    [ObservableProperty]
    private Pack? _selectedPack;

    public PackStoreViewModel(GameStateService gameStateService)
    {
        _gameStateService = gameStateService;
        Title = "Pack Store";

        _gameStateService.StateChanged += OnStateChanged;

        // Load packs
        foreach (var pack in PackConfig.AllPacks)
        {
            Packs.Add(pack);
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        Coins = _gameStateService.CurrentState.Coins;
    }

    [RelayCommand]
    private void Initialize()
    {
        Coins = _gameStateService.CurrentState.Coins;
    }

    [RelayCommand]
    private async Task SelectPack(Pack pack)
    {
        if (pack == null) return;

        if (!_gameStateService.CanAfford(pack.Cost))
        {
            await Shell.Current.DisplayAlert("Not Enough Coins",
                $"You need {pack.Cost} coins to buy this pack. You have {Coins} coins.",
                "OK");
            return;
        }

        SelectedPack = pack;
        await Shell.Current.GoToAsync($"packopening?packId={pack.Id}");
    }

    public bool CanAfford(int cost) => _gameStateService.CanAfford(cost);
}
