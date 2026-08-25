using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

/// <summary>
/// Single source of truth for the application's visual theme.
/// Pages should use DynamicResource semantic keys instead of hard-coded colors.
/// </summary>
public static class ThemeManager
{
    private const string ThemeVersionKey = "Theme_Version";
    private const int CurrentThemeVersion = 2;

    public static void Initialize()
    {
        MigrateLegacyThemePreferences();
        Apply();
    }

    public static void Apply()
    {
        var app = Application.Current;
        if (app == null)
            return;

        var resources = app.Resources;
        var dark = Preferences.Get("Theme_DarkMode", false);
        app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;

        var primary = GetPrimary(dark);
        var secondary = GetSecondary(dark);
        var background = GetBackground(dark);

        var surface = background;
        var surfaceContainer = dark
            ? Color.FromArgb("#242426")
            : Blend(background, Colors.White, 0.72f);
        var surfaceContainerHigh = dark
            ? Color.FromArgb("#2E2E30")
            : Blend(background, Colors.White, 0.52f);
        var surfaceContainerHighest = dark
            ? Color.FromArgb("#3A3A3C")
            : Blend(background, Colors.White, 0.30f);

        var onSurface = dark
            ? Color.FromArgb("#F4F4F5")
            : Color.FromArgb("#18181B");
        var onSurfaceVariant = dark
            ? Color.FromArgb("#A1A1AA")
            : Color.FromArgb("#52525B");
        var outline = dark
            ? Color.FromArgb("#52525B")
            : Color.FromArgb("#D4D4D8");
        var outlineVariant = dark
            ? Color.FromArgb("#3F3F46")
            : Color.FromArgb("#E4E4E7");

        Set(resources, "Primary", primary);
        Set(resources, "PrimaryContainer", WithAlpha(primary, dark ? 0.22f : 0.12f));
        Set(resources, "OnPrimary", Colors.White);
        Set(resources, "OnPrimaryContainer", dark ? Color.FromArgb("#D1FAE5") : Color.FromArgb("#065F46"));

        Set(resources, "Secondary", secondary);
        Set(resources, "SecondaryContainer", WithAlpha(secondary, dark ? 0.22f : 0.12f));
        Set(resources, "OnSecondary", Colors.White);
        Set(resources, "OnSecondaryContainer", dark ? Colors.White : Color.FromArgb("#1E3A8A"));

        Set(resources, "Surface", surface);
        Set(resources, "SurfaceDim", dark ? Color.FromArgb("#141414") : Blend(surface, Colors.Black, 0.04f));
        Set(resources, "SurfaceBright", dark ? Color.FromArgb("#2C2C2E") : Colors.White);
        Set(resources, "SurfaceContainer", surfaceContainer);
        Set(resources, "SurfaceContainerHigh", surfaceContainerHigh);
        Set(resources, "SurfaceContainerHighest", surfaceContainerHighest);
        Set(resources, "OnSurface", onSurface);
        Set(resources, "OnSurfaceVariant", onSurfaceVariant);
        Set(resources, "Outline", outline);
        Set(resources, "OutlineVariant", outlineVariant);

        Set(resources, "Error", dark ? Color.FromArgb("#F87171") : Color.FromArgb("#DC2626"));
        Set(resources, "ErrorContainer", dark ? Color.FromArgb("#7F1D1D") : Color.FromArgb("#FEE2E2"));
        Set(resources, "OnError", Colors.White);
        Set(resources, "OnErrorContainer", dark ? Colors.White : Color.FromArgb("#7F1D1D"));

        // Keep legacy aliases synchronized for older pages.
        Set(resources, "AccentGreen", primary);
        Set(resources, "AccentBlue", secondary);
        Set(resources, "TextPrimary", onSurface);
        Set(resources, "TextSecondary", onSurfaceVariant);
        Set(resources, "TextMuted", dark ? Color.FromArgb("#71717A") : Color.FromArgb("#71717A"));
        Set(resources, "BorderDark", outlineVariant);

        SetBrush(resources, "PrimaryBrush", primary);
        SetBrush(resources, "PrimaryContainerBrush", WithAlpha(primary, dark ? 0.22f : 0.12f));
        SetBrush(resources, "SecondaryBrush", secondary);
        SetBrush(resources, "SecondaryContainerBrush", WithAlpha(secondary, dark ? 0.22f : 0.12f));
        SetBrush(resources, "SurfaceBrush", surface);
        SetBrush(resources, "SurfaceContainerBrush", surfaceContainer);
        SetBrush(resources, "SurfaceContainerHighBrush", surfaceContainerHigh);
        SetBrush(resources, "OnSurfaceBrush", onSurface);
        SetBrush(resources, "OnSurfaceVariantBrush", onSurfaceVariant);
        SetBrush(resources, "OutlineBrush", outline);
        SetBrush(resources, "OutlineVariantBrush", outlineVariant);
        SetBrush(resources, "AccentGreenBrush", primary);
        SetBrush(resources, "AccentBlueBrush", secondary);
        SetBrush(resources, "TextPrimaryBrush", onSurface);
        SetBrush(resources, "TextSecondaryBrush", onSurfaceVariant);
        SetBrush(resources, "TextMutedBrush", Color.FromArgb("#71717A"));
    }

