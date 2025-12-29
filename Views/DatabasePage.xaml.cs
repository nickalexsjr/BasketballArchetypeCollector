namespace BasketballArchetypeCollector.Views;

using BasketballArchetypeCollector.ViewModels;

public partial class DatabasePage : ContentPage
{
    public DatabasePage(DatabaseViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DatabaseViewModel vm)
        {
            vm.LoadPlayersCommand.Execute(null);
        }
    }
}
