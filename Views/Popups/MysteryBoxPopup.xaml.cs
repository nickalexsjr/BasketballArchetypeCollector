using BasketballArchetypeCollector.Services;

namespace BasketballArchetypeCollector.Views.Popups;

public partial class MysteryBoxPopup : ContentPage
{
    private readonly MiniGameService _miniGameService;
    private bool _hasOpened = false;
    private readonly int[] _boxPrizes;

    public MysteryBoxPopup(MiniGameService miniGameService)
    {
        InitializeComponent();
        _miniGameService = miniGameService;

        // Generate prizes for all 6 boxes via service
        _boxPrizes = _miniGameService.GenerateMysteryBoxPrizes(6);
    }

    private async void OnBoxTapped(object? sender, TappedEventArgs e)
    {
        if (_hasOpened) return;

        var param = e.Parameter?.ToString();
        if (!int.TryParse(param, out int boxNum) || boxNum < 1 || boxNum > 6)
            return;

        _hasOpened = true;

        // Get the selected box and its prize
        int prize = _boxPrizes[boxNum - 1];

        // Animate the selected box
        var selectedBox = GetBox(boxNum);
        var selectedLabel = GetBoxLabel(boxNum);

        if (selectedBox != null && selectedLabel != null)
        {
            // Shake animation
            for (int i = 0; i < 6; i++)
            {
                await selectedBox.RotateTo(i % 2 == 0 ? 5 : -5, 50);
            }
            await selectedBox.RotateTo(0, 50);

            // Show prize in selected box
            selectedLabel.Text = $"{prize}";
            selectedBox.BackgroundColor = Color.FromArgb("#22c55e");
        }

        // Reveal all other boxes
        for (int i = 1; i <= 6; i++)
        {
            if (i != boxNum)
            {
                var box = GetBox(i);
                var label = GetBoxLabel(i);
                if (box != null && label != null)
                {
                    label.Text = $"{_boxPrizes[i - 1]}";
                    box.BackgroundColor = Color.FromArgb("#334155");
                    box.Opacity = 0.6;
                }
            }
        }

        // Short delay then show result
        await Task.Delay(500);

        // Complete via service (awards coins + sets cooldown)
        await _miniGameService.CompleteMysteryBox(prize);

        // Show result
        ResultStack.IsVisible = true;
        ResultLabel.Text = $"+{prize} coins!";

        CloseButton.Text = "Collect & Close";
        CloseButton.BackgroundColor = Color.FromArgb("#a855f7");
        CloseButton.TextColor = Colors.White;
    }

    private Border? GetBox(int num)
    {
        return num switch
        {
            1 => Box1,
            2 => Box2,
            3 => Box3,
            4 => Box4,
            5 => Box5,
            6 => Box6,
            _ => null
        };
    }

    private Label? GetBoxLabel(int num)
    {
        return num switch
        {
            1 => Box1Label,
            2 => Box2Label,
            3 => Box3Label,
            4 => Box4Label,
            5 => Box5Label,
            6 => Box6Label,
            _ => null
        };
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
