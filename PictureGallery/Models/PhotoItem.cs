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

    public double Latitude { get; set; }

    public double Longitude { get; set; }


    [Indexed]
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public int Rating { get; set; } = 0; // 0 = no rating, 1-5 = stars

    /// <summary>
    /// PhotoBookId indicates whether a photo belongs only to a PhotoBook (not in the main gallery).
    /// If null or 0, the photo is in the main gallery.
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

    // Runtime property - labels loaded separately from database
    [Ignore]
    public ObservableCollection<string> Labels { get; } = new ObservableCollection<string>();
    
    // Selection status for UI (runtime only)
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
    public string DimensionsText => $"Dimensions: {Width} x {Height}";

    [Ignore]
    public string FileSizeText => $"File Size: {FileSizeMb:F1} MB";

    [Ignore]
    public string CoordinatesText =>
    (Latitude != 0 || Longitude != 0)
        ? $"Location: {Latitude:F1}, {Longitude:F1}"
        : "Location: Not available";

    // Helper methods for database integration

    /// <summary>
    /// Initializes the ImageSource from the FilePath.
    /// Validates that the file is a valid image using SkiaSharp before creating the ImageSource.
    /// Also validates file permissions and accessibility.
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

        // Additional validation: check that file is accessible and readable
        try
        {
            var fileInfo = new FileInfo(FilePath);
            if (fileInfo.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP foto {Id} - Bestand '{FilePath}' is leeg (0 bytes)");
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

        // Validate that the file is actually a valid image by attempting to decode it
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
                        System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP foto {Id} - Bestand '{FilePath}' is geen geldige afbeelding (SkiaSharp kon het niet decoderen)");
                        return; // Do not set ImageSource if file is not a valid image
                    }

                    if (bitmap.Width <= 0 || bitmap.Height <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP foto {Id} - Afbeelding '{FilePath}' heeft ongeldige afmetingen ({bitmap.Width}x{bitmap.Height})");
                        bitmap.Dispose();
                        return; // Do not set ImageSource if image has invalid dimensions
                    }

                    // Verify that bitmap data is valid
                    if (bitmap.Pixels == null || bitmap.Pixels.Length == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP foto {Id} - Afbeelding '{FilePath}' heeft geen pixel data");
                        bitmap.Dispose();
                        return;
                    }
                }

                // Dispose bitmap before creating ImageSource
                bitmap?.Dispose();
                bitmap = null;

                // File is valid, try to create ImageSource now
                // Wrap ImageSource creation in try-catch because MAUI can fail even if SkiaSharp succeeds
                try
                {
                    ImageSource = ImageSource.FromFile(FilePath);
                    System.Diagnostics.Debug.WriteLine($"InitializeImageSource: ImageSource succesvol aangemaakt voor foto {Id} van '{FilePath}'");
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP foto {Id} - MAUI kan geen ImageSource maken van '{FilePath}': {ex.Message}");
                    // Do not set ImageSource if MAUI cannot create it
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
            System.Diagnostics.Debug.WriteLine($"InitializeImageSource: FOUT bij valideren/aanmaken ImageSource voor foto {Id}: {ex.Message}");
            // Do not throw,log error and do not set ImageSource
            // This prevents the app from crashing if a single image is corrupt
            return;
        }
    }

    /// <summary>
    /// Checks whether the file still exists on disk
    /// </summary>
    [Ignore]
    public bool FileExists => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    /// <summary>
    /// Checks whether the photo has valid data
    /// </summary>
    [Ignore]
    public bool IsValid => !string.IsNullOrEmpty(FileName) && 
                          !string.IsNullOrEmpty(FilePath) && 
                          Width > 0 && 
                          Height > 0;

    /// <summary>
    /// Returns a user friendly date string
    /// </summary>
    [Ignore]
    public string CreatedDateDisplay => "Imported: " + CreatedDate.ToString("dd MMM yyyy HH:mm");

    /// <summary>
    /// Checks whether the photo has labels
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
