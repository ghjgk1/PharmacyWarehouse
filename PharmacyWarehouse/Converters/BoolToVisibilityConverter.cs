using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PharmacyWarehouse.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!(value is bool boolValue))
        {
            return Visibility.Collapsed;
        }

        // Проверка на инверсию
        bool invert = parameter != null && 
                     (parameter.ToString().Equals("Invert", StringComparison.OrdinalIgnoreCase) || 
                      parameter.ToString().Equals("invert", StringComparison.OrdinalIgnoreCase));
        
        if (invert)
        {
            boolValue = !boolValue;
        }

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool result = visibility == Visibility.Visible;
            bool invert = parameter != null && 
                         (parameter.ToString().Equals("Invert", StringComparison.OrdinalIgnoreCase) || 
                          parameter.ToString().Equals("invert", StringComparison.OrdinalIgnoreCase));
            
            if (invert)
            {
                result = !result;
            }
            
            return result;
        }
        return DependencyProperty.UnsetValue;
    }
}
