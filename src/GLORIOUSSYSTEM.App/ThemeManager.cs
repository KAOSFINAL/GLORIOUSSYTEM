using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

/// <summary>
/// Single source of truth for the GLORIOUSSYSTEM visual theme.
/// The palette provides coordinated Material tonal options for action colors,
/// solar accents, and surfaces while keeping operational states easy to scan.
/// </summary>
public static class ThemeManager
{
    private const string ThemeVersionKey = "Theme_Version";
    private const int CurrentThemeVersion = 14;

    private sealed record TonalColor(
        string Light, string Dark,
        string LightContainer, string DarkContainer,
        string OnLight, string OnDark,
        string OnLightContainer, string OnDarkContainer);

    private sealed record SurfaceScheme(
        string LightSurface, string LightDim, string LightBright,
        string LightContainer, string LightContainerHigh, string LightContainerHighest,
        string LightOnSurface, string LightOnSurfaceVariant,
        string LightOutline, string LightOutlineVariant,
        string DarkSurface, string DarkDim, string DarkBright,
        string DarkContainer, string DarkContainerHigh, string DarkContainerHighest,
        string DarkOnSurface, string DarkOnSurfaceVariant,
        string DarkOutline, string DarkOutlineVariant);

    private static readonly TonalColor[] PrimaryOptions =
    {
        new("#176B3A", "#88DFA9", "#A7F3C8", "#00522A", "#FFFFFF", "#00391C", "#00210E", "#C4FAD8"),
        new("#006590", "#79CFFF", "#C6E7FF", "#004C6E", "#FFFFFF", "#00344D", "#001E2D", "#C6E7FF"),
        new("#506600", "#B7D86B", "#D4ED8C", "#3B4D00", "#FFFFFF", "#283500", "#171E00", "#D4ED8C"),
        new("#006A67", "#69D9D3", "#9CF2ED", "#00504E", "#FFFFFF", "#003735", "#00201F", "#9CF2ED"),
        new("#6750A4", "#CFBCFF", "#E9DDFF", "#4F378B", "#FFFFFF", "#381E72", "#22005D", "#E9DDFF"),
        new("#8C4A00", "#FFB77C", "#FFDCC2", "#6A3500", "#FFFFFF", "#4A2800", "#2E1500", "#FFDCC2"),
        new("#9B405F", "#FFB0C8", "#FFD9E2", "#7B2948", "#FFFFFF", "#5E1232", "#3F001D", "#FFD9E2"),
        new("#006B5F", "#54DBC7", "#76F8E2", "#005047", "#FFFFFF", "#003731", "#00201C", "#76F8E2"),
        new("#44529A", "#BEC2FF", "#DDE1FF", "#37448F", "#FFFFFF", "#202B60", "#0A194F", "#DDE1FF"),
        new("#005AC1", "#A9C7FF", "#D7E2FF", "#004494", "#FFFFFF", "#002F65", "#001B3F", "#D7E2FF"),
        new("#2E683B", "#95D5A0", "#B0F2B8", "#145026", "#FFFFFF", "#003915", "#00210A", "#B0F2B8"),
        new("#006C50", "#63DDB1", "#86F8CD", "#00513A", "#FFFFFF", "#003826", "#002116", "#86F8CD"),
        new("#8F3F76", "#FFAFDC", "#FFD8EB", "#71305D", "#FFFFFF", "#541442", "#3B002D", "#FFD8EB"),
        new("#A4343F", "#FFB3B8", "#FFDADC", "#84202A", "#FFFFFF", "#680E18", "#410008", "#FFDADC"),
        new("#98461A", "#FFB694", "#FFDBCA", "#783006", "#FFFFFF", "#552000", "#351000", "#FFDBCA"),
        new("#7A5900", "#F6C94D", "#FFE082", "#5B4300", "#FFFFFF", "#3F2E00", "#241A00", "#FFE082")
    };

