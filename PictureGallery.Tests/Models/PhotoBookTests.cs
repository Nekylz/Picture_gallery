using FluentAssertions;
using PictureGallery.Core.Models;
using System;
using System.ComponentModel;
using Xunit;

namespace PictureGallery.Tests.Models;

/// <summary>
/// Tests for PhotoBook model
/// </summary>
public class PhotoBookTests
{
    [Fact]
    public void TotalPhotos_WithNoPages_ReturnsZero()
    {
        // Arrange
        var photoBook = new PhotoBook();

        // Act
        var result = photoBook.TotalPhotos;

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void TotalPhotos_WithEmptyPages_ReturnsZero()
    {
        // Arrange
        var photoBook = new PhotoBook();
        photoBook.Pages.Add(new PhotoBookPageModel());

        // Act
        var result = photoBook.TotalPhotos;

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void TotalPhotos_WithOnePageAndPhotos_ReturnsCorrectCount()
    {
        // Arrange
        var photoBook = new PhotoBook();
        var page = new PhotoBookPageModel();
        page.Photos.Add(new PhotoItem { Id = 1 });
        page.Photos.Add(new PhotoItem { Id = 2 });
        page.Photos.Add(new PhotoItem { Id = 3 });
        photoBook.Pages.Add(page);

        // Act
        var result = photoBook.TotalPhotos;

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public void TotalPhotos_WithMultiplePages_ReturnsSumOfAllPhotos()
    {
        // Arrange
        var photoBook = new PhotoBook();
        
        var page1 = new PhotoBookPageModel();
        page1.Photos.Add(new PhotoItem { Id = 1 });
        page1.Photos.Add(new PhotoItem { Id = 2 });
        
        var page2 = new PhotoBookPageModel();
        page2.Photos.Add(new PhotoItem { Id = 3 });
        page2.Photos.Add(new PhotoItem { Id = 4 });
        page2.Photos.Add(new PhotoItem { Id = 5 });
        
        photoBook.Pages.Add(page1);
        photoBook.Pages.Add(page2);

        // Act
        var result = photoBook.TotalPhotos;

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void TotalSizeMb_WithNoPhotos_ReturnsZero()
    {
        // Arrange
        var photoBook = new PhotoBook();

        // Act
        var result = photoBook.TotalSizeMb;

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void TotalSizeMb_WithPhotos_ReturnsSumOfFileSizes()
    {
        // Arrange
        var photoBook = new PhotoBook();
        var page = new PhotoBookPageModel();
        page.Photos.Add(new PhotoItem { Id = 1, FileSizeMb = 2.5 });
        page.Photos.Add(new PhotoItem { Id = 2, FileSizeMb = 3.7 });
        page.Photos.Add(new PhotoItem { Id = 3, FileSizeMb = 1.2 });
        photoBook.Pages.Add(page);

        // Act
        var result = photoBook.TotalSizeMb;

        // Assert
        result.Should().BeApproximately(7.4, 0.01); // Allow for floating point precision
    }

    [Fact]
    public void TotalSizeMb_WithMultiplePages_ReturnsSumOfAllFileSizes()
    {
        // Arrange
        var photoBook = new PhotoBook();
        
        var page1 = new PhotoBookPageModel();
        page1.Photos.Add(new PhotoItem { Id = 1, FileSizeMb = 2.0 });
        page1.Photos.Add(new PhotoItem { Id = 2, FileSizeMb = 1.5 });
        
        var page2 = new PhotoBookPageModel();
        page2.Photos.Add(new PhotoItem { Id = 3, FileSizeMb = 3.0 });
        
        photoBook.Pages.Add(page1);
        photoBook.Pages.Add(page2);

        // Act
        var result = photoBook.TotalSizeMb;

        // Assert
        result.Should().BeApproximately(6.5, 0.01);
    }

    [Fact]
    public void HasDescription_WithDescription_ReturnsTrue()
    {
        // Arrange
        var photoBook = new PhotoBook
        {
            Description = "My vacation photos"
        };

        // Act
        var result = photoBook.HasDescription;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasDescription_WithoutDescription_ReturnsFalse()
    {
        // Arrange
        var photoBook = new PhotoBook
        {
            Description = null
        };

        // Act
        var result = photoBook.HasDescription;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasDescription_WithEmptyDescription_ReturnsFalse()
    {
        // Arrange
        var photoBook = new PhotoBook
        {
            Description = string.Empty
        };

        // Act
        var result = photoBook.HasDescription;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasDescription_WithWhitespaceOnlyDescription_ReturnsFalse()
    {
        // Arrange
        var photoBook = new PhotoBook
        {
            Description = "   "
        };

        // Act
        var result = photoBook.HasDescription;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShowPhotoCount_WithPhotos_ReturnsTrue()
    {
        // Arrange
        var photoBook = new PhotoBook();
        var page = new PhotoBookPageModel();
        page.Photos.Add(new PhotoItem { Id = 1 });
        photoBook.Pages.Add(page);

        // Act
        var result = photoBook.ShowPhotoCount;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShowPhotoCount_WithoutPhotos_ReturnsFalse()
    {
        // Arrange
        var photoBook = new PhotoBook();

        // Act
        var result = photoBook.ShowPhotoCount;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShowStorageSize_WithPhotos_ReturnsTrue()
    {
        // Arrange
        var photoBook = new PhotoBook();
        var page = new PhotoBookPageModel();
        page.Photos.Add(new PhotoItem { Id = 1, FileSizeMb = 1.5 });
        photoBook.Pages.Add(page);

        // Act
        var result = photoBook.ShowStorageSize;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShowStorageSize_WithoutPhotos_ReturnsFalse()
    {
        // Arrange
        var photoBook = new PhotoBook();

        // Act
        var result = photoBook.ShowStorageSize;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSelected_SettingValue_RaisesPropertyChanged()
    {
        // Arrange
        var photoBook = new PhotoBook();
        string? changedPropertyName = null;
        photoBook.PropertyChanged += (sender, e) => changedPropertyName = e.PropertyName;

        // Act
        photoBook.IsSelected = true;

        // Assert
        photoBook.IsSelected.Should().BeTrue();
        changedPropertyName.Should().Be(nameof(PhotoBook.IsSelected));
    }

    [Fact]
    public void IsSelected_SettingSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var photoBook = new PhotoBook { IsSelected = true };
        bool propertyChangedRaised = false;
        photoBook.PropertyChanged += (sender, e) => propertyChangedRaised = true;

        // Act
        photoBook.IsSelected = true;

        // Assert
        propertyChangedRaised.Should().BeFalse();
    }

    [Fact]
    public void CreatedDate_DefaultValue_IsSetToNow()
    {
        // Arrange
        var beforeCreation = DateTime.Now;

        // Act
        var photoBook = new PhotoBook();
        var afterCreation = DateTime.Now;

        // Assert
        photoBook.CreatedDate.Should().BeOnOrAfter(beforeCreation);
        photoBook.CreatedDate.Should().BeOnOrBefore(afterCreation);
    }

    [Fact]
    public void UpdatedDate_DefaultValue_IsSetToNow()
    {
        // Arrange
        var beforeCreation = DateTime.Now;

        // Act
        var photoBook = new PhotoBook();
        var afterCreation = DateTime.Now;

        // Assert
        photoBook.UpdatedDate.Should().BeOnOrAfter(beforeCreation);
        photoBook.UpdatedDate.Should().BeOnOrBefore(afterCreation);
    }

    [Fact]
    public void Name_CanBeSetAndRetrieved()
    {
        // Arrange
        var photoBook = new PhotoBook();
        const string testName = "My Photo Book";

        // Act
        photoBook.Name = testName;

        // Assert
        photoBook.Name.Should().Be(testName);
    }

    [Fact]
    public void Pages_IsInitializedAsEmptyCollection()
    {
        // Arrange & Act
        var photoBook = new PhotoBook();

        // Assert
        photoBook.Pages.Should().NotBeNull();
        photoBook.Pages.Should().BeEmpty();
    }
}

