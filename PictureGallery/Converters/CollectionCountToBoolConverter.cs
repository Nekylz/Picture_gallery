using System.Collections;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace PictureGallery.Converters;

public class CollectionCountToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable enumerable)
        {
            // Check if collection has any items
            foreach (var _ in enumerable)
            {
                return true; // Has items
            }
        }
        return false; // Empty or null
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


