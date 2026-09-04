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

    private async Task AnimateCurrentPageAsync()
    {
        if (_isAnimating || CurrentPage is null)
            return;

        _isAnimating = true;
        try
        {
            var page = CurrentPage;
            page.Opacity = 0;
            page.TranslationY = 14;
            await Task.WhenAll(
                ViewExtensions.FadeToAsync(page, 1, 240, Easing.CubicOut),
                ViewExtensions.TranslateToAsync(page, 0, 0, 300, Easing.CubicOut));
        }
        catch
        {
            // Navigation must remain functional if an animation cannot run.
        }
        finally
        {
            _isAnimating = false;
        }
    }
}
