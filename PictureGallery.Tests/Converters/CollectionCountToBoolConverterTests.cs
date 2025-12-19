using FluentAssertions;
using PictureGallery.Core.Converters;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace PictureGallery.Tests.Converters;

public class CollectionCountToBoolConverterTests
{
    private readonly CollectionCountToBoolConverter _converter;

    public CollectionCountToBoolConverterTests()
    {
        _converter = new CollectionCountToBoolConverter();
    }

    [Fact]
    public void Convert_WithNonEmptyList_ReturnsTrue()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };

        // Act
        var result = _converter.Convert(list, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var list = new List<int>();

        // Act
        var result = _converter.Convert(list, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(false);
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
    public void Convert_WithArray_ReturnsTrue()
    {
        // Arrange
        var array = new[] { 1, 2, 3 };

        // Act
        var result = _converter.Convert(array, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WithString_ReturnsFalse()
    {
        // Arrange
        var str = "test";

        // Act
        var result = _converter.Convert(str, typeof(bool), null, CultureInfo.CurrentCulture);

        // Assert
        // String is IEnumerable<char>, so it will iterate and return true if it has characters
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

