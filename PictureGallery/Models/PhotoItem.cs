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

    public int Rating { get; set; } = 0; // 0 = geen rating, 1-5 = sterren

    /// <summary>
    /// PhotoBookId om aan te geven of foto alleen bij een PhotoBook hoort (niet in hoofdgalerij)
    /// Als null of 0, dan staat de foto in de hoofdgalerij
    /// </summary>
    [Indexed]
    public int? PhotoBookId { get; set; }

    // Runtime property - niet opgeslagen in database
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

    // Runtime property - apart geladen uit database
    [Ignore]
    public ObservableCollection<string> Labels { get; } = new ObservableCollection<string>();
    
    // Selectiestatus voor UI (alleen runtime)
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

    // Berekende properties
    [Ignore]
    public string DimensionsText => $"Dimensions: {Width} x {Height}";

    [Ignore]
    public string FileSizeText => $"File Size: {FileSizeMb:F1} MB";

    [Ignore]
    public string CoordinatesText =>
    (Latitude != 0 || Longitude != 0)
        ? $"Location: {Latitude:F1}, {Longitude:F1}"
        : "Location: Not available";

    // Helper methodes voor database integratie

    /// <summary>
    /// Initialiseert de ImageSource vanuit het FilePath
    /// Valideert dat het bestand een geldige afbeelding is met SkiaSharp voordat ImageSource wordt aangemaakt
    /// Valideert ook bestandspermissies en toegankelijkheid
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

        // Aanvullende validatie: controleer of bestand toegankelijk en leesbaar is
        try
        {
            var fileInfo = new FileInfo(FilePath);
            if (fileInfo.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP foto {Id} - Bestand '{FilePath}' is leeg (0 bytes)");
                return;
            }

            // Controleer bestandspermissies door te proberen het te openen
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

        // Valideer dat het bestand daadwerkelijk een geldige afbeelding is door te proberen het te decoderen
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
                        return; // Zet geen ImageSource als bestand geen geldige afbeelding is
                    }

                    if (bitmap.Width <= 0 || bitmap.Height <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP foto {Id} - Afbeelding '{FilePath}' heeft ongeldige afmetingen ({bitmap.Width}x{bitmap.Height})");
                        bitmap.Dispose();
                        return; // Zet geen ImageSource als afbeelding ongeldige afmetingen heeft
                    }

                    // Verifieer dat bitmap data geldig is
                    if (bitmap.Pixels == null || bitmap.Pixels.Length == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP foto {Id} - Afbeelding '{FilePath}' heeft geen pixel data");
                        bitmap.Dispose();
                        return;
                    }
                }

                // Verwijder bitmap voordat ImageSource wordt aangemaakt
                bitmap?.Dispose();
                bitmap = null;

                // Bestand is geldig, probeer nu ImageSource aan te maken
                // Gebruik try-catch rond ImageSource creatie omdat MAUI kan falen zelfs als SkiaSharp slaagt
                try
                {
                    ImageSource = ImageSource.FromFile(FilePath);
                    System.Diagnostics.Debug.WriteLine($"InitializeImageSource: ImageSource succesvol aangemaakt voor foto {Id} van '{FilePath}'");
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"InitializeImageSource: SKIP foto {Id} - MAUI kan geen ImageSource maken van '{FilePath}': {ex.Message}");
                    // Zet geen ImageSource als MAUI het niet kan aanmaken
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
            // Gooi geen exception - log alleen de fout en zet geen ImageSource
            // Dit voorkomt dat de app crasht als één afbeelding corrupt is
            return;
        }
    }

    /// <summary>
    /// Controleert of het bestand nog steeds op schijf bestaat
    /// </summary>
    [Ignore]
    public bool FileExists => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    /// <summary>
    /// Controleert of de foto geldige data heeft
    /// </summary>
    [Ignore]
    public bool IsValid => !string.IsNullOrEmpty(FileName) && 
                          !string.IsNullOrEmpty(FilePath) && 
                          Width > 0 && 
                          Height > 0;

    /// <summary>
    /// Geeft een gebruiksvriendelijke datum string terug
    /// </summary>
    [Ignore]
    public string CreatedDateDisplay => "Imported: " + CreatedDate.ToString("dd MMM yyyy HH:mm");

    /// <summary>
    /// Controleert of de foto labels heeft
    /// </summary>
    [Ignore]
    public bool HasLabels => Labels.Count > 0;
    
    /// <summary>
    /// Helper methode voor property change notificatie
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
