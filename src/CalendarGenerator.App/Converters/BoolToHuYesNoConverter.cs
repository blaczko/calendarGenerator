using System.Globalization;
using System.Windows.Data;

namespace CalendarGenerator.App.Converters;

public sealed class BoolToHuYesNoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "igen" : "nem";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
