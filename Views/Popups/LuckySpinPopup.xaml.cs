using BasketballArchetypeCollector.Services;

namespace BasketballArchetypeCollector.Views.Popups;

public partial class LuckySpinPopup : ContentPage
{
    private readonly MiniGameService _miniGameService;
    private bool _isSpinning = false;
    private bool _hasSpun = false;
    private readonly Random _random = new();

    public LuckySpinPopup(MiniGameService miniGameService)
    {
        InitializeComponent();
        _miniGameService = miniGameService;
    }

    private async void OnSpinClicked(object sender, EventArgs e)
    {
        if (_isSpinning) return;

        if (_hasSpun)
        {
            await Navigation.PopModalAsync();
            return;
        }

        _isSpinning = true;
        SpinButton.IsEnabled = false;
        SpinButton.Text = "Spinning...";
        InitialLabel.IsVisible = false;
        SpinningLabel.IsVisible = true;
        ResultStack.IsVisible = false;

        // Animate spinning numbers
        var spinValues = new[] { "50", "100", "250", "500", "1000", "???", "!!!" };
        int spinCount = 20 + _random.Next(10);

        for (int i = 0; i < spinCount; i++)
        {
            SpinningLabel.Text = spinValues[_random.Next(spinValues.Length)];
            int delay = i < spinCount - 5 ? 80 : 80 + (i - (spinCount - 5)) * 40;
            await Task.Delay(delay);
        }

        // Complete the spin via service
        int prize = await _miniGameService.CompleteLuckySpin();

        // Show final result
        SpinningLabel.IsVisible = false;
        ResultStack.IsVisible = true;
        ResultLabel.Text = $"+{prize}";

        SpinButton.Text = $"🎉 Won {prize} coins!";
        SpinButton.BackgroundColor = Color.FromArgb("#22c55e");
        SpinButton.IsEnabled = true;

        CloseButton.Text = "Collect & Close";
        CloseButton.BackgroundColor = Color.FromArgb("#f97316");

        _isSpinning = false;
        _hasSpun = true;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
