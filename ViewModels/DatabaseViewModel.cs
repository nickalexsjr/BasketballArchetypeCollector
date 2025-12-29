using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BasketballArchetypeCollector.Services;
using BasketballArchetypeCollector.Models;
using System.Collections.ObjectModel;

namespace BasketballArchetypeCollector.ViewModels;

public partial class DatabasePlayerItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int Overall { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public double Ppg { get; set; }
    public double Rpg { get; set; }
    public double Apg { get; set; }
    public string RarityLabel { get; set; } = string.Empty;
    public string RarityColor { get; set; } = "#7F8C8D";
    public bool IsOwned { get; set; }
    public string RowBackgroundColor => IsOwned ? "#1a3a1a" : "#0f172a";
}

public partial class DatabaseViewModel : BaseViewModel
{
    private readonly PlayerDataService _playerDataService;
    private readonly GameStateService _gameStateService;
    private List<Player> _allPlayers = new();

    [ObservableProperty]
    private ObservableCollection<DatabasePlayerItem> _filteredPlayers = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedRarity = "All";

    [ObservableProperty]
    private string _selectedStatus = "All";

    [ObservableProperty]
    private int _currentPage = 0;

    [ObservableProperty]
    private int _filteredCount = 0;

    [ObservableProperty]
    private string _pageInfo = "Page 1 of 1";

    private const int PageSize = 50;

    public DatabaseViewModel(PlayerDataService playerDataService, GameStateService gameStateService)
    {
        _playerDataService = playerDataService;
        _gameStateService = gameStateService;
        Title = "Database";
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedRarityChanged(string value) => ApplyFilters();
    partial void OnSelectedStatusChanged(string value) => ApplyFilters();

    [RelayCommand]
    private async Task LoadPlayers()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            await _playerDataService.LoadPlayersAsync();
            _allPlayers = _playerDataService.Players.ToList();
            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        CurrentPage = 0;

        var filtered = _allPlayers.AsEnumerable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLower();
            filtered = filtered.Where(p =>
                $"{p.FirstName} {p.LastName}".ToLower().Contains(query));
        }

        // Rarity filter
        if (SelectedRarity != "All")
        {
            filtered = filtered.Where(p =>
                RarityConfig.Info[p.Rarity].Label.Equals(SelectedRarity, StringComparison.OrdinalIgnoreCase));
        }

        // Status filter (owned vs not owned)
        var gameState = _gameStateService.CurrentState;
        if (SelectedStatus == "Unlocked")
        {
            filtered = filtered.Where(p => gameState.Collection.Contains(p.Id));
        }
        else if (SelectedStatus == "Locked")
        {
            filtered = filtered.Where(p => !gameState.Collection.Contains(p.Id));
        }

        // Sort by overall descending
        var sortedList = filtered.OrderByDescending(p => p.Overall).ThenByDescending(p => p.Ppg).ToList();

        FilteredCount = sortedList.Count;

        // Paginate
        var totalPages = Math.Max(1, (int)Math.Ceiling(sortedList.Count / (double)PageSize));
        var pagedPlayers = sortedList.Skip(CurrentPage * PageSize).Take(PageSize).ToList();

        PageInfo = $"Page {CurrentPage + 1} of {totalPages}";

        // Convert to view items
        FilteredPlayers.Clear();
        int rank = CurrentPage * PageSize + 1;
        foreach (var player in pagedPlayers)
        {
            var rarityInfo = RarityConfig.Info[player.Rarity];
            var isOwned = gameState.Collection.Contains(player.Id);

            FilteredPlayers.Add(new DatabasePlayerItem
            {
                Id = player.Id,
                Rank = rank++,
                Overall = player.Overall,
                Name = $"{player.FirstName} {player.LastName}",
                Team = player.TeamAbbreviation ?? "FA",
                Ppg = player.Ppg,
                Rpg = player.Rpg,
                Apg = player.Apg,
                RarityLabel = rarityInfo.Label,
                RarityColor = rarityInfo.PrimaryColor,
                IsOwned = isOwned
            });
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(FilteredCount / (double)PageSize));
        if (CurrentPage < totalPages - 1)
        {
            CurrentPage++;
            RefreshPage();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;
            RefreshPage();
        }
    }

    private void RefreshPage()
    {
        var filtered = _allPlayers.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLower();
            filtered = filtered.Where(p =>
                $"{p.FirstName} {p.LastName}".ToLower().Contains(query));
        }

        if (SelectedRarity != "All")
        {
            filtered = filtered.Where(p =>
                RarityConfig.Info[p.Rarity].Label.Equals(SelectedRarity, StringComparison.OrdinalIgnoreCase));
        }

        var gameState = _gameStateService.CurrentState;
        if (SelectedStatus == "Unlocked")
        {
            filtered = filtered.Where(p => gameState.Collection.Contains(p.Id));
        }
        else if (SelectedStatus == "Locked")
        {
            filtered = filtered.Where(p => !gameState.Collection.Contains(p.Id));
        }

        var sortedList = filtered.OrderByDescending(p => p.Overall).ThenByDescending(p => p.Ppg).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(sortedList.Count / (double)PageSize));
        var pagedPlayers = sortedList.Skip(CurrentPage * PageSize).Take(PageSize).ToList();

        PageInfo = $"Page {CurrentPage + 1} of {totalPages}";

        FilteredPlayers.Clear();
        int rank = CurrentPage * PageSize + 1;
        foreach (var player in pagedPlayers)
        {
            var rarityInfo = RarityConfig.Info[player.Rarity];
            var isOwned = gameState.Collection.Contains(player.Id);

            FilteredPlayers.Add(new DatabasePlayerItem
            {
                Id = player.Id,
                Rank = rank++,
                Overall = player.Overall,
                Name = $"{player.FirstName} {player.LastName}",
                Team = player.TeamAbbreviation ?? "FA",
                Ppg = player.Ppg,
                Rpg = player.Rpg,
                Apg = player.Apg,
                RarityLabel = rarityInfo.Label,
                RarityColor = rarityInfo.PrimaryColor,
                IsOwned = isOwned
            });
        }
    }

    [RelayCommand]
    private async Task PlayerSelected(DatabasePlayerItem? item)
    {
        if (item == null) return;

        await Shell.Current.GoToAsync($"playerdetail?playerId={item.Id}");
    }
}
