using BasketballArchetypeCollector.ViewModels;
using System.Collections.Specialized;

namespace BasketballArchetypeCollector.Views;

public partial class PackOpeningPage : ContentPage
{
    private readonly PackOpeningViewModel _viewModel;
    private bool _hasStartedOpening;
    private int _animatedCardCount;
    private bool _isAnimatingLoading;
    private CancellationTokenSource? _loadingAnimationCts;

    public PackOpeningPage(PackOpeningViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        // Subscribe to cards collection changes for animation
        _viewModel.Cards.CollectionChanged += OnCardsCollectionChanged;

        // Subscribe to IsOpening changes to start/stop loading animation
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PackOpeningViewModel.IsOpening))
            {
                if (_viewModel.IsOpening)
                {
                    StartLoadingAnimation();
                }
                else
                {
                    StopLoadingAnimation();
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Only auto-start ONCE per page instance
        if (!_hasStartedOpening && !_viewModel.IsOpening && _viewModel.Cards.Count == 0)
        {
            _hasStartedOpening = true;
            _animatedCardCount = 0;
            await Task.Delay(100); // Small delay for smooth transition
            if (_viewModel.OpenPackCommand.CanExecute(null))
            {
                _viewModel.OpenPackCommand.Execute(null);
            }
        }
    }

    private void StartLoadingAnimation()
    {
        if (_isAnimatingLoading) return;
        _isAnimatingLoading = true;
        _loadingAnimationCts = new CancellationTokenSource();

        // Start the animation loop
        _ = AnimateLoadingCards(_loadingAnimationCts.Token);
    }

    private void StopLoadingAnimation()
    {
        _isAnimatingLoading = false;
        _loadingAnimationCts?.Cancel();
        _loadingAnimationCts = null;
    }

    private async Task AnimateLoadingCards(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _isAnimatingLoading)
            {
                // Wobble the top card left and right
                await TopCard.RotateTo(12, 400, Easing.SinInOut);
                if (ct.IsCancellationRequested) break;

                await TopCard.RotateTo(-2, 400, Easing.SinInOut);
                if (ct.IsCancellationRequested) break;

                // Pulse scale
                await TopCard.ScaleTo(1.05, 300, Easing.SinInOut);
                if (ct.IsCancellationRequested) break;

                await TopCard.ScaleTo(1.0, 300, Easing.SinInOut);
                if (ct.IsCancellationRequested) break;

                // Wobble back
                await TopCard.RotateTo(6, 400, Easing.SinInOut);
                if (ct.IsCancellationRequested) break;

                // Brief pause
                await Task.Delay(200, ct);
            }
        }
        catch (TaskCanceledException)
        {
            // Animation was cancelled, reset to default state
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PackOpeningPage] Loading animation error: {ex.Message}");
        }

        // Reset to default position
        TopCard.Rotation = 6;
        TopCard.Scale = 1.0;
    }

    private async void OnCardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            // Wait a moment for the UI to update
            await Task.Delay(50);

            // Animate any new cards
            await AnimateNewCards();
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _animatedCardCount = 0;
        }
    }

    private async Task AnimateNewCards()
    {
        try
        {
            var children = CardsContainer.Children;

            for (int i = _animatedCardCount; i < children.Count; i++)
            {
                if (children[i] is Border cardBorder)
                {
                    // Stagger the animation slightly for each card
                    var delay = (i - _animatedCardCount) * 100;
                    _ = AnimateCardReveal(cardBorder, delay);
                }
            }

            _animatedCardCount = children.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PackOpeningPage] Animation error: {ex.Message}");
        }
    }

    private async Task AnimateCardReveal(Border card, int delayMs)
    {
        await Task.Delay(delayMs);

        // Start with card hidden, small, and rotated
        card.Opacity = 0;
        card.Scale = 0.3;
        card.Rotation = -15;

        // Animate in with a bounce/shake effect
        var fadeIn = card.FadeTo(1, 300, Easing.CubicOut);
        var scaleUp = card.ScaleTo(1.1, 250, Easing.CubicOut);

        await Task.WhenAll(fadeIn, scaleUp);

        // Shake left and right
        await card.RotateTo(12, 80, Easing.CubicInOut);
        await card.RotateTo(-8, 80, Easing.CubicInOut);
        await card.RotateTo(5, 60, Easing.CubicInOut);
        await card.RotateTo(-3, 50, Easing.CubicInOut);
        await card.RotateTo(0, 40, Easing.CubicOut);

        // Settle to final size
        await card.ScaleTo(1.0, 150, Easing.CubicOut);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Don't unsubscribe here as we may come back to this page
    }
}
