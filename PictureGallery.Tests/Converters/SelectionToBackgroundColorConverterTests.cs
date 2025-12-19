using FluentAssertions;
using Microsoft.Maui.Graphics;
using PictureGallery.Core.Converters;
using System.Globalization;
using Xunit;

namespace PictureGallery.Tests.Converters;

public class SelectionToBackgroundColorConverterTests
{
    private readonly SelectionToBackgroundColorConverter _converter;

    public SelectionToBackgroundColorConverterTests()
    {
        _converter = new SelectionToBackgroundColorConverter();
    }

    [Fact]
    public void Convert_WithTrue_ReturnsLightBlue()
    {
        // Act
        var result = _converter.Convert(true, typeof(Color), null, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#E3F2FD"); // Light blue
    }

    [Fact]
    public void Convert_WithFalse_ReturnsWhite()
    {
        // Act
        var result = _converter.Convert(false, typeof(Color), null, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(Colors.White);
    }

    [Fact]
    public void Convert_WithNull_ReturnsWhite()
    {
        // Act
        var result = _converter.Convert(null, typeof(Color), null, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(Colors.White);
    }

    [Fact]
    public void Convert_WithNonBool_ReturnsWhite()
    {
        // Act
        var result = _converter.Convert(123, typeof(Color), null, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(Colors.White);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        _converter.Invoking(c => c.ConvertBack(Colors.White, typeof(bool), null, CultureInfo.CurrentCulture))
            .Should().Throw<NotImplementedException>();
    }
}


