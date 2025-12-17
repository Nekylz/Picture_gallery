using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using PictureGallery.ViewModels;
using PictureGallery.Views;
using SQLite;

namespace PictureGallery.Models;

[Table("Photos")]
public class PhotoItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string FileName { get; set; } = string.Empty;

    [NotNull]
    public string FilePath { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public double FileSizeMb { get; set; }

    [Indexed]
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public int Rating { get; set; } = 0; // 0 = geen rating, 1-5 = sterren

    public string ExifDateText { get; set; } = string.Empty;

    public string ImportedDateText { get; set; } = string.Empty;

    /// <summary>
    /// PhotoBookId to indicate if photo belongs only to a PhotoBook (not in main gallery)
    /// If null or 0, photo is in main gallery
    /// </summary>
    [Indexed]
    public int? PhotoBookId { get; set; }

    // Runtime property - not stored in database
    private ImageSource? _imageSource;
    [Ignore]
    public ImageSource? ImageSource
    {
        get => _imageSource;
        set
        {
            if (_imageSource != value)
            {
                _imageSource = value;
                OnPropertyChanged();
            }
        }
    }

    // Runtime property - loaded from database separately
    [Ignore]
    public ObservableCollection<string> Labels { get; } = new ObservableCollection<string>();
    
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

    // Computed properties
    [Ignore]
    public string DimensionsText => $"Image Dimensions: {Width} x {Height}";

    [Ignore]
    public string FileSizeText => $"File Size: {FileSizeMb:F1} MB";

    // Helper methods for database integration

    /// <summary>
    /// Initialize the ImageSource from the FilePath
    /// </summary>
    public void InitializeImageSource()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            System.Diagnostics.Debug.WriteLine($"InitializeImageSource: FilePath is null or empty for photo {Id}");
            return;
        }
        
        if (!File.Exists(FilePath))
        {
            System.Diagnostics.Debug.WriteLine($"InitializeImageSource: File does not exist at path '{FilePath}' for photo {Id}");
            return;
        }
        
        try
        {
            ImageSource = ImageSource.FromFile(FilePath);
            System.Diagnostics.Debug.WriteLine($"InitializeImageSource: Successfully created ImageSource for photo {Id} from '{FilePath}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeImageSource: ERROR creating ImageSource for photo {Id}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Check if the file still exists on disk
    /// </summary>
    [Ignore]
    public bool FileExists => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    /// <summary>
    /// Check if the photo has valid data
    /// </summary>
    [Ignore]
    public bool IsValid => !string.IsNullOrEmpty(FileName) && 
                          !string.IsNullOrEmpty(FilePath) && 
                          Width > 0 && 
                          Height > 0;

    /// <summary>
    /// Get a display-friendly date string
    /// </summary>
    [Ignore]
    public string CreatedDateDisplay => CreatedDate.ToString("dd MMM yyyy HH:mm");

    /// <summary>
    /// Check if photo has any labels
    /// </summary>
    [Ignore]
    public bool HasLabels => Labels.Count > 0;
    
    /// <summary>
    /// Helper method for property change notification
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
