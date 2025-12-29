namespace BasketballArchetypeCollector.Controls;

public partial class CoinDisplay : ContentView
{
    public static readonly BindableProperty CoinsProperty =
        BindableProperty.Create(nameof(Coins), typeof(int), typeof(CoinDisplay), 0);

    public int Coins
    {
        get => (int)GetValue(CoinsProperty);
        set => SetValue(CoinsProperty, value);
    }

    public CoinDisplay()
    {
        InitializeComponent();
    }
}
