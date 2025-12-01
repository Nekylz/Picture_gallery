using System.Collections.ObjectModel;
using System.IO;
using SQLite;

namespace PictureGallery.Models;

[Table("Photos")]
public class PhotoItem
{
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

    // Runtime property - not stored in database
    [Ignore]
    public ImageSource? ImageSource { get; set; }

    // Runtime property - loaded from database separately
    [Ignore]
    public ObservableCollection<string> Labels { get; } = new ObservableCollection<string>();

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
        if (File.Exists(FilePath))
        {
            ImageSource = ImageSource.FromFile(FilePath);
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
}

