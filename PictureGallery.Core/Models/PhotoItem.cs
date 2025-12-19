using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using SQLite;

namespace PictureGallery.Core.Models;

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
    /// Validates that the file is a valid image using SkiaSharp
    /// Returns true if the file exists and is a valid image, false otherwise
    /// This method does NOT create ImageSource (that's platform-specific)
    /// </summary>
    public bool ValidateImageFile()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            System.Diagnostics.Debug.WriteLine($"ValidateImageFile: FilePath is null or empty for photo {Id}");
            return false;
        }
        
        if (!File.Exists(FilePath))
        {
            System.Diagnostics.Debug.WriteLine($"ValidateImageFile: File does not exist at path '{FilePath}' for photo {Id}");
            return false;
        }

        // Additional validation: check if file is accessible and readable
        try
        {
            var fileInfo = new FileInfo(FilePath);
            if (fileInfo.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"ValidateImageFile: SKIP photo {Id} - File '{FilePath}' is empty (0 bytes)");
                return false;
            }

            // Check file permissions by trying to open it
            using (var testStream = File.OpenRead(FilePath))
            {
                if (testStream.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"ValidateImageFile: SKIP photo {Id} - File '{FilePath}' is empty (0 bytes)");
                    return false;
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"ValidateImageFile: SKIP photo {Id} - No access to file '{FilePath}': {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"ValidateImageFile: SKIP photo {Id} - IO error accessing file '{FilePath}': {ex.Message}");
            return false;
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
                        System.Diagnostics.Debug.WriteLine($"ValidateImageFile: SKIP photo {Id} - File '{FilePath}' is not a valid image (SkiaSharp could not decode it)");
                        return false;
                    }

                    if (bitmap.Width <= 0 || bitmap.Height <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"ValidateImageFile: SKIP photo {Id} - Image '{FilePath}' has invalid dimensions ({bitmap.Width}x{bitmap.Height})");
                        bitmap.Dispose();
                        return false;
                    }

                    // Verify bitmap data is valid
                    if (bitmap.Pixels == null || bitmap.Pixels.Length == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"ValidateImageFile: SKIP photo {Id} - Image '{FilePath}' has no pixel data");
                        bitmap.Dispose();
                        return false;
                    }
                }

                // Dispose bitmap
                bitmap?.Dispose();
                bitmap = null;

                return true;
            }
            finally
            {
                bitmap?.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ValidateImageFile: ERROR validating image for photo {Id}: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Helper method for property change notification
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


