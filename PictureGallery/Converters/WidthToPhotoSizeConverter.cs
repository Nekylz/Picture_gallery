using System.Globalization;
using Microsoft.Maui.Controls;

namespace PictureGallery.Converters;

public class WidthToPhotoSizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter is no longer needed with CollectionView GridItemsLayout
        // CollectionView automatically handles column width
        // Returning -1 lets the Image use the available space
        return -1;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

