using System.Collections;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace PictureGallery.Converters;

public class InverseCollectionCountToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable enumerable)
        {
            // Check if collection is empty
            foreach (var _ in enumerable)
            {
                return false; // Has items, so don't show empty state
            }
        }
        return true; // Empty or null, show empty state
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