    public static IReadOnlyList<string> PrimaryNames { get; } =
        new[]
        {
            "Emerald", "Sky", "Lime", "Cyan", "Violet", "Orange", "Rose", "Teal",
            "Indigo", "Cobalt", "Forest", "Mint", "Magenta", "Crimson", "Coral", "Amber"
        };

    private static readonly TonalColor[] AccentOptions =
    {
        new("#775A00", "#F2C94C", "#FFE082", "#5B4300", "#FFFFFF", "#3E2E00", "#241A00", "#FFE082"),
        PrimaryOptions[1], PrimaryOptions[4], PrimaryOptions[3],
        PrimaryOptions[2], PrimaryOptions[5], PrimaryOptions[6], PrimaryOptions[7],
        PrimaryOptions[8], PrimaryOptions[9], PrimaryOptions[10], PrimaryOptions[11],
        PrimaryOptions[12], PrimaryOptions[13], PrimaryOptions[14], PrimaryOptions[15]
    };

    public static IReadOnlyList<string> AccentNames { get; } =
        new[]
        {
            "Solar Gold", "Sky", "Violet", "Cyan", "Lime", "Orange", "Rose", "Teal",
            "Indigo", "Cobalt", "Forest", "Mint", "Magenta", "Crimson", "Coral", "Amber"
        };

    private static readonly SurfaceScheme[] BackgroundOptions =
    {
        // Neutral
        new("#F0F1F2", "#DDE0E2", "#FBFCFD", "#E9EBED", "#E3E6E8", "#DDE0E2", "#191C1E", "#42474A", "#697175", "#B8C0C4",
            "#141618", "#0D0F11", "#36383B", "#1C1F21", "#24272A", "#2E3235", "#E4E6E8", "#C5C8CA", "#8E9498", "#454B4F"),
        // Blue Gray
        new("#DFEDEE", "#D1DFE0", "#F1F8F8", "#D9E8E9", "#D3E3E4", "#CCDEDF", "#152127", "#354B55", "#647985", "#A7BBC5",
            "#0C2022", "#061416", "#314C4F", "#142B2E", "#1D383B", "#28464A", "#DCEBEC", "#B7CECF", "#849C9E", "#3C575A"),
        // Slate
        new("#E9E4F1", "#DFD9E9", "#F8F5FC", "#E4DFED", "#DED9E8", "#DDD7E7", "#201B28", "#484052", "#71667A", "#B5AABD",
            "#181324", "#100B19", "#43384F", "#211A2F", "#2B233A", "#372E47", "#E9E0F0", "#CDBFD7", "#998BA5", "#51465C"),
        // Warm Gray
        new("#F4EADF", "#E7D9CC", "#FFF9F3", "#EEE2D6", "#EADDD1", "#E6D9CD", "#241A14", "#4C3E37", "#7D6A5F", "#C5B2A5",
            "#1A120D", "#100B07", "#45352A", "#231914", "#2D211A", "#3A2B22", "#EEE1D6", "#D0BFB1", "#9A887A", "#514238"),
        // Soft Green
        new("#E5F2E6", "#D7E7D9", "#F6FCF7", "#E0EEE1", "#DAE9DC", "#D3E5D5", "#152019", "#334B3A", "#617A68", "#A8C0AD",
            "#0C1A10", "#061009", "#304839", "#14251A", "#1B3021", "#25402C", "#DEE9E0", "#BCCCC0", "#84998A", "#3C5443"),
        // Cool Blue
        new("#D8F0FA", "#CDE4EF", "#EFFAFF", "#D5EBF5", "#CEE6F1", "#C7E0ED", "#10212C", "#2F4B5A", "#5C7889", "#A0BAC9",
            "#061A28", "#03111A", "#2A4B62", "#0E2939", "#15374A", "#1D475D", "#D9E9F2", "#B4CFDE", "#7E9AAA", "#354F5F"),
        // Lavender
        new("#FBF7FF", "#E5DDEC", "#FFFFFF", "#F3ECF8", "#ECE5F1", "#E4DDE9", "#201A23", "#5F5763", "#897D8E", "#D2C5D8",
            "#171219", "#100C12", "#3D353F", "#201A22", "#281F2B", "#32283A", "#E9DFEA", "#CEBFCE", "#998A9A", "#4D424F"),
        // Sand
        new("#FFF9F0", "#E8DFCF", "#FFFFFF", "#F7F0E4", "#F0E8DB", "#E8DFD1", "#201B13", "#5D584E", "#888073", "#D4C8B7",
            "#17130D", "#100D09", "#3E382E", "#201B14", "#282219", "#322B21", "#EBE1D3", "#CFC5B6", "#998F81", "#4D473E"),
        // Blush
        new("#FFF7F8", "#EADDE0", "#FFFFFF", "#F8ECEE", "#F1E5E7", "#EADDE0", "#24191B", "#66565A", "#927D82", "#DBC4C9",
            "#191113", "#110B0D", "#413437", "#22191B", "#2B2023", "#36292C", "#F0DEE2", "#D4BFC4", "#9E8A8F", "#524347"),
        // Aqua
        new("#F2FAFA", "#D7E7E7", "#FFFFFF", "#E5F3F3", "#DEECEC", "#D4E5E5", "#132020", "#465F5F", "#708989", "#BDD2D2",
            "#0A1616", "#061010", "#303E3E", "#122020", "#182828", "#213333", "#D9E7E7", "#B9CCCC", "#849797", "#3C4F4F"),
        // Olive
        new("#F9FAF2", "#E3E5D5", "#FFFFFF", "#F0F2E7", "#E9EBDD", "#E1E4D3", "#1C1E15", "#585C4B", "#818573", "#CDD0BE",
            "#13150D", "#0D0F08", "#393C32", "#1C1E16", "#23261C", "#2D3025", "#E4E6DB", "#C6CAB8", "#919681", "#474B3D"),
        // Graphite
        new("#F3F4F6", "#D9DCE0", "#FFFFFF", "#E8EAED", "#E1E3E7", "#D8DBE0", "#181A1C", "#4B4E53", "#73767C", "#C1C4C9",
            "#0B0D10", "#07090B", "#303237", "#14161A", "#1B1D21", "#24262B", "#E3E3E8", "#C5C6CC", "#8F9097", "#42444A")
    };

