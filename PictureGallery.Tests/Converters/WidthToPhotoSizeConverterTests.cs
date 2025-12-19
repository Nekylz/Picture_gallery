using FluentAssertions;
using PictureGallery.Core.Converters;
using System.Globalization;
using Xunit;

namespace PictureGallery.Tests.Converters;

public class WidthToPhotoSizeConverterTests
{
    private readonly WidthToPhotoSizeConverter _converter;

    public WidthToPhotoSizeConverterTests()
    {
        _converter = new WidthToPhotoSizeConverter();
    }

    [Fact]
    public void Convert_AlwaysReturnsMinusOne()
    {
        // Arrange
        var anyValue = 100;

        // Act
        var result = _converter.Convert(anyValue, typeof(double), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void Convert_WithNull_ReturnsMinusOne()
    {
        // Act
        var result = _converter.Convert(null, typeof(double), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void Convert_WithAnyParameter_ReturnsMinusOne()
    {
        // Arrange
        var parameter = "some-parameter";

        // Act
        var result = _converter.Convert(100, typeof(double), parameter, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        _converter.Invoking(c => c.ConvertBack(-1, typeof(double), null, CultureInfo.CurrentCulture))
            .Should().Throw<NotImplementedException>();
    }
}

