using System.Collections.ObjectModel;
using BasketballArchetypeCollector.Models;
using BasketballArchetypeCollector.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballArchetypeCollector.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly GameStateService _gameStateService;
    private readonly PlayerDataService _playerDataService;
    private readonly AppwriteService _appwriteService;

    [ObservableProperty]
    private int _coins;

    [ObservableProperty]
    private int _collectionCount;

    [ObservableProperty]
    private int _totalPlayers;

    [ObservableProperty]
    private int _packsOpened;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Player> _featuredPlayers = new();

    public MainViewModel(GameStateService gameStateService, PlayerDataService playerDataService, AppwriteService appwriteService)
    {
        _gameStateService = gameStateService;
        _playerDataService = playerDataService;
        _appwriteService = appwriteService;

        Title = "Home";

        // Subscribe to state changes
        _gameStateService.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateFromState();
    }

    private void UpdateFromState()
    {
        var state = _gameStateService.CurrentState;
        Coins = state.Coins;
        CollectionCount = state.Collection.Count;
        PacksOpened = state.Stats.PacksOpened;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            // Load player data
            await _playerDataService.LoadPlayersAsync();
            TotalPlayers = _playerDataService.Players.Count;

            // Check auth and initialize game state
            var session = await _appwriteService.GetCurrentSession();
            IsLoggedIn = session != null;
            if (session != null)
            {
                Username = session.UserEmail ?? "Player";
            }

            await _gameStateService.InitializeAsync(session?.UserId);
            UpdateFromState();

            // Get featured players (top 6 by overall)
            var topPlayers = _playerDataService.Players
                .Where(p => p.HasStats)
                .Take(6)
                .ToList();

            FeaturedPlayers.Clear();
            foreach (var player in topPlayers)
            {
                FeaturedPlayers.Add(player);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Init error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToCollection()
    {
        await Shell.Current.GoToAsync("//collection");
    }

    [RelayCommand]
    private async Task NavigateToPacks()
    {
        await Shell.Current.GoToAsync("//packs");
    }

    [RelayCommand]
    private async Task NavigateToPlayer(Player player)
    {
        if (player == null) return;
        await Shell.Current.GoToAsync($"playerdetail?playerId={player.Id}");
    }

    [RelayCommand]
    private async Task SignIn()
    {
        // Navigate to sign in page
        await Shell.Current.GoToAsync("signin");
    }

    [RelayCommand]
    private async Task SignOut()
    {
        await _appwriteService.Logout();
        IsLoggedIn = false;
        Username = string.Empty;
        await _gameStateService.InitializeAsync(null);
        UpdateFromState();
    }

    [RelayCommand]
    private async Task ClaimDailyBonus()
    {
        // Navigate to Daily tab where user can claim properly
        await Shell.Current.GoToAsync("//daily");
    }
}
