using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

/// <summary>
/// Single source of truth for the application's visual theme.
/// Pages should consume DynamicResource semantic keys instead of hard-coded
/// colors or AppThemeBinding color literals.
/// </summary>
public static class ThemeManager
{
    private const string ThemeVersionKey = "Theme_Version";
    private const int CurrentThemeVersion = 6;

    public static event EventHandler? ThemeChanged;

    public static void Initialize()
    {
        InitializeMissingPreferences();
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
        var surface = GetBackground(dark);

        var surfaceContainer = dark
            ? Blend(surface, Colors.White, 0.08f)
            : Blend(surface, Colors.White, 0.70f);

        var surfaceContainerHigh = dark
            ? Blend(surface, Colors.White, 0.14f)
            : Blend(surface, Colors.White, 0.48f);

        var surfaceContainerHighest = dark
            ? Blend(surface, Colors.White, 0.20f)
            : Blend(surface, Colors.White, 0.28f);

        var surfaceBright = dark
            ? Blend(surface, Colors.White, 0.12f)
            : Colors.White;

        var surfaceDim = dark
            ? Blend(surface, Colors.Black, 0.12f)
            : Blend(surface, Colors.Black, 0.04f);

        var onSurface = dark
            ? Color.FromArgb("#F5F5F5")
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

        var primaryContainer = WithAlpha(primary, dark ? 0.22f : 0.12f);
        var secondaryContainer = WithAlpha(secondary, dark ? 0.20f : 0.10f);

        // Core semantic resources.
        Set(resources, "Primary", primary);
        Set(resources, "PrimaryDark", primary);
        Set(resources, "PrimaryContainer", primaryContainer);
        Set(resources, "PrimaryContainerDark", primaryContainer);
        Set(resources, "OnPrimary", Colors.White);
        Set(resources, "OnPrimaryDark", Colors.White);
        Set(resources, "OnPrimaryContainer", dark ? Color.FromArgb("#F4F4F5") : Darken(primary, 0.55f));
        Set(resources, "OnPrimaryContainerDark", dark ? Color.FromArgb("#F4F4F5") : Darken(primary, 0.55f));

        Set(resources, "Secondary", secondary);
        Set(resources, "SecondaryDark", secondary);
        Set(resources, "SecondaryContainer", secondaryContainer);
        Set(resources, "SecondaryContainerDark", secondaryContainer);
        Set(resources, "OnSecondary", Colors.White);
        Set(resources, "OnSecondaryDark", Colors.White);
        Set(resources, "OnSecondaryContainer", dark ? Color.FromArgb("#F4F4F5") : Darken(secondary, 0.55f));
        Set(resources, "OnSecondaryContainerDark", dark ? Color.FromArgb("#F4F4F5") : Darken(secondary, 0.55f));

        // Semantic status colors intentionally stay independent of the user's accent.
        var success = dark ? Color.FromArgb("#34D399") : Color.FromArgb("#059669");
        var warning = dark ? Color.FromArgb("#FBBF24") : Color.FromArgb("#D97706");
        var error = dark ? Color.FromArgb("#F87171") : Color.FromArgb("#DC2626");
        var info = dark ? Color.FromArgb("#60A5FA") : Color.FromArgb("#2563EB");

        Set(resources, "Tertiary", warning);
        Set(resources, "TertiaryDark", warning);
        Set(resources, "TertiaryContainer", dark ? Color.FromArgb("#78350F") : Color.FromArgb("#FEF3C7"));
        Set(resources, "TertiaryContainerDark", Color.FromArgb("#78350F"));
        Set(resources, "OnTertiary", Colors.White);
        Set(resources, "OnTertiaryDark", Colors.White);
        Set(resources, "OnTertiaryContainer", dark ? Color.FromArgb("#FEF3C7") : Color.FromArgb("#78350F"));
        Set(resources, "OnTertiaryContainerDark", Color.FromArgb("#FEF3C7"));

        Set(resources, "Error", error);
        Set(resources, "ErrorDark", error);
        Set(resources, "ErrorContainer", dark ? Color.FromArgb("#7F1D1D") : Color.FromArgb("#FEE2E2"));
        Set(resources, "ErrorContainerDark", Color.FromArgb("#7F1D1D"));
        Set(resources, "OnError", Colors.White);
        Set(resources, "OnErrorDark", Colors.White);
        Set(resources, "OnErrorContainer", dark ? Colors.White : Color.FromArgb("#7F1D1D"));
        Set(resources, "OnErrorContainerDark", Colors.White);

        // Surfaces are derived from the selected background so the Background
        // setting actually affects cards and page surfaces too.
        Set(resources, "Surface", surface);
        Set(resources, "SurfaceDark", surface);
        Set(resources, "SurfaceDim", surfaceDim);
        Set(resources, "SurfaceDimDark", dark ? surfaceDim : Color.FromArgb("#141414"));
        Set(resources, "SurfaceBright", surfaceBright);
        Set(resources, "SurfaceBrightDark", surfaceBright);
        Set(resources, "SurfaceContainer", surfaceContainer);
        Set(resources, "SurfaceContainerDark", surfaceContainer);
        Set(resources, "SurfaceContainerHigh", surfaceContainerHigh);
        Set(resources, "SurfaceContainerHighDark", surfaceContainerHigh);
        Set(resources, "SurfaceContainerHighest", surfaceContainerHighest);
        Set(resources, "SurfaceContainerHighestDark", surfaceContainerHighest);
        Set(resources, "OnSurface", onSurface);
        Set(resources, "OnSurfaceDark", onSurface);
        Set(resources, "OnSurfaceVariant", onSurfaceVariant);
        Set(resources, "OnSurfaceVariantDark", onSurfaceVariant);
        Set(resources, "Outline", outline);
        Set(resources, "OutlineDark", outline);
        Set(resources, "OutlineVariant", outlineVariant);
        Set(resources, "OutlineVariantDark", outlineVariant);

        // Application-level semantic aliases. These are the preferred keys for
        // pages going forward and make the design system independent of color names.
        Set(resources, "AppPrimary", primary);
        Set(resources, "AppAccent", secondary);
        Set(resources, "AppBackground", surface);
        Set(resources, "AppCard", surfaceContainer);
        Set(resources, "AppCardElevated", surfaceBright);
        Set(resources, "AppBorder", outlineVariant);
        Set(resources, "AppText", onSurface);
        Set(resources, "AppTextSecondary", onSurfaceVariant);
        Set(resources, "AppPrimarySoft", primaryContainer);
        Set(resources, "AppAccentSoft", secondaryContainer);
        Set(resources, "AppSuccess", success);
        Set(resources, "AppWarning", warning);
        Set(resources, "AppError", error);
        Set(resources, "AppInfo", info);

        // Short semantic aliases for XAML. Do not use color names such as
        // AccentGreen/AccentBlue for new UI because those cannot express a
        // user-selected theme correctly.
        Set(resources, "Background", surface);
        Set(resources, "CardBackground", surfaceContainer);
        Set(resources, "CardBackgroundElevated", surfaceBright);
        Set(resources, "Text", onSurface);
        Set(resources, "TextSecondarySemantic", onSurfaceVariant);
        Set(resources, "Border", outlineVariant);
        Set(resources, "Accent", secondary);
        Set(resources, "Success", success);
        Set(resources, "Warning", warning);
        Set(resources, "Info", info);

        // Compatibility aliases. Older pages now follow the selected theme
        // instead of introducing another fixed green/blue palette.
        Set(resources, "AccentGreen", primary);
        Set(resources, "AccentBlue", secondary);
        Set(resources, "TextPrimary", onSurface);
        Set(resources, "TextSecondary", onSurfaceVariant);
        Set(resources, "TextMuted", dark ? Color.FromArgb("#71717A") : Color.FromArgb("#71717A"));
        Set(resources, "BgDark", surface);
        Set(resources, "CardDark", surfaceContainer);
        Set(resources, "BorderDark", outlineVariant);

        // Status aliases remain semantic; an online sensor is still green even
        // when the application's branding is indigo or blue.
        Set(resources, "StatusOnline", success);
        Set(resources, "StatusOffline", Color.FromArgb(dark ? "#A1A1AA" : "#64748B"));
        Set(resources, "StatusWarning", warning);
        Set(resources, "StatusCritical", error);
        Set(resources, "StatusUnknown", Color.FromArgb(dark ? "#A1A1AA" : "#94A3B8"));

        // Brushes mirror the color resources and are recreated on every Apply
        // so code that consumes a SolidColorBrush never retains the old theme.
        SetBrush(resources, "PrimaryBrush", primary);
        SetBrush(resources, "PrimaryContainerBrush", primaryContainer);
        SetBrush(resources, "PrimaryDarkBrush", primary);
        SetBrush(resources, "PrimaryContainerDarkBrush", primaryContainer);
        SetBrush(resources, "SecondaryBrush", secondary);
        SetBrush(resources, "SecondaryContainerBrush", secondaryContainer);
        SetBrush(resources, "SecondaryDarkBrush", secondary);
        SetBrush(resources, "SurfaceBrush", surface);
        SetBrush(resources, "SurfaceDarkBrush", surface);
        SetBrush(resources, "SurfaceContainerBrush", surfaceContainer);
        SetBrush(resources, "SurfaceContainerDarkBrush", surfaceContainer);
        SetBrush(resources, "SurfaceContainerHighBrush", surfaceContainerHigh);
        SetBrush(resources, "OnSurfaceBrush", onSurface);
        SetBrush(resources, "OnSurfaceDarkBrush", onSurface);
        SetBrush(resources, "OnSurfaceVariantBrush", onSurfaceVariant);
        SetBrush(resources, "OnSurfaceVariantDarkBrush", onSurfaceVariant);
        SetBrush(resources, "OutlineBrush", outline);
        SetBrush(resources, "OutlineDarkBrush", outline);
        SetBrush(resources, "OutlineVariantBrush", outlineVariant);
        SetBrush(resources, "ErrorBrush", error);
        SetBrush(resources, "ErrorDarkBrush", error);
        SetBrush(resources, "AccentGreenBrush", primary);
        SetBrush(resources, "AccentBlueBrush", secondary);
        SetBrush(resources, "TextPrimaryBrush", onSurface);
        SetBrush(resources, "TextSecondaryBrush", onSurfaceVariant);
        SetBrush(resources, "TextMutedBrush", Color.FromArgb("#71717A"));
        SetBrush(resources, "StatusOnlineBrush", success);
        SetBrush(resources, "StatusOfflineBrush", Color.FromArgb(dark ? "#A1A1AA" : "#64748B"));
        SetBrush(resources, "StatusWarningBrush", warning);
        SetBrush(resources, "StatusCriticalBrush", error);
        SetBrush(resources, "AppPrimaryBrush", primary);
        SetBrush(resources, "AppAccentBrush", secondary);
        SetBrush(resources, "AppBackgroundBrush", surface);
        SetBrush(resources, "AppCardBrush", surfaceContainer);
        SetBrush(resources, "AppPrimarySoftBrush", primaryContainer);
        SetBrush(resources, "AppAccentSoftBrush", secondaryContainer);
        SetBrush(resources, "AppBorderBrush", outlineVariant);
        SetBrush(resources, "AppTextBrush", onSurface);
        SetBrush(resources, "AppTextSecondaryBrush", onSurfaceVariant);
        SetBrush(resources, "AppSuccessBrush", success);
        SetBrush(resources, "AppWarningBrush", warning);
        SetBrush(resources, "AppErrorBrush", error);
        SetBrush(resources, "AppInfoBrush", info);

        Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static Color GetPrimary(bool dark)
    {
        var index = Math.Clamp(Preferences.Get("Theme_PrimaryIndex", 0), 0, 7);
        var themes = new[]
        {
            (Light: "#059669", Dark: "#34D399"),
            (Light: "#0284C7", Dark: "#38BDF8"),
            (Light: "#EA580C", Dark: "#FB923C"),
            (Light: "#7C3AED", Dark: "#A78BFA"),
            (Light: "#E11D48", Dark: "#FB7185"),
            (Light: "#0D9488", Dark: "#2DD4BF"),
            (Light: "#4F46E5", Dark: "#818CF8"),
            (Light: "#10B981", Dark: "#34D399")
        };

        return Color.FromArgb(dark ? themes[index].Dark : themes[index].Light);
    }

    private static Color GetSecondary(bool dark)
    {
        var index = Math.Clamp(Preferences.Get("Theme_AccentIndex", 0), 0, 7);
        var themes = new[]
        {
            (Light: "#2563EB", Dark: "#60A5FA"),
            (Light: "#059669", Dark: "#34D399"),
            (Light: "#D97706", Dark: "#FBBF24"),
            (Light: "#DC2626", Dark: "#F87171"),
            (Light: "#7C3AED", Dark: "#A78BFA"),
            (Light: "#0891B2", Dark: "#67E8F9"),
            (Light: "#65A30D", Dark: "#A3E635"),
            (Light: "#DB2777", Dark: "#F472B6")
        };

        return Color.FromArgb(dark ? themes[index].Dark : themes[index].Light);
    }

    private static Color GetBackground(bool dark)
    {
        var index = Math.Clamp(Preferences.Get("Theme_BackgroundIndex", 0), 0, 7);

        if (dark)
        {
            return index switch
            {
                1 => Color.FromArgb("#151618"),
                2 => Color.FromArgb("#171719"),
                3 => Color.FromArgb("#15201B"),
                4 => Color.FromArgb("#151B22"),
                5 => Color.FromArgb("#18191B"),
                6 => Color.FromArgb("#171A20"),
                7 => Color.FromArgb("#16181C"),
                _ => Color.FromArgb("#18181B")
            };
        }

        return index switch
        {
            1 => Color.FromArgb("#FFFFFF"),
            2 => Color.FromArgb("#FCFAF7"),
            3 => Color.FromArgb("#F5FAF7"),
            4 => Color.FromArgb("#F6F9FD"),
            5 => Color.FromArgb("#F4F5F6"),
            6 => Color.FromArgb("#F3F6FA"),
            7 => Color.FromArgb("#F7F8FB"),
            _ => Color.FromArgb("#F6F7F8")
        };
    }

    private static void InitializeMissingPreferences()
    {
        if (!Preferences.ContainsKey("Theme_PrimaryIndex"))
            Preferences.Set("Theme_PrimaryIndex", 0);
        if (!Preferences.ContainsKey("Theme_AccentIndex"))
            Preferences.Set("Theme_AccentIndex", 0);
        if (!Preferences.ContainsKey("Theme_BackgroundIndex"))
            Preferences.Set("Theme_BackgroundIndex", 0);
        if (!Preferences.ContainsKey("Theme_DarkMode"))
            Preferences.Set("Theme_DarkMode", false);

        Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
    }

    private static void Set(ResourceDictionary resources, string key, object value)
        => resources[key] = value;

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
        => resources[key] = new SolidColorBrush(color);

    private static Color WithAlpha(Color color, float alpha)
        => color.WithAlpha(alpha);

    private static Color Darken(Color color, float amount)
        => Blend(color, Colors.Black, Math.Clamp(amount, 0f, 1f));

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
