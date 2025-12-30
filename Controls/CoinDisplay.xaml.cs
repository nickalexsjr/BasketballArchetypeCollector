namespace BasketballArchetypeCollector.Controls;

public partial class CoinDisplay : ContentView
{
    public static readonly BindableProperty CoinsProperty =
        BindableProperty.Create(nameof(Coins), typeof(int), typeof(CoinDisplay), 0);

    public static readonly BindableProperty ShowBuyButtonProperty =
        BindableProperty.Create(nameof(ShowBuyButton), typeof(bool), typeof(CoinDisplay), false);

    public int Coins
    {
        get => (int)GetValue(CoinsProperty);
        set => SetValue(CoinsProperty, value);
    }

    public bool ShowBuyButton
    {
        get => (bool)GetValue(ShowBuyButtonProperty);
        set => SetValue(ShowBuyButtonProperty, value);
    }

    public event EventHandler? BuyCoinsRequested;

    public CoinDisplay()
    {
        InitializeComponent();
    }

    private void OnBuyTapped(object? sender, EventArgs e)
    {
        BuyCoinsRequested?.Invoke(this, EventArgs.Empty);
    }
}
