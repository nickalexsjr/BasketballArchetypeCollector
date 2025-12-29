using BasketballArchetypeCollector.Services;
using BasketballArchetypeCollector.ViewModels;
using BasketballArchetypeCollector.Views;
using Microsoft.Extensions.Logging;

namespace BasketballArchetypeCollector;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // Inter font family (primary body text)
                fonts.AddFont("Inter-Regular.ttf", "InterRegular");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");

                // Orbitron font family (display/title text)
                fonts.AddFont("Orbitron-Regular.ttf", "OrbitronRegular");
                fonts.AddFont("Orbitron-Bold.ttf", "OrbitronBold");
                fonts.AddFont("Orbitron-Black.ttf", "OrbitronBlack");
            });

        // Register services (singletons for app-wide state)
        builder.Services.AddSingleton<PlayerDataService>();
        builder.Services.AddSingleton<AppwriteService>();
        builder.Services.AddSingleton<GameStateService>();

        // Register ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<CollectionViewModel>();
        builder.Services.AddTransient<PackStoreViewModel>();
        builder.Services.AddTransient<PackOpeningViewModel>();
        builder.Services.AddTransient<PlayerDetailViewModel>();
        builder.Services.AddTransient<SignInViewModel>();
        builder.Services.AddTransient<DatabaseViewModel>();
        builder.Services.AddTransient<StatsViewModel>();

        // Register Pages
        builder.Services.AddTransient<LoadingPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CollectionPage>();
        builder.Services.AddTransient<PackStorePage>();
        builder.Services.AddTransient<PackOpeningPage>();
        builder.Services.AddTransient<PlayerDetailPage>();
        builder.Services.AddTransient<SignInPage>();
        builder.Services.AddTransient<DatabasePage>();
        builder.Services.AddTransient<StatsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
