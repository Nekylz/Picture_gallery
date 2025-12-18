using System.Globalization;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;

namespace PictureGallery.Core.Converters;

public class RatingToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int rating && parameter is string starIndexStr && int.TryParse(starIndexStr, out int starIndex))
        {
            // If rating >= starIndex, show gold/yellow, otherwise show gray
            if (rating >= starIndex)
            {
                return Color.FromArgb("#FFD700"); // Gold color
            }
            else
            {
                return Color.FromArgb("#CCCCCC"); // Gray color
            }
        }
        return Color.FromArgb("#CCCCCC"); // Default gray
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


