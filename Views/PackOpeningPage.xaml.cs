using BasketballArchetypeCollector.ViewModels;

namespace BasketballArchetypeCollector.Views;

public partial class PackOpeningPage : ContentPage
{
    private readonly PackOpeningViewModel _viewModel;
    private bool _hasStartedOpening;

    public PackOpeningPage(PackOpeningViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Only auto-start ONCE per page instance
        if (!_hasStartedOpening && !_viewModel.IsOpening && _viewModel.Cards.Count == 0)
        {
            _hasStartedOpening = true;
            await Task.Delay(100); // Small delay for smooth transition
            if (_viewModel.OpenPackCommand.CanExecute(null))
            {
                _viewModel.OpenPackCommand.Execute(null);
            }
        }
    }
}
