using FluentAssertions;
using PictureGallery.Core.Models;
using System;
using Xunit;

namespace PictureGallery.Tests.Models;

/// <summary>
/// Tests for photo metadata viewing functionality
/// </summary>
public class PhotoMetadataTests
{
    [Fact]
    public void DimensionsText_WithValidDimensions_ShowsCorrectFormat()
    {
        // Arrange
        var photo = new PhotoItem
        {
            Width = 1920,
            Height = 1080
        };

        // Act
        var result = photo.DimensionsText;

        // Assert
        result.Should().Be("Image Dimensions: 1920 x 1080");
        result.Should().Contain("1920");
        result.Should().Contain("1080");
    }

    [Fact]
    public void DimensionsText_WithDifferentDimensions_ShowsCorrectValues()
    {
        // Arrange
        var photo = new PhotoItem
        {
            Width = 2560,
            Height = 1440
        };

        // Act
        var result = photo.DimensionsText;

        // Assert
        result.Should().Contain("2560");
        result.Should().Contain("1440");
    }

    [Fact]
    public void FileSizeText_WithValidSize_ShowsCorrectFormat()
    {
        // Arrange
        var photo = new PhotoItem
        {
            FileSizeMb = 2.5
        };

        // Act
        var result = photo.FileSizeText;

        // Assert
        result.Should().StartWith("File Size: ");
        result.Should().EndWith(" MB");
        result.Should().Contain("2"); // Minimaal het hele getal
    }

    [Fact]
    public void FileSizeText_WithLargeFile_ShowsCorrectSize()
    {
        // Arrange
        var photo = new PhotoItem
        {
            FileSizeMb = 15.8
        };

        // Act
        var result = photo.FileSizeText;

        // Assert
        result.Should().StartWith("File Size: ");
        result.Should().EndWith(" MB");
        result.Should().Contain("15");
    }

    [Fact]
    public void CreatedDateDisplay_WithValidDate_ShowsFormattedDate()
    {
        // Arrange
        var testDate = new DateTime(2024, 12, 18, 14, 30, 0);
        var photo = new PhotoItem
        {
            CreatedDate = testDate
        };

        // Act
        var result = photo.CreatedDateDisplay;

        // Assert
        result.Should().Be("18 Dec 2024 14:30");
    }

    [Fact]
    public void CreatedDateDisplay_WithDifferentDate_ShowsCorrectFormat()
    {
        // Arrange
        var testDate = new DateTime(2024, 1, 5, 9, 15, 0);
        var photo = new PhotoItem
        {
            CreatedDate = testDate
        };

        // Act
        var result = photo.CreatedDateDisplay;

        // Assert
        result.Should().Be("05 Jan 2024 09:15");
    }

    [Fact]
    public void CreatedDateDisplay_ContainsDayMonthYear()
    {
        // Arrange
        var photo = new PhotoItem
        {
            CreatedDate = new DateTime(2024, 3, 15, 10, 30, 0)
        };

        // Act
        var result = photo.CreatedDateDisplay;

        // Assert
        result.Should().Contain("15"); // Day
        result.Should().Contain("Mar"); // Month
        result.Should().Contain("2024"); // Year
    }

    [Fact]
    public void CreatedDateDisplay_ContainsTime()
    {
        // Arrange
        var photo = new PhotoItem
        {
            CreatedDate = new DateTime(2024, 1, 1, 23, 59, 0)
        };

        // Act
        var result = photo.CreatedDateDisplay;

        // Assert
        result.Should().Contain("23:59");
    }

    [Fact]
    public void Metadata_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var testDate = new DateTime(2024, 6, 15, 12, 0, 0);
        var photo = new PhotoItem
        {
            FileName = "vacation.jpg",
            Width = 1920,
            Height = 1080,
            FileSizeMb = 3.5,
            CreatedDate = testDate,
            Rating = 4
        };

        // Act & Assert
        photo.FileName.Should().Be("vacation.jpg");
        photo.Width.Should().Be(1920);
        photo.Height.Should().Be(1080);
        photo.FileSizeMb.Should().Be(3.5);
        photo.CreatedDate.Should().Be(testDate);
        photo.Rating.Should().Be(4);
    }

    [Fact]
    public void Metadata_ComputedProperties_ShowCorrectValues()
    {
        // Arrange
        var photo = new PhotoItem
        {
            FileName = "test.jpg",
            Width = 1920,
            Height = 1080,
            FileSizeMb = 2.5,
            CreatedDate = new DateTime(2024, 12, 18, 14, 30, 0)
        };

        // Act & Assert
        photo.DimensionsText.Should().Be("Image Dimensions: 1920 x 1080");
        photo.FileSizeText.Should().StartWith("File Size: ");
        photo.CreatedDateDisplay.Should().Be("18 Dec 2024 14:30");
    }
}