    private static Color GetPrimary(bool dark)
    {
        var index = Preferences.Get("Theme_PrimaryIndex", 0);
        var themes = new[]
        {
            (Light: "#059669", Dark: "#34D399"),
            (Light: "#0284C7", Dark: "#38BDF8"),
            (Light: "#EA580C", Dark: "#FB923C"),
            (Light: "#7C3AED", Dark: "#A78BFA"),
            (Light: "#E11D48", Dark: "#F472B6"),
            (Light: "#0D9488", Dark: "#2DD4BF"),
            (Light: "#4F46E5", Dark: "#818CF8"),
            (Light: "#059669", Dark: "#34D399")
        };
        index = Math.Clamp(index, 0, themes.Length - 1);
        return Color.FromArgb(dark ? themes[index].Dark : themes[index].Light);
    }

    private static Color GetSecondary(bool dark)
    {
        var index = Preferences.Get("Theme_AccentIndex", 0);
        var themes = new[]
        {
            (Light: "#1D4ED8", Dark: "#60A5FA"),
            (Light: "#059669", Dark: "#34D399"),
            (Light: "#D97706", Dark: "#FBBF24"),
            (Light: "#DC2626", Dark: "#F87171"),
            (Light: "#7C3AED", Dark: "#A78BFA"),
            (Light: "#06B6D4", Dark: "#67E8F9"),
            (Light: "#65A30D", Dark: "#A3E635"),
            (Light: "#DB2777", Dark: "#F472B6")
        };
        index = Math.Clamp(index, 0, themes.Length - 1);
        return Color.FromArgb(dark ? themes[index].Dark : themes[index].Light);
    }

    private static Color GetBackground(bool dark)
    {
        var index = Preferences.Get("Theme_BackgroundIndex", 0);
        if (dark)
        {
            return index switch
            {
                6 => Color.FromArgb("#0F172A"),
                3 => Color.FromArgb("#064E3B"),
                4 => Color.FromArgb("#1E3A8A"),
                _ => Color.FromArgb("#18181B")
            };
        }

        return index switch
        {
            1 => Color.FromArgb("#FFFFFF"),
            2 => Color.FromArgb("#FCFAF7"),
            3 => Color.FromArgb("#F4FBF7"),
            4 => Color.FromArgb("#F5F9FF"),
            _ => Color.FromArgb("#F6F7F8")
        };
    }

    private static void MigrateLegacyThemePreferences()
    {
        var version = Preferences.Get(ThemeVersionKey, 0);
        if (version >= CurrentThemeVersion)
            return;

        // Older builds could leave a Rose/Pink theme selected unintentionally.
        // Start the unified design system from the known-safe defaults once.
        Preferences.Set("Theme_PrimaryIndex", 0);
        Preferences.Set("Theme_AccentIndex", 0);
        Preferences.Set("Theme_BackgroundIndex", 0);
        Preferences.Set("Theme_DarkMode", false);
        Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
    }

    private static void Set(ResourceDictionary resources, string key, object value)
        => resources[key] = value;

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
        => resources[key] = new SolidColorBrush(color);

    private static Color WithAlpha(Color color, float alpha)
        => color.WithAlpha(alpha);

    private static Color Blend(Color baseColor, Color overlay, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromRgba(
            baseColor.Red + (overlay.Red - baseColor.Red) * amount,
            baseColor.Green + (overlay.Green - baseColor.Green) * amount,
            baseColor.Blue + (overlay.Blue - baseColor.Blue) * amount,
            1f);
    }
}
