namespace GLORIOUSSYSTEM.App.Converters;

public class InverseBoolConverter : Microsoft.Maui.Controls.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }
}