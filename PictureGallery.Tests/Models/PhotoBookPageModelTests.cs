using FluentAssertions;
using PictureGallery.Core.Models;
using Xunit;

namespace PictureGallery.Tests.Models;

/// <summary>
/// Tests for PhotoBookPageModel
/// </summary>
public class PhotoBookPageModelTests
{
    [Fact]
    public void Photos_IsInitializedAsEmptyCollection()
    {
        // Arrange & Act
        var page = new PhotoBookPageModel();

        // Assert
        page.Photos.Should().NotBeNull();
        page.Photos.Should().BeEmpty();
    }

    [Fact]
    public void Photos_CanAddPhoto()
    {
        // Arrange
        var page = new PhotoBookPageModel();
        var photo = new PhotoItem { Id = 1, FileName = "test.jpg" };

        // Act
        page.Photos.Add(photo);

        // Assert
        page.Photos.Should().Contain(photo);
        page.Photos.Should().HaveCount(1);
    }

    [Fact]
    public void Photos_CanAddMultiplePhotos()
    {
        // Arrange
        var page = new PhotoBookPageModel();
        var photo1 = new PhotoItem { Id = 1, FileName = "test1.jpg" };
        var photo2 = new PhotoItem { Id = 2, FileName = "test2.jpg" };
        var photo3 = new PhotoItem { Id = 3, FileName = "test3.jpg" };

        // Act
        page.Photos.Add(photo1);
        page.Photos.Add(photo2);
        page.Photos.Add(photo3);

        // Assert
        page.Photos.Should().HaveCount(3);
        page.Photos.Should().Contain(photo1);
        page.Photos.Should().Contain(photo2);
        page.Photos.Should().Contain(photo3);
    }

    [Fact]
    public void Title_CanBeSetAndRetrieved()
    {
        // Arrange
        var page = new PhotoBookPageModel();
        const string testTitle = "Page 1";

        // Act
        page.Title = testTitle;

        // Assert
        page.Title.Should().Be(testTitle);
    }

    [Fact]
    public void Title_CanBeNull()
    {
        // Arrange
        var page = new PhotoBookPageModel
        {
            Title = "Initial Title"
        };

        // Act
        page.Title = null;

        // Assert
        page.Title.Should().BeNull();
    }

    [Fact]
    public void Title_DefaultValue_IsNull()
    {
        // Arrange & Act
        var page = new PhotoBookPageModel();

        // Assert
        page.Title.Should().BeNull();
    }
}

