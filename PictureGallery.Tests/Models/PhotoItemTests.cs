using FluentAssertions;
using PictureGallery.Core.Models;
using PictureGallery.Tests.Helpers;
using System;
using System.Collections.ObjectModel;
using System.IO;
using Xunit;

namespace PictureGallery.Tests.Models;

/// <summary>
/// Tests voor PhotoItem model uit PictureGallery.Core
/// </summary>
public class PhotoItemTests : TestBase
{
    [Fact]
    public void FileExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var imagePath = TestHelpers.CreateTestImageFile(TestDataDirectory, "test.jpg");
        var photo = new PhotoItem
        {
            Id = 1,
            FileName = "test.jpg",
            FilePath = imagePath,
            Width = 100,
            Height = 100
        };

        // Act
        var result = photo.FileExists;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void FileExists_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var photo = new PhotoItem
        {
            Id = 1,
            FileName = "nonexistent.jpg",
            FilePath = Path.Combine(TestDataDirectory, "nonexistent.jpg"),
            Width = 100,
            Height = 100
        };

        // Act
        var result = photo.FileExists;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FileExists_WithEmptyFilePath_ReturnsFalse()
    {
        // Arrange
        var photo = new PhotoItem
        {
            Id = 1,
            FileName = "test.jpg",
            FilePath = string.Empty,
            Width = 100,
            Height = 100
        };

        // Act
        var result = photo.FileExists;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithValidPhoto_ReturnsTrue()
    {
        // Arrange
        var photo = new PhotoItem
        {
            Id = 1,
            FileName = "test.jpg",
            FilePath = "/path/to/test.jpg",
            Width = 1920,
            Height = 1080,
            FileSizeMb = 2.5
        };

        // Act
        var result = photo.IsValid;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithEmptyFileName_ReturnsFalse()
    {
        // Arrange
        var photo = new PhotoItem
        {
            Id = 1,
            FileName = string.Empty,
            FilePath = "/path/to/test.jpg",
            Width = 1920,
            Height = 1080
        };

        // Act
        var result = photo.IsValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithZeroWidth_ReturnsFalse()
    {
        // Arrange
        var photo = new PhotoItem
        {
            Id = 1,
            FileName = "test.jpg",
            FilePath = "/path/to/test.jpg",
            Width = 0,
            Height = 1080
        };

        // Act
        var result = photo.IsValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DimensionsText_WithValidDimensions_ReturnsFormattedString()
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
    }

    [Fact]
    public void FileSizeText_WithValidSize_ReturnsFormattedString()
    {
        // Arrange
        var photo = new PhotoItem
        {
            FileSizeMb = 2.5
        };

        // Act
        var result = photo.FileSizeText;

        // Assert
        // Formatting kan locale-specifiek zijn (2.5 vs 2,5), dus test alleen dat het patroon klopt
        result.Should().StartWith("File Size: ");
        result.Should().EndWith(" MB");
        result.Should().Contain("2"); // Minimaal het hele getal
    }

    [Fact]
    public void CreatedDateDisplay_WithValidDate_ReturnsFormattedString()
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
    public void HasLabels_WithNoLabels_ReturnsFalse()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        var result = photo.HasLabels;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasLabels_WithLabels_ReturnsTrue()
    {
        // Arrange
        var photo = new PhotoItem();
        photo.Labels.Add("Test Label");

        // Act
        var result = photo.HasLabels;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSelected_SettingValue_RaisesPropertyChanged()
    {
        // Arrange
        var photo = new PhotoItem();
        string? changedPropertyName = null;
        photo.PropertyChanged += (sender, e) => changedPropertyName = e.PropertyName;

        // Act
        photo.IsSelected = true;

        // Assert
        photo.IsSelected.Should().BeTrue();
        changedPropertyName.Should().Be(nameof(PhotoItem.IsSelected));
    }

    [Fact]
    public void ValidateImageFile_WithValidImageFile_ReturnsTrue()
    {
        // Arrange
        var imagePath = TestHelpers.CreateTestImageFile(TestDataDirectory, "test.png", 200, 150);
        var photo = new PhotoItem
        {
            Id = 1,
            FileName = "test.png",
            FilePath = imagePath,
            Width = 200,
            Height = 150
        };

        // Act
        var result = photo.ValidateImageFile();

        // Assert
        result.Should().BeTrue();
        photo.FileExists.Should().BeTrue();
    }

    [Fact]
    public void ValidateImageFile_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var photo = new PhotoItem
        {
            Id = 1,
            FileName = "nonexistent.jpg",
            FilePath = Path.Combine(TestDataDirectory, "nonexistent.jpg")
        };

        // Act
        var result = photo.ValidateImageFile();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateImageFile_WithEmptyFile_ReturnsFalse()
    {
        // Arrange
        var emptyFilePath = TestHelpers.CreateEmptyTestFile(TestDataDirectory, "empty.jpg");
        var photo = new PhotoItem
        {
            Id = 1,
            FileName = "empty.jpg",
            FilePath = emptyFilePath
        };

        // Act
        var result = photo.ValidateImageFile();

        // Assert
        result.Should().BeFalse();
        File.Exists(emptyFilePath).Should().BeTrue();
        new FileInfo(emptyFilePath).Length.Should().Be(0);
    }

    [Fact]
    public void Labels_IsObservableCollection()
    {
        // Arrange & Act
        var photo = new PhotoItem();

        // Assert
        photo.Labels.Should().BeOfType<ObservableCollection<string>>();
        photo.Labels.Should().BeEmpty();
    }

    [Fact]
    public void Labels_AddingLabel_UpdatesCollection()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Labels.Add("Nature");
        photo.Labels.Add("Outdoor");

        // Assert
        photo.Labels.Should().HaveCount(2);
        photo.Labels.Should().Contain("Nature");
        photo.Labels.Should().Contain("Outdoor");
        photo.HasLabels.Should().BeTrue();
    }

    // Helper tests blijven ook
    [Fact]
    public void TestHelpers_CreateTestImageFile_CreatesValidImage()
    {
        // Arrange & Act
        var imagePath = TestHelpers.CreateTestImageFile(TestDataDirectory, "test.png", 200, 150);

        // Assert
        File.Exists(imagePath).Should().BeTrue();
        var fileInfo = new FileInfo(imagePath);
        fileInfo.Length.Should().BeGreaterThan(0);

        // Verify it's a valid image using SkiaSharp
        using (var stream = File.OpenRead(imagePath))
        {
            var bitmap = SkiaSharp.SKBitmap.Decode(stream);
            bitmap.Should().NotBeNull();
            bitmap?.Width.Should().Be(200);
            bitmap?.Height.Should().Be(150);
            bitmap?.Dispose();
        }
    }

    [Fact]
    public void TestBase_CreatesTestDirectory()
    {
        // Arrange & Act
        // TestBase constructor creates TestDataDirectory

        // Assert
        Directory.Exists(TestDataDirectory).Should().BeTrue();
        TestDbPath.Should().Contain(TestDataDirectory);
    }

    [Fact]
    public void TestHelpers_CreateEmptyTestFile_CreatesEmptyFile()
    {
        // Arrange & Act
        var emptyPath = TestHelpers.CreateEmptyTestFile(TestDataDirectory, "empty.txt");

        // Assert
        File.Exists(emptyPath).Should().BeTrue();
        new FileInfo(emptyPath).Length.Should().Be(0);
    }
}
