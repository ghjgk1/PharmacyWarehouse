using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PharmacyWarehouse.Converters;

public class StatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Pending" => "Ожидает",
            "Accepted" => "Принято",
            "Rejected" => "Отклонено",
            "Processing" => "Обрабатывается",
            "InCirculation" => "В обороте",
            "Shipped" => "Отгружен",
            "WrittenOff" => "Списан",
            "Withdrawn" => "Изъят",
            _ => value
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Pending" => Brushes.Orange,
            "Accepted" => Brushes.Green,
            "Rejected" => Brushes.Red,
            "Processing" => Brushes.Blue,
            "InCirculation" => Brushes.Green,
            "Shipped" => Brushes.Blue,
            "WrittenOff" => Brushes.Red,
            "Withdrawn" => Brushes.DarkRed,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
