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
    private bool _isLoadingArchetype;

    [ObservableProperty]
    private bool _hasArchetype;

    [ObservableProperty]
    private string _archetypeName = string.Empty;

    [ObservableProperty]
    private string _archetypeDescription = string.Empty;

    [ObservableProperty]
    private string? _crestImageUrl;

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
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("playerId", out var playerIdObj) && playerIdObj is string playerId)
        {
            LoadPlayer(playerId);
        }
    }

    private void LoadPlayer(string playerId)
    {
        Player = _playerDataService.GetPlayerById(playerId);
        if (Player != null)
        {
            Title = Player.FullName;
            IsOwned = _gameStateService.OwnsCard(Player.Id);
            Coins = _gameStateService.CurrentState.Coins;

            // Check for cached archetype
            var cached = _gameStateService.GetCachedArchetype(Player.Id);
            if (cached != null)
            {
                SetArchetype(cached);
            }
        }
    }

    private void SetArchetype(ArchetypeData archetype)
    {
        Archetype = archetype;
        HasArchetype = true;
        ArchetypeName = archetype.ArchetypeName;
        ArchetypeDescription = archetype.Description;
        CrestImageUrl = archetype.CrestImageUrl;
    }

    [RelayCommand]
    private async Task GenerateArchetypeAsync()
    {
        if (Player == null || IsLoadingArchetype) return;

        IsLoadingArchetype = true;

        try
        {
            var archetype = await _appwriteService.GenerateArchetype(Player);
            if (archetype != null)
            {
                await _gameStateService.CacheArchetype(archetype);
                SetArchetype(archetype);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerDetailViewModel] Archetype error: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", "Failed to generate archetype. Please try again.", "OK");
        }
        finally
        {
            IsLoadingArchetype = false;
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

    public string RarityColor => RarityConfig.Info[Player?.Rarity ?? Rarity.Common].Color;
    public string EraColor => EraConfig.GetColor(Player?.Era ?? Era.Modern);
}