    public static IReadOnlyList<string> BackgroundNames { get; } =
        new[]
        {
            "Neutral", "Blue Gray", "Slate", "Warm Gray", "Soft Green", "Cool Blue",
            "Lavender", "Sand", "Blush", "Aqua", "Olive", "Graphite"
        };

    public static event EventHandler? ThemeChanged;

    public static void Initialize()
    {
        var version = Preferences.Get(ThemeVersionKey, 0);
        if (version < CurrentThemeVersion)
        {
            if (!Preferences.ContainsKey("Theme_DarkMode"))
                Preferences.Set("Theme_DarkMode", true);
            if (!Preferences.ContainsKey("Theme_PrimaryIndex"))
                Preferences.Set("Theme_PrimaryIndex", 0);
            if (!Preferences.ContainsKey("Theme_AccentIndex"))
                Preferences.Set("Theme_AccentIndex", 0);
            if (!Preferences.ContainsKey("Theme_BackgroundIndex"))
                Preferences.Set("Theme_BackgroundIndex", 0);
            Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
        }

        Apply();
    }

    public static void Apply()
    {
        var app = Application.Current;
        if (app == null)
            return;

        var resources = app.Resources;
        var darkMode = Preferences.Get("Theme_DarkMode", true);
        var primaryIndex = Math.Clamp(Preferences.Get("Theme_PrimaryIndex", 0), 0, PrimaryOptions.Length - 1);
        var accentIndex = Math.Clamp(Preferences.Get("Theme_AccentIndex", 0), 0, AccentOptions.Length - 1);
        var backgroundIndex = Math.Clamp(Preferences.Get("Theme_BackgroundIndex", 0), 0, BackgroundOptions.Length - 1);
        var primaryTone = PrimaryOptions[primaryIndex];
        var secondaryTone = AccentOptions[accentIndex];
        var backgroundTone = BackgroundOptions[backgroundIndex];

        var primary = darkMode ? primaryTone.Dark : primaryTone.Light;
        var secondary = darkMode ? secondaryTone.Dark : secondaryTone.Light;
        var onPrimary = darkMode ? primaryTone.OnDark : primaryTone.OnLight;
        var onSecondary = darkMode ? secondaryTone.OnDark : secondaryTone.OnLight;
        var primaryContainer = darkMode ? primaryTone.DarkContainer : primaryTone.LightContainer;
        var secondaryContainer = darkMode ? secondaryTone.DarkContainer : secondaryTone.LightContainer;
        var onPrimaryContainer = darkMode ? primaryTone.OnDarkContainer : primaryTone.OnLightContainer;
        var onSecondaryContainer = darkMode ? secondaryTone.OnDarkContainer : secondaryTone.OnLightContainer;
        var tertiary = darkMode ? "#82D3FF" : "#00658A";
        var error = darkMode ? "#FFB4AB" : "#BA1A1A";
        var success = darkMode ? "#88DFA9" : "#176B3A";
        var warning = darkMode ? "#F2C94C" : "#765A00";

        var tertiaryContainer = darkMode ? "#123246" : "#DCEFFD";
        var errorContainer = darkMode ? "#93000A" : "#FFDAD6";
        var surface = darkMode ? backgroundTone.DarkSurface : backgroundTone.LightSurface;
        var surfaceDim = darkMode ? backgroundTone.DarkDim : backgroundTone.LightDim;
        var surfaceBright = darkMode ? backgroundTone.DarkBright : backgroundTone.LightBright;
        var surfaceContainer = darkMode ? backgroundTone.DarkContainer : backgroundTone.LightContainer;
        var surfaceContainerHigh = darkMode ? backgroundTone.DarkContainerHigh : backgroundTone.LightContainerHigh;
        var surfaceContainerHighest = darkMode ? backgroundTone.DarkContainerHighest : backgroundTone.LightContainerHighest;
        var onSurface = darkMode ? backgroundTone.DarkOnSurface : backgroundTone.LightOnSurface;
        var onSurfaceVariant = darkMode ? backgroundTone.DarkOnSurfaceVariant : backgroundTone.LightOnSurfaceVariant;
        var outline = darkMode ? backgroundTone.DarkOutline : backgroundTone.LightOutline;
        var outlineVariant = darkMode ? backgroundTone.DarkOutlineVariant : backgroundTone.LightOutlineVariant;
        var shadow = darkMode ? "#000000" : "#000000";

        app.UserAppTheme = darkMode ? AppTheme.Dark : AppTheme.Light;

        Set(resources, "Primary", primary);
        Set(resources, "PrimaryDark", primary);
        Set(resources, "PrimaryContainer", primaryContainer);
        Set(resources, "PrimaryContainerDark", primaryContainer);
        Set(resources, "OnPrimary", onPrimary);
        Set(resources, "OnPrimaryDark", onPrimary);
        Set(resources, "OnPrimaryContainer", onPrimaryContainer);
        Set(resources, "OnPrimaryContainerDark", onPrimaryContainer);

        Set(resources, "Secondary", secondary);
        Set(resources, "SecondaryDark", secondary);
        Set(resources, "SecondaryContainer", secondaryContainer);
        Set(resources, "SecondaryContainerDark", secondaryContainer);
        Set(resources, "OnSecondary", onSecondary);
        Set(resources, "OnSecondaryDark", onSecondary);
        Set(resources, "OnSecondaryContainer", onSecondaryContainer);
        Set(resources, "OnSecondaryContainerDark", onSecondaryContainer);

        Set(resources, "Tertiary", tertiary);
        Set(resources, "TertiaryDark", tertiary);
        Set(resources, "TertiaryContainer", tertiaryContainer);
        Set(resources, "TertiaryContainerDark", tertiaryContainer);
        Set(resources, "OnTertiary", darkMode ? "#00344A" : "#FFFFFF");
        Set(resources, "OnTertiaryDark", darkMode ? "#00344A" : "#FFFFFF");
        Set(resources, "OnTertiaryContainer", darkMode ? "#DCEFFD" : "#123246");
        Set(resources, "OnTertiaryContainerDark", darkMode ? "#DCEFFD" : "#123246");

        Set(resources, "Error", error);
        Set(resources, "ErrorDark", error);
        Set(resources, "ErrorContainer", errorContainer);
        Set(resources, "ErrorContainerDark", errorContainer);
        Set(resources, "OnError", darkMode ? "#690005" : "#FFFFFF");
        Set(resources, "OnErrorDark", darkMode ? "#690005" : "#FFFFFF");
        Set(resources, "OnErrorContainer", darkMode ? "#FFDAD6" : "#410002");
        Set(resources, "OnErrorContainerDark", darkMode ? "#FFDAD6" : "#410002");

        Set(resources, "Surface", surface);
        Set(resources, "SurfaceDark", surface);
        Set(resources, "SurfaceDim", surfaceDim);
        Set(resources, "SurfaceDimDark", surfaceDim);
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
        Set(resources, "Shadow", shadow);
        Set(resources, "Scrim", shadow);
        Set(resources, "InverseSurface", onSurface);
        Set(resources, "InverseOnSurface", surface);
        Set(resources, "InversePrimary", primary);

        // Semantic colors do not change with appearance customization.
        Set(resources, "StatusOnline", success);
        Set(resources, "StatusOffline", onSurfaceVariant);
        Set(resources, "StatusWarning", warning);
        Set(resources, "StatusCritical", error);
        Set(resources, "StatusUnknown", outline);
        Set(resources, "Success", success);
        Set(resources, "Warning", warning);
        Set(resources, "Info", tertiary);

        // Compatibility aliases used by older pages.
        Set(resources, "AccentGreen", primary);
        Set(resources, "AccentBlue", tertiary);
        Set(resources, "AccentAmber", secondary);
        Set(resources, "AccentRed", error);
        Set(resources, "TextPrimary", onSurface);
        Set(resources, "TextSecondary", onSurfaceVariant);
        Set(resources, "TextMuted", onSurfaceVariant);
        Set(resources, "BgDark", surface);
        Set(resources, "CardDark", surfaceContainer);
        Set(resources, "BorderDark", outlineVariant);

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
        SetBrush(resources, "AccentBlueBrush", tertiary);
        SetBrush(resources, "TextPrimaryBrush", onSurface);
        SetBrush(resources, "TextSecondaryBrush", onSurfaceVariant);
        SetBrush(resources, "TextMutedBrush", onSurfaceVariant);
        SetBrush(resources, "StatusOnlineBrush", success);
        SetBrush(resources, "StatusOfflineBrush", onSurfaceVariant);
        SetBrush(resources, "StatusWarningBrush", warning);
        SetBrush(resources, "StatusCriticalBrush", error);

        Set(resources, "Background", surface);
        Set(resources, "CardBackground", surfaceContainer);
        Set(resources, "CardBackgroundElevated", surfaceBright);
        Set(resources, "Text", onSurface);
        Set(resources, "TextSecondarySemantic", onSurfaceVariant);
        Set(resources, "Border", outlineVariant);
        Set(resources, "Accent", secondary);
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
        Set(resources, "AppInfo", tertiary);

        Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void Set(ResourceDictionary resources, string key, string value)
        => resources[key] = Color.FromArgb(value);

    private static void SetBrush(ResourceDictionary resources, string key, string value)
        => resources[key] = new SolidColorBrush(Color.FromArgb(value));
}
