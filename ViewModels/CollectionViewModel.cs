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
    private string _selectedOwnership = "All";

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
        ApplyFilters();
    }

    private void UpdateStats()
    {
        Coins = _gameStateService.CurrentState.Coins;
        CollectionCount = _gameStateService.CurrentState.Collection.Count;
    }

    [RelayCommand]
    private async Task LoadPlayersAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await _playerDataService.LoadPlayersAsync();
            _allPlayers = _playerDataService.Players.Where(p => p.HasStats).ToList();
            TotalPlayers = _allPlayers.Count;
            UpdateStats();
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

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedRarityChanged(string value) => ApplyFilters();
    partial void OnSelectedEraChanged(string value) => ApplyFilters();
    partial void OnSelectedOwnershipChanged(string value) => ApplyFilters();
    partial void OnSelectedSortChanged(string value) => ApplyFilters();
    partial void OnSortDescendingChanged(bool value) => ApplyFilters();

    private void ApplyFilters()
    {
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

    public bool IsOwned(string playerId) => _gameStateService.OwnsCard(playerId);
}
