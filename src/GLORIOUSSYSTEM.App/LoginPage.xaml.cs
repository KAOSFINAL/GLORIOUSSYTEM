using BCrypt.Net;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System.Net.Mail;

namespace GLORIOUSSYSTEM.App;

public partial class LoginPage : ContentPage
{
    bool? _usingDesktopLayout;
    bool _isCreateAccountMode;

    public LoginPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Remembered access restores the e-mail only. Passwords are never stored
        // in preferences, and this also clears data saved by older app builds.
        Preferences.Remove("SavedPassword");
        if (Preferences.Get("RememberMe", false))
        {
            EmailEntry.Text = Preferences.Get("SavedEmail", "");
            RememberMeCheckBox.IsChecked = true;
            UpdateLoginButtonState();
        }
    }

    void OnLoginSizeChanged(object? sender, EventArgs e)
    {
        if (LoginResponsiveGrid.Width <= 0)
            return;

        var useDesktop = DeviceInfo.Current.Idiom == DeviceIdiom.Desktop && LoginResponsiveGrid.Width >= 1040;
        if (_usingDesktopLayout == useDesktop)
            return;

        _usingDesktopLayout = useDesktop;
        LoginResponsiveGrid.ColumnDefinitions.Clear();

        if (useDesktop)
        {
            LoginResponsiveGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.15, GridUnitType.Star)));
            LoginResponsiveGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(DesktopHero, 0);
            Grid.SetColumn(LoginFormScroll, 1);
            DesktopHero.IsVisible = true;
            MobileBrandCard.IsVisible = false;
        }
        else
        {
            LoginResponsiveGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(LoginFormScroll, 0);
            DesktopHero.IsVisible = false;
            MobileBrandCard.IsVisible = true;
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

    void OnNameCompleted(object? sender, EventArgs e)
    {
        EmailEntry.Focus();
    }

    void OnPasswordCompleted(object? sender, EventArgs e)
    {
        if (_isCreateAccountMode)
        {
            ConfirmPasswordEntry.Focus();
            return;
        }

        if (LoginButton.IsEnabled)
        {
            OnLoginClicked(LoginButton, e);
        }
    }

    void OnConfirmPasswordCompleted(object? sender, EventArgs e)
    {
        if (LoginButton.IsEnabled)
            OnLoginClicked(LoginButton, e);
    }

    void OnShowPasswordChanged(object? sender, CheckedChangedEventArgs e)
    {
        PasswordEntry.IsPassword = !e.Value;
        ConfirmPasswordEntry.IsPassword = !e.Value;
    }

    void UpdateLoginButtonState()
    {
        var hasEmail = !string.IsNullOrWhiteSpace(EmailEntry.Text);
        var hasPassword = !string.IsNullOrWhiteSpace(PasswordEntry.Text);
        var hasName = !string.IsNullOrWhiteSpace(NameEntry.Text);
        var hasMatchingConfirmation = PasswordEntry.Text == ConfirmPasswordEntry.Text
            && !string.IsNullOrWhiteSpace(ConfirmPasswordEntry.Text);
        LoginButton.IsEnabled = hasEmail && hasPassword
            && (!_isCreateAccountMode || (hasName && hasMatchingConfirmation));
    }

    void OnAuthModeClicked(object sender, EventArgs e)
    {
        _isCreateAccountMode = !_isCreateAccountMode;
        ErrorLabel.IsVisible = false;
        NameField.IsVisible = _isCreateAccountMode;
        ConfirmPasswordField.IsVisible = _isCreateAccountMode;
        ForgotPasswordButton.IsVisible = !_isCreateAccountMode;
        AuthTitleLabel.Text = _isCreateAccountMode ? "Create your account" : "Welcome back";
        AuthSubtitleLabel.Text = _isCreateAccountMode
            ? "Use your Gmail or another email address to create a private local account."
            : "Sign in to view live farm conditions and crop reports.";
        LoginButton.Text = _isCreateAccountMode ? "Create account" : "Sign in";
        SemanticProperties.SetDescription(LoginButton, LoginButton.Text);
        ModePromptLabel.Text = _isCreateAccountMode ? "Already have an account?" : "New to GLORIOUS SYSTEM?";
        AuthModeButton.Text = _isCreateAccountMode ? "Sign in" : "Create account";
        UpdateLoginButtonState();
    }

    async void OnLoginClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        LoginButton.IsEnabled = false;
        LoginButton.Text = _isCreateAccountMode ? "Creating account..." : "Signing in...";

        try
        {
            var email = EmailEntry.Text?.Trim() ?? "";
            var password = PasswordEntry.Text ?? "";

            var (success, user, errorMessage) = _isCreateAccountMode
                ? await CreateAccountAsync(NameEntry.Text ?? "", email, password, ConfirmPasswordEntry.Text ?? "")
                : await ValidateCredentialsAsync(email, password);

            if (success && user != null)
            {
                CompleteLogin(user, RememberMeCheckBox.IsChecked);
            }
            else
            {
                ErrorLabel.Text = errorMessage;
                ErrorLabel.IsVisible = true;
            }
        }
        catch (Exception)
        {
            ErrorLabel.Text = "We could not complete that request. Please try again.";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = _isCreateAccountMode ? "Create account" : "Sign in";
        }
    }

    void CompleteLogin(User user, bool rememberEmail)
    {
        if (rememberEmail)
        {
            Preferences.Set("RememberMe", true);
            Preferences.Set("SavedEmail", user.Email);
        }
        else
        {
            Preferences.Remove("RememberMe");
            Preferences.Remove("SavedEmail");
        }

        Preferences.Remove("SavedPassword");
        Preferences.Set("CurrentUserId", user.Id);
        Preferences.Set("CurrentUserEmail", user.Email);
        Preferences.Set("CurrentUserName", user.Name);
        Preferences.Set("IsLoggedIn", true);

        if (Application.Current?.Windows.Count > 0)
        {
            var window = Application.Current.Windows[0];
            window.Page = new AppShell();
        }
    }

    async Task<(bool success, User? user, string errorMessage)> ValidateCredentialsAsync(string email, string password)
    {
        try
        {
            using var scope = (App.Services ?? throw new InvalidOperationException("Application services are unavailable.")).CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var normalizedEmail = NormalizeEmail(email);

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && u.IsActive == 1);

            if (user != null)
            {
                var verifyResult = VerifyPassword(password, user.PasswordHash);
                if (verifyResult)
                {
                    user.LastLoginAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    return (true, user, string.Empty);
                }
            }

            return (false, null, "Invalid email or password.");
        }
        catch (Exception)
        {
            return (false, null, "We could not sign you in. Please try again.");
        }
    }

    async Task<(bool success, User? user, string errorMessage)> CreateAccountAsync(
        string name, string email, string password, string confirmation)
    {
        var normalizedName = name.Trim();
        var normalizedEmail = NormalizeEmail(email);

        if (normalizedName.Length < 2)
            return (false, null, "Enter a name with at least two characters.");
        if (!IsValidEmail(normalizedEmail))
            return (false, null, "Enter a valid email address.");
        if (password.Length < 8)
            return (false, null, "Use a password with at least 8 characters.");
        if (password != confirmation)
            return (false, null, "The passwords do not match.");

        try
        {
            using var scope = (App.Services ?? throw new InvalidOperationException("Application services are unavailable.")).CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();

            var accountExists = await db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
            if (accountExists)
                return (false, null, "An account already exists for that email. Sign in instead.");

            var user = new User
            {
                Name = normalizedName,
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                IsActive = 1,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                Role = "User"
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
            return (true, user, string.Empty);
        }
        catch (DbUpdateException)
        {
            return (false, null, "An account already exists for that email. Sign in instead.");
        }
        catch (Exception)
        {
            return (false, null, "We could not create your account. Please try again.");
        }
    }

    static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
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
        await DisplayAlertAsync("Password reset", "Password-reset email is not configured for this local app yet. Create another account or ask the device owner for help.", "OK");
    }
}
