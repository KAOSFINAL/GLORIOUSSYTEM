namespace GLORIOUSSYSTEM.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Log out", "Are you sure you want to log out?", "Yes", "Cancel");
        if (confirm)
            await DisplayAlert("Log out", "Authentication isn't wired up yet — this is a placeholder.", "OK");
    }
}