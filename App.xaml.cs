using BasketballArchetypeCollector.Services;

namespace BasketballArchetypeCollector;

public partial class App : Application
{
	private readonly AppwriteService _appwriteService;
	private readonly GameStateService _gameStateService;

	public App(AppwriteService appwriteService, GameStateService gameStateService)
	{
		InitializeComponent();
		_appwriteService = appwriteService;
		_gameStateService = gameStateService;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell(_appwriteService, _gameStateService));
	}
}