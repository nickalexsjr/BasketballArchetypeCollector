using BasketballArchetypeCollector.Models;

namespace BasketballArchetypeCollector.Controls;

public partial class PackCard : ContentView
{
    public static readonly BindableProperty PackProperty =
        BindableProperty.Create(nameof(Pack), typeof(Pack), typeof(PackCard), null, propertyChanged: OnPackChanged);

    public static readonly BindableProperty IsDisabledProperty =
        BindableProperty.Create(nameof(IsDisabled), typeof(bool), typeof(PackCard), false);

    public Pack? Pack
    {
        get => (Pack?)GetValue(PackProperty);
        set => SetValue(PackProperty, value);
    }

    public bool IsDisabled
    {
        get => (bool)GetValue(IsDisabledProperty);
        set => SetValue(IsDisabledProperty, value);
    }

    public string PackColor => Pack?.Color ?? "#6b7280";

    public bool HasGuaranteed => Pack?.Guaranteed.HasValue ?? false;

    public string GuaranteedText => Pack?.Guaranteed?.ToString() ?? "";

    public string GuaranteedColor => Pack?.Guaranteed != null
        ? RarityConfig.Info[Pack.Guaranteed.Value].Color
        : "#6b7280";

    public PackCard()
    {
        InitializeComponent();
    }

    private static void OnPackChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PackCard card)
        {
            card.OnPropertyChanged(nameof(PackColor));
            card.OnPropertyChanged(nameof(HasGuaranteed));
            card.OnPropertyChanged(nameof(GuaranteedText));
            card.OnPropertyChanged(nameof(GuaranteedColor));
        }
    }
}
