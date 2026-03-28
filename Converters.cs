using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace ShortcutManager
{
    /// <summary>
    /// Converts a boolean selection state to a background brush for the item highlight.
    /// </summary>
    public class SelectionToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isSelected = (bool)value;
            if (isSelected)
            {
                // Return the system standard accent-colored highlight brush
                return Microsoft.UI.Xaml.Application.Current.Resources["SystemControlHighlightListAccentLowBrush"] as Brush;
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean selection state to a border brush for the item highlight.
    /// </summary>
    public class SelectionToBorderBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isSelected = (bool)value;
            if (isSelected)
            {
                // Return the system standard accent brush for the border
                return Microsoft.UI.Xaml.Application.Current.Resources["SystemControlHighlightAccentBrush"] as Brush;
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
