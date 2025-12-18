using FluentAssertions;
using PictureGallery.Core.Models;
using Xunit;

namespace PictureGallery.Tests.Models;

/// <summary>
/// Tests voor PhotoLabel model
/// </summary>
public class PhotoLabelTests
{
    [Fact]
    public void IsValid_WithValidLabel_ReturnsTrue()
    {
        // Arrange
        var label = new PhotoLabel
        {
            Id = 1,
            PhotoId = 100,
            LabelText = "Nature",
            CreatedDate = DateTime.Now
        };

        // Act
        var result = label.IsValid;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithZeroPhotoId_ReturnsFalse()
    {
        // Arrange
        var label = new PhotoLabel
        {
            Id = 1,
            PhotoId = 0,
            LabelText = "Nature"
        };

        // Act
        var result = label.IsValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithNegativePhotoId_ReturnsFalse()
    {
        // Arrange
        var label = new PhotoLabel
        {
            Id = 1,
            PhotoId = -1,
            LabelText = "Nature"
        };

        // Act
        var result = label.IsValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithEmptyLabelText_ReturnsFalse()
    {
        // Arrange
        var label = new PhotoLabel
        {
            Id = 1,
            PhotoId = 100,
            LabelText = string.Empty
        };

        // Act
        var result = label.IsValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithWhitespaceOnlyLabelText_ReturnsFalse()
    {
        // Arrange
        var label = new PhotoLabel
        {
            Id = 1,
            PhotoId = 100,
            LabelText = "   "
        };

        // Act
        var result = label.IsValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithNullLabelText_ReturnsFalse()
    {
        // Arrange
        var label = new PhotoLabel
        {
            Id = 1,
            PhotoId = 100,
            LabelText = null!
        };

        // Act
        var result = label.IsValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithLabelTextExceedingMaxLength_ReturnsFalse()
    {
        // Arrange
        var label = new PhotoLabel
        {
            Id = 1,
            PhotoId = 100,
            LabelText = new string('a', 101) // 101 characters (max is 100)
        };

        // Act
        var result = label.IsValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithLabelTextAtMaxLength_ReturnsTrue()
    {
        // Arrange
        var label = new PhotoLabel
        {
            Id = 1,
            PhotoId = 100,
            LabelText = new string('a', 100) // Exactly 100 characters
        };

        // Act
        var result = label.IsValid;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void TrimmedLabelText_WithWhitespace_ReturnsTrimmed()
    {
        // Arrange
        var label = new PhotoLabel
        {
            LabelText = "  Nature  "
        };

        // Act
        var result = label.TrimmedLabelText;

        // Assert
        result.Should().Be("Nature");
    }

    [Fact]
    public void TrimmedLabelText_WithNoWhitespace_ReturnsOriginal()
    {
        // Arrange
        var label = new PhotoLabel
        {
            LabelText = "Nature"
        };

        // Act
        var result = label.TrimmedLabelText;

        // Assert
        result.Should().Be("Nature");
    }

    [Fact]
    public void TrimmedLabelText_WithOnlyWhitespace_ReturnsEmpty()
    {
        // Arrange
        var label = new PhotoLabel
        {
            LabelText = "   "
        };

        // Act
        var result = label.TrimmedLabelText;

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void CreatedDate_DefaultValue_IsSetToNow()
    {
        // Arrange
        var beforeCreation = DateTime.Now;
        
        // Act
        var label = new PhotoLabel();
        var afterCreation = DateTime.Now;

        // Assert
        label.CreatedDate.Should().BeOnOrAfter(beforeCreation);
        label.CreatedDate.Should().BeOnOrBefore(afterCreation);
    }

    [Fact]
    public void LabelText_CanBeSetAndRetrieved()
    {
        // Arrange
        var label = new PhotoLabel();
        const string testLabel = "Outdoor Photography";

        // Act
        label.LabelText = testLabel;

        // Assert
        label.LabelText.Should().Be(testLabel);
    }

    [Fact]
    public void PhotoId_CanBeSetAndRetrieved()
    {
        // Arrange
        var label = new PhotoLabel();
        const int testPhotoId = 42;

        // Act
        label.PhotoId = testPhotoId;

        // Assert
        label.PhotoId.Should().Be(testPhotoId);
    }
}

