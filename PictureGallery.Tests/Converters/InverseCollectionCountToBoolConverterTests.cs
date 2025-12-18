using FluentAssertions;
using PictureGallery.Core.Converters;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace PictureGallery.Tests.Converters;

public class InverseCollectionCountToBoolConverterTests
{
    private readonly InverseCollectionCountToBoolConverter _converter;

    public InverseCollectionCountToBoolConverterTests()
    {
        _converter = new InverseCollectionCountToBoolConverter();
    }

    [Fact]
    public void Convert_WithNonEmptyList_ReturnsFalse()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };

        // Act
        var result = _converter.Convert(list, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithEmptyList_ReturnsTrue()
    {
        // Arrange
        var list = new List<int>();

        // Act
        var result = _converter.Convert(list, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WithNull_ReturnsTrue()
    {
        // Act
        var result = _converter.Convert(null, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        _converter.Invoking(c => c.ConvertBack(true, typeof(object), null, CultureInfo.CurrentCulture))
            .Should().Throw<NotImplementedException>();
    }
}

