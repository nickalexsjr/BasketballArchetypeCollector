namespace BasketballArchetypeCollector;

public partial class App : Application
{
	private const string InstalledKey = "bac_installed_v1";

	public App()
	{
		InitializeComponent();

		// Check for fresh install - Preferences (NSUserDefaults) is cleared on uninstall,
		// but SecureStorage (Keychain) persists. So if Preferences doesn't have our marker
		// but SecureStorage has data, it means app was reinstalled - clear old data.
		CheckForFreshInstall();
	}

	private void CheckForFreshInstall()
	{
		try
		{
			var isInstalled = Preferences.Get(InstalledKey, false);

			if (!isInstalled)
			{
				// This is a fresh install (or reinstall after uninstall)
				// Clear any persisted Keychain data from previous install
				System.Diagnostics.Debug.WriteLine("[App] Fresh install detected - clearing SecureStorage");

				SecureStorage.RemoveAll();

				// Mark as installed
				Preferences.Set(InstalledKey, true);
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("[App] Existing installation detected");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[App] Error checking fresh install: {ex.Message}");
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
