using BasketballArchetypeCollector.Views;

namespace BasketballArchetypeCollector;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("packopening", typeof(PackOpeningPage));
        Routing.RegisterRoute("playerdetail", typeof(PlayerDetailPage));
        Routing.RegisterRoute("signin", typeof(SignInPage));
    }
}
