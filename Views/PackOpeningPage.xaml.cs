using BasketballArchetypeCollector.ViewModels;

namespace BasketballArchetypeCollector.Views;

public partial class PackOpeningPage : ContentPage
{
    private readonly PackOpeningViewModel _viewModel;

    public PackOpeningPage(PackOpeningViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Auto-start pack opening when page appears
        if (_viewModel.OpenPackCommand.CanExecute(null))
        {
            await Task.Delay(100); // Small delay for smooth transition
            _viewModel.OpenPackCommand.Execute(null);
        }
    }
}
