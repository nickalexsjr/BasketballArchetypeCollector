using BasketballArchetypeCollector.Services;
using BasketballArchetypeCollector.Views.Popups;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballArchetypeCollector.ViewModels;

public partial class DailyViewModel : BaseViewModel
{
    private readonly GameStateService _gameStateService;
    private readonly MiniGameService _miniGameService;

    private const int BaseReward = 100;
    private const int StreakBonus = 50;

    [ObservableProperty]
    private int _coins;

    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    private bool _canClaim;

    [ObservableProperty]
    private string _lastClaimText = string.Empty;

    [ObservableProperty]
    private string _claimButtonText = string.Empty;

    [ObservableProperty]
    private int _nextRewardAmount;

    // Mini-game availability
    [ObservableProperty]
    private bool _canLuckySpin;

    [ObservableProperty]
    private bool _canMysteryBox;

    [ObservableProperty]
    private bool _canCoinFlip;

    [ObservableProperty]
    private bool _canTrivia;

    // Mini-game status text
    [ObservableProperty]
    private string _luckySpinStatus = string.Empty;

    [ObservableProperty]
    private string _mysteryBoxStatus = string.Empty;

    [ObservableProperty]
    private string _coinFlipStatus = string.Empty;

    [ObservableProperty]
    private string _triviaStatus = string.Empty;

    public DailyViewModel(GameStateService gameStateService, MiniGameService miniGameService)
    {
        _gameStateService = gameStateService;
        _miniGameService = miniGameService;
        Title = "Daily";

        _gameStateService.StateChanged += OnStateChanged;
        Refresh();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        var state = _gameStateService.CurrentState;
        Coins = state.Coins;
        CurrentStreak = state.DailyStreak;

        var lastClaim = state.LastDailyClaimUtc;
        var now = DateTime.UtcNow;

        // Check if can claim (hasn't claimed today)
        if (lastClaim == null || lastClaim == DateTime.MinValue)
        {
            CanClaim = true;
            LastClaimText = "Start your streak today!";
            ClaimButtonText = "Tap to claim!";
        }
        else
        {
            var hoursSinceClaim = (now - lastClaim.Value).TotalHours;

            if (hoursSinceClaim >= 24 && hoursSinceClaim < 48)
            {
                // Can claim - within 24-48 hour window
                CanClaim = true;
                LastClaimText = $"Last claimed {FormatTimeAgo(lastClaim.Value)}";
                ClaimButtonText = "Tap to claim!";
            }
            else if (hoursSinceClaim >= 48)
            {
                // Streak broken - can still claim but streak resets
                CanClaim = true;
                CurrentStreak = 0; // Will reset when claimed
                LastClaimText = "Streak broken! Start fresh today.";
                ClaimButtonText = "Tap to start new streak!";
            }
            else
            {
                // Already claimed today
                CanClaim = false;
                var hoursUntilNext = 24 - hoursSinceClaim;
                LastClaimText = $"Last claimed {FormatTimeAgo(lastClaim.Value)}";
                ClaimButtonText = $"Come back in {FormatTimeRemaining(hoursUntilNext)}";
            }
        }

        // Calculate next reward
        var effectiveStreak = CanClaim ? (CurrentStreak + 1) : CurrentStreak;
        if (effectiveStreak == 0) effectiveStreak = 1;
        NextRewardAmount = BaseReward + (effectiveStreak - 1) * StreakBonus;

        // Update mini-game cooldowns
        UpdateMiniGameCooldowns();
    }

    private void UpdateMiniGameCooldowns()
    {
        // Use MiniGameService to check all cooldowns
        (CanLuckySpin, LuckySpinStatus) = _miniGameService.CheckLuckySpinCooldown();
        (CanMysteryBox, MysteryBoxStatus) = _miniGameService.CheckMysteryBoxCooldown();
        (CanCoinFlip, CoinFlipStatus) = _miniGameService.CheckCoinFlipCooldown();
        (CanTrivia, TriviaStatus) = _miniGameService.CheckTriviaCooldown();
    }

    [RelayCommand]
    private async Task ClaimDailyAsync()
    {
        if (!CanClaim) return;

        var state = _gameStateService.CurrentState;
        var now = DateTime.UtcNow;
        var lastClaim = state.LastDailyClaimUtc;

        // Check if streak should reset
        if (lastClaim != null && lastClaim != DateTime.MinValue)
        {
            var hoursSinceClaim = (now - lastClaim.Value).TotalHours;
            if (hoursSinceClaim >= 48)
            {
                // Streak broken - reset to 1
                state.DailyStreak = 0;
            }
        }

        // Increment streak
        state.DailyStreak++;
        CurrentStreak = state.DailyStreak;

        // Calculate reward
        var reward = BaseReward + (state.DailyStreak - 1) * StreakBonus;

        // Update last claim time
        state.LastDailyClaimUtc = now;

        // Add coins (this also saves and syncs)
        await _gameStateService.AddCoins(reward);

        // Update UI
        Coins = state.Coins;
        CanClaim = false;
        LastClaimText = "Claimed just now!";
        ClaimButtonText = "Come back in 24 hours";
        NextRewardAmount = BaseReward + state.DailyStreak * StreakBonus;

        // Show celebration
        await Shell.Current.DisplayAlert(
            "Daily Bonus Claimed!",
            $"You earned {reward} coins!\n\nStreak: {state.DailyStreak} days",
            "Awesome!");
    }

    private string FormatTimeAgo(DateTime utcTime)
    {
        var ago = DateTime.UtcNow - utcTime;
        if (ago.TotalMinutes < 60)
            return $"{(int)ago.TotalMinutes}m ago";
        if (ago.TotalHours < 24)
            return $"{(int)ago.TotalHours}h ago";
        return $"{(int)ago.TotalDays}d ago";
    }

    private string FormatTimeRemaining(double hours)
    {
        if (hours < 1)
            return $"{(int)(hours * 60)}m";
        return $"{(int)hours}h {(int)((hours % 1) * 60)}m";
    }

    // Mini-game commands
    [RelayCommand]
    private async Task OpenLuckySpin()
    {
        if (!CanLuckySpin)
        {
            await Shell.Current.DisplayAlert("Cooldown Active", LuckySpinStatus, "OK");
            return;
        }

        var popup = new LuckySpinPopup(_miniGameService);
        await Shell.Current.Navigation.PushModalAsync(popup);
        Refresh();
    }

    [RelayCommand]
    private async Task OpenMysteryBox()
    {
        if (!CanMysteryBox)
        {
            await Shell.Current.DisplayAlert("Cooldown Active", MysteryBoxStatus, "OK");
            return;
        }

        var popup = new MysteryBoxPopup(_miniGameService);
        await Shell.Current.Navigation.PushModalAsync(popup);
        Refresh();
    }

    [RelayCommand]
    private async Task OpenCoinFlip()
    {
        if (!CanCoinFlip)
        {
            await Shell.Current.DisplayAlert("Cooldown Active", CoinFlipStatus, "OK");
            return;
        }

        var popup = new CoinFlipPopup(_miniGameService);
        await Shell.Current.Navigation.PushModalAsync(popup);
        Refresh();
    }

    [RelayCommand]
    private async Task OpenTrivia()
    {
        if (!CanTrivia)
        {
            await Shell.Current.DisplayAlert("Cooldown Active", TriviaStatus, "OK");
            return;
        }

        var popup = new TriviaPopup(_miniGameService);
        await Shell.Current.Navigation.PushModalAsync(popup);
        Refresh();
    }
}
