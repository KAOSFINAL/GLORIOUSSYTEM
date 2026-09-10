using Microsoft.Maui.Devices;

namespace GLORIOUSSYSTEM.App;

/// <summary>
/// Gives desktop windows a persistent left navigation rail while keeping the
/// same navigation control as a compact bottom bar on phones and narrow windows.
/// </summary>
public sealed class AdaptivePageLayout : Grid
{
    const double DesktopBreakpoint = 900;
    bool? _usingDesktopLayout;

    public AdaptivePageLayout()
    {
        BackgroundColor = Colors.Transparent;
        SizeChanged += (_, _) => ApplyLayout();
        Loaded += (_, _) => ApplyLayout();
    }

    void ApplyLayout()
    {
        if (Width <= 0)
            return;

        var navigation = Children.OfType<AppSidebar>().FirstOrDefault();
        var content = Children.OfType<View>().FirstOrDefault(child => child != navigation);
        if (navigation == null || content == null)
            return;

        var useDesktop = DeviceInfo.Current.Idiom == DeviceIdiom.Desktop && Width >= DesktopBreakpoint;
        if (_usingDesktopLayout == useDesktop)
            return;

        _usingDesktopLayout = useDesktop;
        RowDefinitions.Clear();
        ColumnDefinitions.Clear();

        if (useDesktop)
        {
            RowDefinitions.Add(new RowDefinition(GridLength.Star));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            Grid.SetRow((BindableObject)navigation, 0);
            Grid.SetColumn((BindableObject)navigation, 0);
            Grid.SetRow((BindableObject)content, 0);
            Grid.SetColumn((BindableObject)content, 1);
        }
        else
        {
            RowDefinitions.Add(new RowDefinition(GridLength.Star));
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            Grid.SetRow((BindableObject)content, 0);
            Grid.SetColumn((BindableObject)content, 0);
            Grid.SetRow((BindableObject)navigation, 1);
            Grid.SetColumn((BindableObject)navigation, 0);
        }

        navigation.IsDesktopMode = useDesktop;
    }
}
