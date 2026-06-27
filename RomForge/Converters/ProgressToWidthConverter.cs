using System.Globalization;
using System.Windows.Data;

namespace RomForge.Converters;

public class ProgressToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is double t1 && values[1] is int p1)
            return t1 * (p1 / 100.0);

        if (values[0] is double t2 && values[1] is double p2)
            return t2 * (p2 / 100.0);

        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}