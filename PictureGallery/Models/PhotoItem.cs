using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using PictureGallery.ViewModels;
using PictureGallery.Views;
using SkiaSharp;
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
    /// Validates that the file is a valid image using SkiaSharp before creating ImageSource
    /// Also validates file permissions and accessibility
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

        // Additional validation: check if file is accessible and readable
        try
        {
            var fileInfo = new FileInfo(FilePath);
            if (fileInfo.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP photo {Id} - File '{FilePath}' is empty (0 bytes)");
                return;
            }

            // Check file permissions by trying to open it
            using (var testStream = File.OpenRead(FilePath))
            {
                if (testStream.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP photo {Id} - File '{FilePath}' is empty (0 bytes)");
                    return;
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP photo {Id} - No access to file '{FilePath}': {ex.Message}");
            return;
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP photo {Id} - IO error accessing file '{FilePath}': {ex.Message}");
            return;
        }

        // Validate that the file is actually a valid image by trying to decode it
        try
        {
            SKBitmap? bitmap = null;
            try
            {
                using (var stream = File.OpenRead(FilePath))
                {
                    bitmap = SKBitmap.Decode(stream);
                    
                    if (bitmap == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP photo {Id} - File '{FilePath}' is not a valid image (SkiaSharp could not decode it)");
                        return; // Don't set ImageSource if file is not a valid image
                    }

                    if (bitmap.Width <= 0 || bitmap.Height <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP photo {Id} - Image '{FilePath}' has invalid dimensions ({bitmap.Width}x{bitmap.Height})");
                        bitmap.Dispose();
                        return; // Don't set ImageSource if image has invalid dimensions
                    }

                    // Verify bitmap data is valid
                    if (bitmap.Pixels == null || bitmap.Pixels.Length == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP photo {Id} - Image '{FilePath}' has no pixel data");
                        bitmap.Dispose();
                        return;
                    }
                }

                // Dispose bitmap before creating ImageSource
                bitmap?.Dispose();
                bitmap = null;

                // File is valid, now try to create ImageSource
                // Use a try-catch around ImageSource creation as MAUI may fail even if SkiaSharp succeeds
                try
                {
                    ImageSource = ImageSource.FromFile(FilePath);
                    System.Diagnostics.Debug.WriteLine($"InitializeImageSource: Successfully created ImageSource for photo {Id} from '{FilePath}'");
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP photo {Id} - MAUI cannot create ImageSource from '{FilePath}': {ex.Message}");
                    // Don't set ImageSource if MAUI cannot create it
                    return;
                }
            }
            finally
            {
                bitmap?.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeImageSource: ERROR validating/creating ImageSource for photo {Id}: {ex.Message}");
            // Don't throw - just log the error and don't set ImageSource
            // This prevents the app from crashing if one image is corrupt
            return;
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
    public string CreatedDateDisplay => "Imported: " + CreatedDate.ToString("dd MMM yyyy HH:mm");

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
