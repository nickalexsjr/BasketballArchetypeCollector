using BasketballArchetypeCollector.Services;
using BasketballArchetypeCollector.ViewModels;

namespace BasketballArchetypeCollector.Views;

public partial class PackStorePage : ContentPage
{
    private readonly PackStoreViewModel _viewModel;
    private readonly StoreService _storeService;

    public PackStorePage(PackStoreViewModel viewModel, StoreService storeService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _storeService = storeService;

        CoinDisplayControl.BuyCoinsRequested += OnBuyCoinsRequested;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.InitializeCommand.Execute(null);
    }

    private async void OnBuyCoinsRequested(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Buy Coins",
            "Get 5,000 coins for $5.00 AUD?",
            "Buy Now", "Cancel");

        if (!confirm) return;

        var result = await _storeService.PurchaseCoinsAsync(StoreService.CoinPack5000ProductId);

        if (result.Success)
        {
            await DisplayAlert("Success!", $"You received {result.CoinsAdded:N0} coins!", "Awesome!");
            _viewModel.InitializeCommand.Execute(null);
        }
        else if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            await DisplayAlert("Purchase Failed", result.ErrorMessage, "OK");
        }
    }
}
