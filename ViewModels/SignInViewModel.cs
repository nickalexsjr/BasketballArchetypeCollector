using BasketballArchetypeCollector.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballArchetypeCollector.ViewModels;

public partial class SignInViewModel : BaseViewModel
{
    private readonly AppwriteService _appwriteService;
    private readonly GameStateService _gameStateService;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _isSignUp;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _showSupportInfo;

    public SignInViewModel(AppwriteService appwriteService, GameStateService gameStateService)
    {
        _appwriteService = appwriteService;
        _gameStateService = gameStateService;
        Title = "Sign In";
    }

    [RelayCommand]
    private void ToggleSupportInfo()
    {
        ShowSupportInfo = !ShowSupportInfo;
    }

    [RelayCommand]
    private async Task CopyEmail()
    {
        await Clipboard.SetTextAsync("support@najdevelopments.com.au");
        await Shell.Current.DisplayAlert("Copied", "Email copied to clipboard", "OK");
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsSignUp = !IsSignUp;
        Title = IsSignUp ? "Sign Up" : "Sign In";
        ErrorMessage = string.Empty;
        HasError = false;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;

        // Validate
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Please enter email and password.");
            return;
        }

        if (IsSignUp && Password != ConfirmPassword)
        {
            ShowError("Passwords do not match.");
            return;
        }

        if (Password.Length < 8)
        {
            ShowError("Password must be at least 8 characters.");
            return;
        }

        IsBusy = true;
        HasError = false;

        try
        {
            bool success;
            if (IsSignUp)
            {
                success = await _appwriteService.SignUp(Email, Password);
            }
            else
            {
                success = await _appwriteService.Login(Email, Password);
            }

            if (success)
            {
                var session = await _appwriteService.GetCurrentSession();
                await _gameStateService.InitializeAsync(session?.UserId);
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                ShowError(IsSignUp ? "Failed to create account." : "Invalid email or password.");
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
