using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BasketballArchetypeCollector.Services;
using BasketballArchetypeCollector.Models;

namespace BasketballArchetypeCollector.ViewModels;

public partial class StatsViewModel : BaseViewModel
{
    private readonly GameStateService _gameStateService;
    private readonly PlayerDataService _playerDataService;
    private readonly AppwriteService _appwriteService;

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

    // Pack purchase statistics
    [ObservableProperty]
    private int _standardPacksBought;

    [ObservableProperty]
    private int _premiumPacksBought;

    [ObservableProperty]
    private int _elitePacksBought;

    [ObservableProperty]
    private int _legendaryPacksBought;

    [ObservableProperty]
    private int _totalCoinsSpent;

    [ObservableProperty]
    private string _favoritePackType = string.Empty;

    // Account properties
    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _userDisplayName = string.Empty;

    [ObservableProperty]
    private bool _showSupportInfo;

    public StatsViewModel(GameStateService gameStateService, PlayerDataService playerDataService, AppwriteService appwriteService)
    {
        _gameStateService = gameStateService;
        _playerDataService = playerDataService;
        _appwriteService = appwriteService;
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

        // Load account info
        await LoadAccountInfo();

        // Load pack purchase stats (if logged in)
        await LoadPackPurchaseStats();
    }

    private async Task LoadPackPurchaseStats()
    {
        try
        {
            var user = await _appwriteService.GetCurrentUser();
            if (user != null)
            {
                var stats = await _appwriteService.GetUserPackPurchaseStats(user.Id);
                StandardPacksBought = stats.StandardPacksBought;
                PremiumPacksBought = stats.PremiumPacksBought;
                ElitePacksBought = stats.ElitePacksBought;
                LegendaryPacksBought = stats.LegendaryPacksBought;
                TotalCoinsSpent = stats.TotalCoinsSpent;

                // Format favorite pack type for display
                FavoritePackType = stats.FavoritePackType switch
                {
                    "standard" => "Standard",
                    "premium" => "Premium",
                    "elite" => "Elite",
                    "legendary" => "Legendary",
                    _ => "None yet"
                };
            }
            else
            {
                // Clear stats if not logged in
                StandardPacksBought = 0;
                PremiumPacksBought = 0;
                ElitePacksBought = 0;
                LegendaryPacksBought = 0;
                TotalCoinsSpent = 0;
                FavoritePackType = "Sign in to track";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StatsViewModel] LoadPackPurchaseStats error: {ex.Message}");
        }
    }

    private async Task LoadAccountInfo()
    {
        try
        {
            var user = await _appwriteService.GetCurrentUser();
            if (user != null)
            {
                IsLoggedIn = true;
                UserEmail = user.Email;
                UserDisplayName = user.DisplayName ?? user.Email;
            }
            else
            {
                IsLoggedIn = false;
                UserEmail = string.Empty;
                UserDisplayName = string.Empty;
            }
        }
        catch
        {
            IsLoggedIn = false;
        }
    }

    [RelayCommand]
    private async Task SignIn()
    {
        // Navigate to login page without tabs
        await Shell.Current.GoToAsync("//login");
    }

    [RelayCommand]
    private async Task SignOut()
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Sign Out",
            "Are you sure you want to sign out?",
            "Sign Out", "Cancel");

        if (confirm)
        {
            await _appwriteService.SignOut();
            IsLoggedIn = false;
            UserEmail = string.Empty;
            UserDisplayName = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleSupportInfo()
    {
        ShowSupportInfo = !ShowSupportInfo;
    }

    [RelayCommand]
    private async Task CopyEmail()
    {
        await Clipboard.SetTextAsync("support@najdevelopments.com.au");
        await Shell.Current.DisplayAlert("Copied", "Email copied to clipboard", "OK");
    }

    [RelayCommand]
    private async Task DeleteAccount()
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Delete Account",
            "Are you sure you want to delete your account? This cannot be undone. Your collection and progress will be lost.",
            "Delete", "Cancel");

        if (confirm)
        {
            var doubleConfirm = await Shell.Current.DisplayAlert(
                "Final Confirmation",
                "This will permanently delete:\n• Your account\n• Your collection data\n• Your pack purchase history\n\nThis action is permanent.",
                "I Understand, Delete", "Cancel");

            if (doubleConfirm)
            {
                try
                {
                    // Delete account and all associated data
                    await _appwriteService.DeleteAccount();
                    IsLoggedIn = false;
                    UserEmail = string.Empty;
                    UserDisplayName = string.Empty;

                    // Clear pack purchase stats
                    StandardPacksBought = 0;
                    PremiumPacksBought = 0;
                    ElitePacksBought = 0;
                    LegendaryPacksBought = 0;
                    TotalCoinsSpent = 0;
                    FavoritePackType = "Sign in to track";

                    await Shell.Current.DisplayAlert("Account Deleted", "Your account and all data have been deleted.", "OK");
                }
                catch (Exception ex)
                {
                    await Shell.Current.DisplayAlert("Error", $"Failed to delete account: {ex.Message}", "OK");
                }
            }
        }
    }
}
