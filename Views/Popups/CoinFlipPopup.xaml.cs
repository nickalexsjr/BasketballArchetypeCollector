using BasketballArchetypeCollector.Services;

namespace BasketballArchetypeCollector.Views.Popups;

public partial class CoinFlipPopup : ContentPage
{
    private readonly MiniGameService _miniGameService;
    private bool _isFlipping = false;
    private bool _hasFlipped = false;
    private readonly Random _random = new();

    private int _betAmount = 50;

    public CoinFlipPopup(MiniGameService miniGameService)
    {
        InitializeComponent();
        _miniGameService = miniGameService;
        UpdateBetDisplay();
    }

    private void UpdateBetDisplay()
    {
        BetLabel.Text = $"BET: {_betAmount}";

        // Disable buttons if at limits or not enough coins
        DecreaseBtn.IsEnabled = _betAmount > MiniGameService.MinBet;
        IncreaseBtn.IsEnabled = _betAmount < MiniGameService.MaxBet &&
                                _betAmount + MiniGameService.BetStep <= _miniGameService.GetCurrentCoins();

        // Check if player can afford current bet
        bool canAfford = _miniGameService.CanAffordBet(_betAmount);
        HeadsBtn.IsEnabled = canAfford && !_hasFlipped;
        TailsBtn.IsEnabled = canAfford && !_hasFlipped;

        if (!canAfford)
        {
            SubtitleLabel.Text = "Not enough coins!";
            SubtitleLabel.TextColor = Color.FromArgb("#f87171");
        }
    }

    private void OnIncreaseBet(object sender, EventArgs e)
    {
        if (_betAmount + MiniGameService.BetStep <= MiniGameService.MaxBet &&
            _betAmount + MiniGameService.BetStep <= _miniGameService.GetCurrentCoins())
        {
            _betAmount += MiniGameService.BetStep;
            UpdateBetDisplay();
        }
    }

    private void OnDecreaseBet(object sender, EventArgs e)
    {
        if (_betAmount - MiniGameService.BetStep >= MiniGameService.MinBet)
        {
            _betAmount -= MiniGameService.BetStep;
            UpdateBetDisplay();
        }
    }

    private async void OnHeadsClicked(object sender, EventArgs e)
    {
        await FlipCoin(true);
    }

    private async void OnTailsClicked(object sender, EventArgs e)
    {
        await FlipCoin(false);
    }

    private async Task FlipCoin(bool choseHeads)
    {
        if (_isFlipping || _hasFlipped) return;
        if (!_miniGameService.CanAffordBet(_betAmount)) return;

        _isFlipping = true;
        _hasFlipped = true;

        // Disable buttons
        HeadsBtn.IsEnabled = false;
        TailsBtn.IsEnabled = false;
        DecreaseBtn.IsEnabled = false;
        IncreaseBtn.IsEnabled = false;

        // Hide bet display, show coin
        BetBorder.IsVisible = false;
        ChoiceGrid.IsVisible = false;
        CoinLabel.IsVisible = true;

        // Coin flip animation
        string[] flipFrames = { "🪙", "⚪", "🪙", "⚪", "🪙" };
        for (int i = 0; i < 12; i++)
        {
            CoinLabel.Text = flipFrames[i % flipFrames.Length];
            await CoinLabel.ScaleTo(1.2, 60);
            await CoinLabel.ScaleTo(1.0, 60);
        }

        // Determine result via service
        var (isHeads, won) = _miniGameService.FlipCoin(choseHeads);

        // Final coin display
        CoinLabel.Text = isHeads ? "👑" : "🔴";
        await Task.Delay(300);

        CoinLabel.IsVisible = false;
        ResultStack.IsVisible = true;

        // Complete the flip via service
        int result = await _miniGameService.CompleteCoinFlip(_betAmount, won);

        if (won)
        {
            ResultEmoji.Text = "🎉";
            ResultLabel.Text = $"+{_betAmount} coins!";
            ResultLabel.TextColor = Color.FromArgb("#22c55e");
            SubtitleLabel.Text = $"{(isHeads ? "HEADS" : "TAILS")} - You WIN!";
            SubtitleLabel.TextColor = Color.FromArgb("#22c55e");
        }
        else
        {
            ResultEmoji.Text = "😢";
            ResultLabel.Text = $"-{_betAmount} coins";
            ResultLabel.TextColor = Color.FromArgb("#f87171");
            SubtitleLabel.Text = $"{(isHeads ? "HEADS" : "TAILS")} - You lose!";
            SubtitleLabel.TextColor = Color.FromArgb("#f87171");
        }

        CloseButton.Text = "Close";
        CloseButton.BackgroundColor = Color.FromArgb("#3b82f6");
        CloseButton.TextColor = Colors.White;

        _isFlipping = false;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
