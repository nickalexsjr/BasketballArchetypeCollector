namespace BasketballArchetypeCollector.Views;

using BasketballArchetypeCollector.ViewModels;

public partial class StatsPage : ContentPage
{
    public StatsPage(StatsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is StatsViewModel vm)
        {
            vm.LoadStatsCommand.Execute(null);
        }
    }
}
