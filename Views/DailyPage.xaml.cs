using BasketballArchetypeCollector.ViewModels;

namespace BasketballArchetypeCollector.Views;

public partial class DailyPage : ContentPage
{
    public DailyPage(DailyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DailyViewModel vm)
        {
            vm.RefreshCommand.Execute(null);
        }
    }
}
