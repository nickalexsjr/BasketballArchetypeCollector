using BasketballArchetypeCollector.Services;
using BasketballArchetypeCollector.Views;

namespace BasketballArchetypeCollector;

public partial class AppShell : Shell
{
    private readonly AppwriteService _appwriteService;
    private readonly GameStateService _gameStateService;

    public AppShell(AppwriteService appwriteService, GameStateService gameStateService)
    {
        InitializeComponent();

        _appwriteService = appwriteService;
        _gameStateService = gameStateService;

        // Register routes for navigation
        Routing.RegisterRoute("packopening", typeof(PackOpeningPage));
        Routing.RegisterRoute("playerdetail", typeof(PlayerDetailPage));
        Routing.RegisterRoute("signin", typeof(SignInPage));

        // Check for existing session on startup
        Loaded += OnShellLoaded;
    }

    private async void OnShellLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnShellLoaded; // Only run once

        try
        {
            // Check if user has an active session
            var user = await _appwriteService.GetCurrentUser();
            if (user != null)
            {
                // User is logged in, initialize and go to main
                System.Diagnostics.Debug.WriteLine($"[AppShell] Found existing session for {user.Email}");
                await _gameStateService.InitializeAsync(user.Id);
                await GoToAsync("//main/packs");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] No existing session, staying on login");
                // Stay on login page - no action needed
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Session check error: {ex.Message}");
            // Stay on login page on error
        }
    }
}
