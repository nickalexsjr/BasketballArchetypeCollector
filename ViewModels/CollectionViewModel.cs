using System.Collections.ObjectModel;
using BasketballArchetypeCollector.Models;
using BasketballArchetypeCollector.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballArchetypeCollector.ViewModels;

public partial class CollectionViewModel : BaseViewModel
{
    private readonly GameStateService _gameStateService;
    private readonly PlayerDataService _playerDataService;

    private List<Player> _allPlayers = new();
    private bool _hasLoadedOnce;

    [ObservableProperty]
    private ObservableCollection<Player> _players = new();

    [ObservableProperty]
    private int _coins;

    [ObservableProperty]
    private int _collectionCount;

    [ObservableProperty]
    private int _totalPlayers;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedRarity = "All";

    [ObservableProperty]
    private string _selectedEra = "All";

    [ObservableProperty]
    private string _selectedOwnership = "Owned";

    [ObservableProperty]
    private string _selectedSort = "Overall";

    [ObservableProperty]
    private bool _sortDescending = true;

    public List<string> RarityOptions { get; } = new()
    {
        "All", "Goat", "Legendary", "Epic", "Rare", "Uncommon", "Common"
    };

    public List<string> EraOptions { get; } = new()
    {
        "All", "Modern", "2010s", "2000s", "90s", "80s", "Classic"
    };

    public List<string> OwnershipOptions { get; } = new()
    {
        "All", "Owned", "Not Owned"
    };

    public List<string> SortOptions { get; } = new()
    {
        "Overall", "Name", "PPG", "RPG", "APG", "Games"
    };

    public CollectionViewModel(GameStateService gameStateService, PlayerDataService playerDataService)
    {
        _gameStateService = gameStateService;
        _playerDataService = playerDataService;

        Title = "Collection";

        _gameStateService.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateStats();
        // Only apply filters if we've already loaded players
        if (_hasLoadedOnce && _allPlayers.Count > 0)
        {
            ApplyFilters();
        }
    }

    private void UpdateStats()
    {
        Coins = _gameStateService.CurrentState.Coins;
        CollectionCount = _gameStateService.CurrentState.Collection.Count;
    }

    [RelayCommand]
    private async Task LoadPlayersAsync()
    {
        // Allow reload but prevent concurrent loads
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[CollectionViewModel] LoadPlayersAsync starting...");

            // Load player data
            await _playerDataService.LoadPlayersAsync();
            _allPlayers = _playerDataService.Players.Where(p => p.HasStats).ToList();
            TotalPlayers = _allPlayers.Count;
            System.Diagnostics.Debug.WriteLine($"[CollectionViewModel] Loaded {_allPlayers.Count} players from data service");

            // Wait briefly for GameState to load if collection is empty
            // This handles race condition when user navigates to Collection before HomePage finishes
            int waitAttempts = 0;
            while (_gameStateService.CurrentState.Collection.Count == 0 && waitAttempts < 15)
            {
                System.Diagnostics.Debug.WriteLine($"[CollectionViewModel] Waiting for GameState to load... attempt {waitAttempts + 1}");
                await Task.Delay(200);
                waitAttempts++;
            }

            _hasLoadedOnce = true;
            UpdateStats();

            System.Diagnostics.Debug.WriteLine($"[CollectionViewModel] GameState Collection: {_gameStateService.CurrentState.Collection.Count} cards");
            System.Diagnostics.Debug.WriteLine($"[CollectionViewModel] SelectedOwnership: {SelectedOwnership}");

            ApplyFilters();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CollectionViewModel] Load error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFiltersIfReady();
    partial void OnSelectedRarityChanged(string value) => ApplyFiltersIfReady();
    partial void OnSelectedEraChanged(string value) => ApplyFiltersIfReady();
    partial void OnSelectedOwnershipChanged(string value) => ApplyFiltersIfReady();
    partial void OnSelectedSortChanged(string value) => ApplyFiltersIfReady();
    partial void OnSortDescendingChanged(bool value) => ApplyFiltersIfReady();

    private void ApplyFiltersIfReady()
    {
        if (_hasLoadedOnce && _allPlayers.Count > 0)
        {
            ApplyFilters();
        }
    }

    private void ApplyFilters()
    {
        System.Diagnostics.Debug.WriteLine($"[CollectionViewModel] ApplyFilters: AllPlayers={_allPlayers.Count}, Ownership={SelectedOwnership}, OwnedCount={_gameStateService.CurrentState.Collection.Count}");

        var filtered = _allPlayers.AsEnumerable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.FullName.ToLowerInvariant().Contains(query) ||
                p.TeamAbbreviation.ToLowerInvariant().Contains(query));
        }

        // Rarity filter
        if (SelectedRarity != "All")
        {
            if (Enum.TryParse<Rarity>(SelectedRarity, true, out var rarity))
            {
                filtered = filtered.Where(p => p.Rarity == rarity);
            }
        }

        // Era filter
        if (SelectedEra != "All")
        {
            filtered = filtered.Where(p => EraConfig.GetLabel(p.Era) == SelectedEra);
        }

        // Ownership filter
        if (SelectedOwnership == "Owned")
        {
            filtered = filtered.Where(p => _gameStateService.OwnsCard(p.Id));
        }
        else if (SelectedOwnership == "Not Owned")
        {
            filtered = filtered.Where(p => !_gameStateService.OwnsCard(p.Id));
        }

        // Sorting
        filtered = SelectedSort switch
        {
            "Name" => SortDescending
                ? filtered.OrderByDescending(p => p.LastName).ThenByDescending(p => p.FirstName)
                : filtered.OrderBy(p => p.LastName).ThenBy(p => p.FirstName),
            "PPG" => SortDescending
                ? filtered.OrderByDescending(p => p.Ppg)
                : filtered.OrderBy(p => p.Ppg),
            "RPG" => SortDescending
                ? filtered.OrderByDescending(p => p.Rpg)
                : filtered.OrderBy(p => p.Rpg),
            "APG" => SortDescending
                ? filtered.OrderByDescending(p => p.Apg)
                : filtered.OrderBy(p => p.Apg),
            "Games" => SortDescending
                ? filtered.OrderByDescending(p => p.Games)
                : filtered.OrderBy(p => p.Games),
            _ => SortDescending
                ? filtered.OrderByDescending(p => p.Overall).ThenByDescending(p => p.SortTiebreaker)
                : filtered.OrderBy(p => p.Overall).ThenBy(p => p.SortTiebreaker)
        };

        // Update collection
        var list = filtered.Take(500).ToList(); // Limit for performance
        System.Diagnostics.Debug.WriteLine($"[CollectionViewModel] ApplyFilters result: {list.Count} players after filtering");
        Players.Clear();
        foreach (var player in list)
        {
            Players.Add(player);
        }
    }

    [RelayCommand]
    private void ToggleSortOrder()
    {
        SortDescending = !SortDescending;
    }

    [RelayCommand]
    private async Task NavigateToPlayer(Player player)
    {
        if (player == null) return;
        await Shell.Current.GoToAsync($"playerdetail?playerId={player.Id}");
    }

    [RelayCommand]
    private async Task GoToPacks()
    {
        await Shell.Current.GoToAsync("//main/packs");
    }

    public bool IsOwned(string playerId) => _gameStateService.OwnsCard(playerId);

    public string? GetCrestUrl(string playerId)
    {
        var archetype = _gameStateService.GetCachedArchetype(playerId);
        return archetype?.CrestImageUrl;
    }
}
