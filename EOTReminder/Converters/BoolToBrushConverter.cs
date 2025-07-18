
// Converters/BoolToBrushConverter.cs

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EOTReminder.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public SolidColorBrush HighlightBrush { get; set; } = new SolidColorBrush(Color.FromRgb(255, 215, 0)); // Gold
        public SolidColorBrush NormalBrush { get; set; } = new SolidColorBrush(Color.FromRgb(153, 153, 153)); // #999

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? HighlightBrush : NormalBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
