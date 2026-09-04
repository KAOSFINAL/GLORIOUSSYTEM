using Microsoft.Maui.Controls;

namespace GLORIOUSSYSTEM.App;

public partial class AppShell : Shell
{
    private bool _isAnimating;

    public AppShell()
    {
        InitializeComponent();
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        _ = AnimateCurrentPageAsync();
    }

    async Task AnimateCurrentPageAsync()
    {
        if (_isAnimating || CurrentPage is null)
            return;

        _isAnimating = true;
        try
        {
            var page = CurrentPage;
            page.Opacity = 0;
            page.TranslationY = 12;
            await Task.WhenAll(
                page.FadeTo(1, 240, Easing.CubicOut),
                page.TranslateTo(0, 300, Easing.CubicOut));
        }
        catch
        {
            // Navigation should never fail because an animation could not run.
        }
        finally
        {
            _isAnimating = false;
        }
    }
}
