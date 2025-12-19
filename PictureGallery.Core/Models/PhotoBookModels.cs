using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SQLite;

namespace PictureGallery.Core.Models;

/// <summary>
/// Represents a single photo item in a photo book page.
/// </summary>
public class PhotoBookPageModel
{
    public ObservableCollection<PhotoItem> Photos { get; } = new();
    public string? Title { get; set; }
}

/// <summary>
/// Database model for PhotoBook with metadata (name, description, date, etc.)
/// </summary>
[Table("PhotoBooks")]
public class PhotoBook : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Indexed]
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime UpdatedDate { get; set; } = DateTime.Now;

    // Runtime property - not stored in database
    [Ignore]
    public ObservableCollection<PhotoBookPageModel> Pages { get; } = new();

    // Computed properties for UI
    [Ignore]
    public int TotalPhotos
    {
        get
        {
            return Pages?.Sum(p => p.Photos?.Count ?? 0) ?? 0;
        }
    }

    [Ignore]
    public double TotalSizeMb
    {
        get
        {
            double totalSize = 0;
            if (Pages != null)
            {
                foreach (var page in Pages)
                {
                    if (page.Photos != null)
                    {
                        totalSize += page.Photos.Sum(p => p.FileSizeMb);
                    }
                }
            }
            return totalSize;
        }
    }

    [Ignore]
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    [Ignore]
    public bool ShowPhotoCount => TotalPhotos > 0;

    [Ignore]
    public bool ShowStorageSize => TotalSizeMb > 0;

    // Selection state for UI (runtime only)
    private bool _isSelected;
    [Ignore]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

