using BasketballArchetypeCollector.Models;

namespace BasketballArchetypeCollector.Controls;

public partial class PlayerCard : ContentView
{
    public static readonly BindableProperty PlayerProperty =
        BindableProperty.Create(nameof(Player), typeof(Player), typeof(PlayerCard), null, propertyChanged: OnPlayerChanged);

    public static readonly BindableProperty IsLockedProperty =
        BindableProperty.Create(nameof(IsLocked), typeof(bool), typeof(PlayerCard), false);

    public static readonly BindableProperty CardSizeProperty =
        BindableProperty.Create(nameof(CardSize), typeof(CardSizeOption), typeof(PlayerCard), CardSizeOption.Medium);

    public Player? Player
    {
        get => (Player?)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    public CardSizeOption CardSize
    {
        get => (CardSizeOption)GetValue(CardSizeProperty);
        set => SetValue(CardSizeProperty, value);
    }

    public string RarityColor => Player != null ? RarityConfig.Info[Player.Rarity].Color : "#6b7280";
    public string EraColor => Player != null ? EraConfig.GetColor(Player.Era) : "#6b7280";

    public double CardWidth => CardSize switch
    {
        CardSizeOption.Small => 100,
        CardSizeOption.Medium => 140,
        CardSizeOption.Large => 200,
        _ => 140
    };

    public double CardHeight => CardSize switch
    {
        CardSizeOption.Small => 140,
        CardSizeOption.Medium => 200,
        CardSizeOption.Large => 280,
        _ => 200
    };

    public PlayerCard()
    {
        InitializeComponent();
    }

    private static void OnPlayerChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PlayerCard card)
        {
            card.OnPropertyChanged(nameof(RarityColor));
            card.OnPropertyChanged(nameof(EraColor));
        }
    }
}

public enum CardSizeOption
{
    Small,
    Medium,
    Large
}
