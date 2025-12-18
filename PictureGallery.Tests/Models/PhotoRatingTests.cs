using FluentAssertions;
using PictureGallery.Core.Models;
using Xunit;

namespace PictureGallery.Tests.Models;

/// <summary>
/// Tests voor rating functionaliteit op PhotoItem
/// </summary>
public class PhotoRatingTests
{
    [Fact]
    public void Rating_DefaultValue_IsZero()
    {
        // Arrange & Act
        var photo = new PhotoItem();

        // Assert
        photo.Rating.Should().Be(0);
    }

    [Fact]
    public void Rating_CanSetValidRating()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Rating = 5;

        // Assert
        photo.Rating.Should().Be(5);
    }

    [Fact]
    public void Rating_CanSetMinimumRating()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Rating = 1;

        // Assert
        photo.Rating.Should().Be(1);
    }

    [Fact]
    public void Rating_CanSetMaximumRating()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Rating = 5;

        // Assert
        photo.Rating.Should().Be(5);
    }

    [Fact]
    public void Rating_CanResetToZero()
    {
        // Arrange
        var photo = new PhotoItem { Rating = 5 };

        // Act
        photo.Rating = 0;

        // Assert
        photo.Rating.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Rating_CanSetAllValidRatings(int rating)
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Rating = rating;

        // Assert
        photo.Rating.Should().Be(rating);
    }

    [Fact]
    public void Rating_CanUpdateRating()
    {
        // Arrange
        var photo = new PhotoItem { Rating = 3 };

        // Act
        photo.Rating = 4;

        // Assert
        photo.Rating.Should().Be(4);
    }

    [Fact]
    public void Rating_SettingSameRating_DoesNotChange()
    {
        // Arrange
        var photo = new PhotoItem { Rating = 3 };

        // Act
        photo.Rating = 3;

        // Assert
        photo.Rating.Should().Be(3);
    }
}

