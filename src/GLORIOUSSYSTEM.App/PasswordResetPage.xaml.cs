using System.Net.Mail;

namespace GLORIOUSSYSTEM.App;

public partial class PasswordResetPage : ContentPage
{
    readonly ApiSensorService _api = new();
    bool _busy;

    public PasswordResetPage()
    {
        InitializeComponent();
        EmailEntry.Text = Preferences.Get("SavedEmail", string.Empty);
    }

    async void OnRequestCodeClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        var email = NormalizeEmail(EmailEntry.Text);
        if (!IsValidEmail(email))
        {
            ShowStatus("Enter the Gmail address used for your account.", isError: true);
            return;
        }

        SetBusy(true, "Sending...");
        try
        {
            await _api.RequestPasswordResetAsync(email);
            ResetFields.IsVisible = true;
            ShowStatus("If an active account exists for this address, a reset code has been sent. Check Gmail, including Spam.", isError: false);
            CodeEntry.Focus();
        }
        catch (HttpRequestException)
        {
            ShowStatus("The API could not be reached. Check the API address in Settings and try again.", isError: true);
        }
        catch (TaskCanceledException)
        {
            ShowStatus("The request timed out. Check your connection and try again.", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        catch (Exception)
        {
            ShowStatus("We could not send the reset code. Please try again.", isError: true);
        }
        finally
        {
            SetBusy(false, "Send reset code");
        }
    }

    async void OnConfirmClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        var email = NormalizeEmail(EmailEntry.Text);
        var code = CodeEntry.Text?.Trim() ?? string.Empty;
        var password = NewPasswordEntry.Text ?? string.Empty;
        var confirmation = ConfirmPasswordEntry.Text ?? string.Empty;

        if (!IsValidEmail(email))
        {
            ShowStatus("Enter the Gmail address used for your account.", isError: true);
            return;
        }
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            ShowStatus("Enter the six-digit code from Gmail.", isError: true);
            return;
        }
        if (password.Length < 8)
        {
            ShowStatus("Use a password with at least 8 characters.", isError: true);
            return;
        }
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            ShowStatus("The passwords do not match.", isError: true);
            return;
        }

        SetBusy(true, "Resetting...");
        try
        {
            await _api.ConfirmPasswordResetAsync(email, code, password);
            Preferences.Remove("SavedPassword");
            SecureStorage.Default.Remove(ApiSensorService.SessionTokenStorageKey);
            SecureStorage.Default.Remove(ApiSensorService.RefreshTokenStorageKey);
            await DisplayAlertAsync("Password reset", "Your password was changed. Sign in with the new password.", "OK");
            await Navigation.PopModalAsync();
        }
        catch (HttpRequestException)
        {
            ShowStatus("The API could not be reached. Check the API address in Settings and try again.", isError: true);
        }
        catch (TaskCanceledException)
        {
            ShowStatus("The request timed out. Check your connection and try again.", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        catch (Exception)
        {
            ShowStatus("We could not reset the password. Please try again.", isError: true);
        }
        finally
        {
            SetBusy(false, "Reset password");
        }
    }

    async void OnCancelClicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync();

    void SetBusy(bool busy, string buttonText)
    {
        _busy = busy;
        RequestCodeButton.IsEnabled = !busy;
        ConfirmButton.IsEnabled = !busy;
        RequestCodeButton.Text = buttonText == "Reset password" || buttonText == "Resetting..."
            ? "Send reset code"
            : buttonText;
        if (buttonText is "Reset password" or "Resetting...")
            ConfirmButton.Text = buttonText;
    }

    void ShowStatus(string message, bool isError)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = isError
            ? (Color)Application.Current!.Resources["Error"]
            : (Color)Application.Current!.Resources["OnSurfaceVariant"];
        StatusLabel.IsVisible = true;
    }

    static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

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
}
