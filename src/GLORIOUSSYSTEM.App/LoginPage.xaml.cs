using BCrypt.Net;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Check for remembered credentials
        if (Preferences.Get("RememberMe", false))
        {
            EmailEntry.Text = Preferences.Get("SavedEmail", "");
            PasswordEntry.Text = Preferences.Get("SavedPassword", "");
            RememberMeCheckBox.IsChecked = true;
            UpdateLoginButtonState();
        }
    }

    void OnFieldChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateLoginButtonState();
    }

    void OnEmailCompleted(object? sender, EventArgs e)
    {
        PasswordEntry.Focus();
    }

    void OnPasswordCompleted(object? sender, EventArgs e)
    {
        if (LoginButton.IsEnabled)
        {
            OnLoginClicked(sender, e);
        }
    }

    void UpdateLoginButtonState()
    {
        var hasEmail = !string.IsNullOrWhiteSpace(EmailEntry.Text);
        var hasPassword = !string.IsNullOrWhiteSpace(PasswordEntry.Text);
        LoginButton.IsEnabled = hasEmail && hasPassword;
    }

    async void OnLoginClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        LoginButton.IsEnabled = false;
        LoginButton.Text = "Signing in...";

        try
        {
            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text?.Trim();

            // Validate credentials against database
            var (success, user) = await ValidateCredentialsAsync(email, password);

            if (success && user != null)
            {
                // Save remember me preference
                if (RememberMeCheckBox.IsChecked)
                {
                    Preferences.Set("RememberMe", true);
                    Preferences.Set("SavedEmail", email);
                    Preferences.Set("SavedPassword", password);
                }
                else
                {
                    Preferences.Remove("RememberMe");
                    Preferences.Remove("SavedEmail");
                    Preferences.Remove("SavedPassword");
                }

                // Store user session
                Preferences.Set("CurrentUserId", user.Id);
                Preferences.Set("CurrentUserEmail", user.Email);
                Preferences.Set("CurrentUserName", user.Name);
                Preferences.Set("IsLoggedIn", true);

                // Navigate to main app by replacing the window's page
                if (Application.Current?.Windows.Count > 0)
                {
                    var window = Application.Current.Windows[0];
                    window.Page = new AppShell();
                }
            }
            else
            {
                ErrorLabel.Text = "Invalid email or password";
                ErrorLabel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Login failed: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "Sign In";
        }
    }

    async Task<(bool success, User? user)> ValidateCredentialsAsync(string email, string password)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive == 1);

            if (user != null)
            {
                var verifyResult = VerifyPassword(password, user.PasswordHash);
                if (verifyResult)
                {
                    return (true, user);
                }
            }

            return (false, null);
        }
        catch (Exception)
        {
            return (false, null);
        }
    }

    bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception)
        {
            return false;
        }
    }

    async void OnForgotPasswordClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Forgot Password", "Contact your system administrator to reset your password.", "OK");
    }
}