using System.ComponentModel;
using System.Runtime.CompilerServices;
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

    async void OnPasswordCompleted(object? sender, EventArgs e)
    {
        if (LoginButton.IsEnabled)
        {
            await OnLoginClicked(sender, e);
        }
    }

    void UpdateLoginButtonState()
    {
        var hasEmail = !string.IsNullOrWhiteSpace(EmailEntry.Text);
        var hasPassword = !string.IsNullOrWhiteSpace(PasswordEntry.Text);
        LoginButton.IsEnabled = hasEmail && hasPassword;
    }

    async Task OnLoginClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        LoginButton.IsEnabled = false;
        LoginButton.Text = "Signing in...";

        try
        {
            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text;

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

                // Navigate to main app
                await Shell.Current.GoToAsync("//dashboard");
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

            if (user != null && VerifyPassword(password, user.PasswordHash))
            {
                return (true, user);
            }

            return (false, null);
        }
        catch
        {
            return (false, null);
        }
    }

    bool VerifyPassword(string password, string hash)
    {
        // Simple verification - in production use BCrypt or similar
        // For demo: password123 hashes to a known value
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    async void OnForgotPasswordClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Forgot Password", "Contact your system administrator to reset your password.", "OK");
    }
}