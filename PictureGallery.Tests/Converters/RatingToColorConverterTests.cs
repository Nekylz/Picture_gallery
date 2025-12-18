using FluentAssertions;
using Microsoft.Maui.Graphics;
using PictureGallery.Core.Converters;
using System.Globalization;
using Xunit;

namespace PictureGallery.Tests.Converters;

public class RatingToColorConverterTests
{
    private readonly RatingToColorConverter _converter;

    public RatingToColorConverterTests()
    {
        _converter = new RatingToColorConverter();
    }

    [Fact]
    public void Convert_WithRatingGreaterThanStarIndex_ReturnsGold()
    {
        // Arrange
        int rating = 4;
        string starIndex = "3";

        // Act
        var result = _converter.Convert(rating, typeof(Color), starIndex, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#FFD700"); // Gold
    }

    [Fact]
    public void Convert_WithRatingEqualToStarIndex_ReturnsGold()
    {
        // Arrange
        int rating = 3;
        string starIndex = "3";

        // Act
        var result = _converter.Convert(rating, typeof(Color), starIndex, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#FFD700"); // Gold
    }

    [Fact]
    public void Convert_WithRatingLessThanStarIndex_ReturnsGray()
    {
        // Arrange
        int rating = 2;
        string starIndex = "3";

        // Act
        var result = _converter.Convert(rating, typeof(Color), starIndex, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#CCCCCC"); // Gray
    }

    [Fact]
    public void Convert_WithInvalidParameter_ReturnsDefaultGray()
    {
        // Arrange
        int rating = 5;
        string invalidParameter = "not-a-number";

        // Act
        var result = _converter.Convert(rating, typeof(Color), invalidParameter, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#CCCCCC"); // Default gray
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsDefaultGray()
    {
        // Act
        var result = _converter.Convert(null, typeof(Color), "1", CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#CCCCCC"); // Default gray
    }

    [Fact]
    public void Convert_WithRatingZero_ReturnsGray()
    {
        // Arrange
        int rating = 0;
        string starIndex = "1";

        // Act
        var result = _converter.Convert(rating, typeof(Color), starIndex, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#CCCCCC"); // Gray
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        _converter.Invoking(c => c.ConvertBack(Colors.Black, typeof(int), null, CultureInfo.CurrentCulture))
            .Should().Throw<NotImplementedException>();
    }
}

