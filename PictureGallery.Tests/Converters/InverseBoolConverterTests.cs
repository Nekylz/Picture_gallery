using FluentAssertions;
using PictureGallery.Core.Converters;
using System.Globalization;
using Xunit;

namespace PictureGallery.Tests.Converters;

public class InverseBoolConverterTests
{
    private readonly InverseBoolConverter _converter;

    public InverseBoolConverterTests()
    {
        _converter = new InverseBoolConverter();
    }

    [Fact]
    public void Convert_WithTrue_ReturnsFalse()
    {
        // Act
        var result = _converter.Convert(true, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithFalse_ReturnsTrue()
    {
        // Act
        var result = _converter.Convert(false, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WithNull_ReturnsFalse()
    {
        // Act
        var result = _converter.Convert(null, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithNonBool_ReturnsFalse()
    {
        // Act
        var result = _converter.Convert(123, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void ConvertBack_WithTrue_ReturnsFalse()
    {
        // Act
        var result = _converter.ConvertBack(true, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void ConvertBack_WithFalse_ReturnsTrue()
    {
        // Act
        var result = _converter.ConvertBack(false, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void ConvertBack_WithNull_ReturnsFalse()
    {
        // Act
        var result = _converter.ConvertBack(null, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(false);
    }
}

