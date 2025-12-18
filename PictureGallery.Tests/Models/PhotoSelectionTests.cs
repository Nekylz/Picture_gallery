using FluentAssertions;
using PictureGallery.Core.Models;
using System.ComponentModel;
using Xunit;

namespace PictureGallery.Tests.Models;

/// <summary>
/// Tests voor photo selection functionaliteit
/// </summary>
public class PhotoSelectionTests
{
    [Fact]
    public void IsSelected_DefaultValue_IsFalse()
    {
        // Arrange & Act
        var photo = new PhotoItem();

        // Assert
        photo.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void IsSelected_SettingToTrue_UpdatesValue()
    {
        // Arrange
        var photo = new PhotoItem();

        // Act
        photo.IsSelected = true;

        // Assert
        photo.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void IsSelected_TogglingSelection_ChangesValue()
    {
        // Arrange
        var photo = new PhotoItem { IsSelected = false };

        // Act
        photo.IsSelected = !photo.IsSelected;

        // Assert
        photo.IsSelected.Should().BeTrue();

        // Act - Toggle again
        photo.IsSelected = !photo.IsSelected;

        // Assert
        photo.IsSelected.Should().BeFalse();
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
        changedPropertyName.Should().Be(nameof(PhotoItem.IsSelected));
    }

    [Fact]
    public void IsSelected_SettingSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var photo = new PhotoItem { IsSelected = true };
        bool propertyChangedRaised = false;
        photo.PropertyChanged += (sender, e) => propertyChangedRaised = true;

        // Act
        photo.IsSelected = true;

        // Assert
        propertyChangedRaised.Should().BeFalse();
    }

    [Fact]
    public void DeselectPhoto_SetsIsSelectedToFalse()
    {
        // Arrange
        var photo = new PhotoItem { IsSelected = true };

        // Act
        photo.IsSelected = false;

        // Assert
        photo.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void SelectPhoto_SetsIsSelectedToTrue()
    {
        // Arrange
        var photo = new PhotoItem { IsSelected = false };

        // Act
        photo.IsSelected = true;

        // Assert
        photo.IsSelected.Should().BeTrue();
    }
}

