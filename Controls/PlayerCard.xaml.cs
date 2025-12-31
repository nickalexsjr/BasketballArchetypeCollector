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

    public static readonly BindableProperty CrestImageUrlProperty =
        BindableProperty.Create(nameof(CrestImageUrl), typeof(string), typeof(PlayerCard), null, propertyChanged: OnCrestChanged);

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

    public string? CrestImageUrl
    {
        get => (string?)GetValue(CrestImageUrlProperty);
        set => SetValue(CrestImageUrlProperty, value);
    }

    public bool HasCrest => !string.IsNullOrEmpty(CrestImageUrl);

    public string RarityColor => Player != null && RarityConfig.Info.TryGetValue(Player.Rarity, out var info)
        ? info.Color
        : "#6b7280";

    public string RarityTextColor => Player != null && RarityConfig.Info.TryGetValue(Player.Rarity, out var info)
        ? info.TextColor
        : "#FFFFFF";

    // Background color with rarity tint (semi-transparent overlay on dark base)
    public string RarityBackgroundColor => Player != null ? GetRarityBackground(Player.Rarity) : "#1e293b";

    public string RarityLabel => Player != null ? RarityConfig.GetLabel(Player.Rarity) : "COMMON";

    public string EraColor => Player != null ? EraConfig.GetColor(Player.Era) : "#6b7280";

    private static string GetRarityBackground(Rarity rarity)
    {
        // Dark background with subtle rarity color tint
        return rarity switch
        {
            Rarity.Goat => "#2a1015",      // Dark crimson tint
            Rarity.Legendary => "#2a2010", // Dark gold tint
            Rarity.Epic => "#1f1a2e",      // Dark purple tint
            Rarity.Rare => "#0f1a2a",      // Dark blue tint
            Rarity.Uncommon => "#0f1f1a",  // Dark green tint
            Rarity.Common => "#1a1f24",    // Dark gray tint
            _ => "#1e293b"
        };
    }

    public double CardWidth => CardSize switch
    {
        CardSizeOption.Small => 85,    // Slightly smaller for 3 cards per row
        CardSizeOption.Medium => 130,
        CardSizeOption.Large => 170,
        _ => 130
    };

    public double CardHeight => CardSize switch
    {
        CardSizeOption.Small => 125,   // Proportionally shorter
        CardSizeOption.Medium => 210,
        CardSizeOption.Large => 280,
        _ => 210
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
            card.OnPropertyChanged(nameof(RarityTextColor));
            card.OnPropertyChanged(nameof(RarityBackgroundColor));
            card.OnPropertyChanged(nameof(RarityLabel));
            card.OnPropertyChanged(nameof(EraColor));
        }
    }

    private static void OnCrestChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PlayerCard card)
        {
            card.OnPropertyChanged(nameof(HasCrest));
        }
    }
}

public enum CardSizeOption
{
    Small,
    Medium,
    Large
}
