using FluentAssertions;
using Microsoft.Maui.Graphics;
using PictureGallery.Core.Converters;
using System.Globalization;
using Xunit;

namespace PictureGallery.Tests.Converters;

public class SelectionToBorderColorConverterTests
{
    private readonly SelectionToBorderColorConverter _converter;

    public SelectionToBorderColorConverterTests()
    {
        _converter = new SelectionToBorderColorConverter();
    }

    [Fact]
    public void Convert_WithTrue_ReturnsBlue()
    {
        // Act
        var result = _converter.Convert(true, typeof(Color), null, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#007AFF"); // Blue
    }

    [Fact]
    public void Convert_WithFalse_ReturnsGray()
    {
        // Act
        var result = _converter.Convert(false, typeof(Color), null, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#E0E0E0"); // Gray
    }

    [Fact]
    public void Convert_WithNull_ReturnsGray()
    {
        // Act
        var result = _converter.Convert(null, typeof(Color), null, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#E0E0E0"); // Gray
    }

    [Fact]
    public void Convert_WithNonBool_ReturnsGray()
    {
        // Act
        var result = _converter.Convert(123, typeof(Color), null, CultureInfo.CurrentCulture) as Color;

        // Assert
        result.Should().NotBeNull();
        result!.ToArgbHex().Should().Be("#E0E0E0"); // Gray
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        _converter.Invoking(c => c.ConvertBack(Colors.Black, typeof(bool), null, CultureInfo.CurrentCulture))
            .Should().Throw<NotImplementedException>();
    }
}

