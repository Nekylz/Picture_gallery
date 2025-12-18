using FluentAssertions;
using PictureGallery.Core.Models;
using System.Collections.ObjectModel;
using Xunit;

namespace PictureGallery.Tests.Models;

/// <summary>
/// Tests voor label operations op PhotoItem
/// </summary>
public class PhotoLabelOperationsTests
{
    [Fact]
    public void Labels_AddingLabel_AddsToCollection()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Labels.Add("Nature");

        // Assert
        photo.Labels.Should().Contain("Nature");
        photo.Labels.Should().HaveCount(1);
        photo.HasLabels.Should().BeTrue();
    }

    [Fact]
    public void Labels_AddingMultipleLabels_AddsAll()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Labels.Add("Nature");
        photo.Labels.Add("Outdoor");
        photo.Labels.Add("Vacation");

        // Assert
        photo.Labels.Should().HaveCount(3);
        photo.Labels.Should().Contain("Nature");
        photo.Labels.Should().Contain("Outdoor");
        photo.Labels.Should().Contain("Vacation");
    }

    [Fact]
    public void Labels_RemovingLabel_RemovesFromCollection()
    {
        // Arrange
        var photo = new PhotoItem();
        photo.Labels.Add("Nature");
        photo.Labels.Add("Outdoor");

        // Act
        photo.Labels.Remove("Nature");

        // Assert
        photo.Labels.Should().NotContain("Nature");
        photo.Labels.Should().Contain("Outdoor");
        photo.Labels.Should().HaveCount(1);
    }

    [Fact]
    public void Labels_RemovingAllLabels_CollectionIsEmpty()
    {
        // Arrange
        var photo = new PhotoItem();
        photo.Labels.Add("Nature");
        photo.Labels.Add("Outdoor");

        // Act
        photo.Labels.Clear();

        // Assert
        photo.Labels.Should().BeEmpty();
        photo.HasLabels.Should().BeFalse();
    }

    [Fact]
    public void Labels_CheckingHasLabels_ReturnsCorrectValue()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act & Assert - Initially false
        photo.HasLabels.Should().BeFalse();

        // Act - Add label
        photo.Labels.Add("Nature");

        // Assert
        photo.HasLabels.Should().BeTrue();

        // Act - Remove label
        photo.Labels.Remove("Nature");

        // Assert
        photo.HasLabels.Should().BeFalse();
    }

    [Fact]
    public void Labels_IsObservableCollection()
    {
        // Arrange & Act
        var photo = new PhotoItem();

        // Assert
        photo.Labels.Should().BeOfType<ObservableCollection<string>>();
    }

    [Fact]
    public void Labels_AddingDuplicateLabel_IsAllowed()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Labels.Add("Nature");
        photo.Labels.Add("Nature"); // Duplicate

        // Assert
        photo.Labels.Should().HaveCount(2);
        photo.Labels.Should().OnlyContain(l => l == "Nature");
    }

    [Fact]
    public void Labels_CaseSensitiveCollection()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Labels.Add("Nature");
        photo.Labels.Add("nature"); // Different case

        // Assert
        photo.Labels.Should().HaveCount(2);
        photo.Labels.Should().Contain("Nature");
        photo.Labels.Should().Contain("nature");
    }

    [Fact]
    public void Labels_CanCheckIfContainsLabel()
    {
        // Arrange
        var photo = new PhotoItem();
        photo.Labels.Add("Nature");
        photo.Labels.Add("Outdoor");

        // Act & Assert
        photo.Labels.Should().Contain("Nature");
        photo.Labels.Should().Contain("Outdoor");
        photo.Labels.Should().NotContain("Vacation");
    }

    [Fact]
    public void Labels_OrderOfLabels_IsPreserved()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.Labels.Add("First");
        photo.Labels.Add("Second");
        photo.Labels.Add("Third");

        // Assert
        photo.Labels[0].Should().Be("First");
        photo.Labels[1].Should().Be("Second");
        photo.Labels[2].Should().Be("Third");
    }
}

