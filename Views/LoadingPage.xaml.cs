using BasketballArchetypeCollector.Services;

namespace BasketballArchetypeCollector.Views;

public partial class LoadingPage : ContentPage
{
    private readonly AppwriteService _appwriteService;
    private readonly GameStateService _gameStateService;
    private bool _hasCheckedSession;

    public LoadingPage(AppwriteService appwriteService, GameStateService gameStateService)
    {
        InitializeComponent();
        _appwriteService = appwriteService;
        _gameStateService = gameStateService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Only check session once per page instance
        if (_hasCheckedSession)
            return;

        _hasCheckedSession = true;

        // Small delay for smooth visual
        await Task.Delay(300);

        try
        {
            // Pre-warm the archetype function in background (fire and forget)
            // This costs nothing and speeds up first crest generation
            _ = _appwriteService.PreWarmArchetypeFunction();

            // Check if user has an active session
            var user = await _appwriteService.GetCurrentUser();
            if (user != null)
            {
                // User is logged in, initialize and go to main
                System.Diagnostics.Debug.WriteLine($"[LoadingPage] Found existing session for {user.Email}");
                await _gameStateService.InitializeAsync(user.Id);
                await Shell.Current.GoToAsync("//main/daily");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[LoadingPage] No existing session, going to login");
                await Shell.Current.GoToAsync("//login");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadingPage] Session check error: {ex.Message}");
            // Go to login on error
            await Shell.Current.GoToAsync("//login");
        }
    }
}
