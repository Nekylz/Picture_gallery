using PictureGallery.Models;

namespace PictureGallery.Views;
public partial class PhotoBookPage : ContentPage
{
    public PhotoBook PhotoBook { get; set; } = new();
    private static readonly int MaxPhotosPerPage = 4;
    private static readonly int MaxPages = 2;

    public PhotoBookPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Initialize pages
        for (int i = 0; i < MaxPages; i++)
            PhotoBook.Pages.Add(new PhotoBookPageModel());
    }

    /// <summary>
    /// Handelt klik op de "Add Photo" knop af
    /// Laat gebruiker meerdere foto's selecteren en voegt ze toe aan de eerste beschikbare pagina
    /// </summary>
    private async void AddPhoto_Clicked(object sender, EventArgs e)
    {
        try
        {
            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Selecteer afbeeldingen"
            });

            if (results == null || !results.Any())
                return;

            foreach (var result in results)
            {
                if (!IsSupportedImage(result.FileName))
                    continue;

                PhotoItem? photo = null;
                try
                {
                    photo = await CreatePhotoItemAsync(result);
                }
                catch (Exception photoEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating photo from {result.FileName}: {photoEx.Message}");
                    await DisplayAlert(
                        "Corrupte foto",
                        $"De foto '{result.FileName}' kon niet worden geladen. Het bestand is beschadigd.",
                        "OK");
                    continue;
                }
                
                if (photo == null || !photo.IsValid)
                {
                    await DisplayAlert(
                        "Ongeldige foto",
                        $"De foto '{result.FileName}' kon niet worden geladen.",
                        "OK");
                    continue;
                }

                // Zoek eerste pagina met minder dan 4 foto's
                var page = PhotoBook.Pages.FirstOrDefault(p => p.Photos.Count < MaxPhotosPerPage);
                if (page == null)
                {
                    await DisplayAlert("Vol!", "Alle pagina's zijn vol (max 4 foto's per pagina, 2 pagina's).", "OK");
                    break;
                }

                page.Photos.Add(photo);
            }

            // Forceer UI refresh door ItemsSource opnieuw in te stellen
            PagesCollectionView.ItemsSource = null;
            PagesCollectionView.ItemsSource = PhotoBook.Pages;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fout", ex.Message, "OK");
        }
    }

    private bool IsSupportedImage(string fileName)
    {
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) && allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maakt een PhotoItem aan van een geselecteerd bestand
    /// Kopieert het bestand naar cache directory als het niet direct beschikbaar is
    /// </summary>
    private async Task<PhotoItem> CreatePhotoItemAsync(FileResult result)
    {
        var filePath = result.FullPath;
        // Als bestand niet direct beschikbaar is, kopieer het naar cache directory
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            try
            {
                await using var pickedStream = await result.OpenReadAsync();
                filePath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid()}_{result.FileName}");
                await using var tempFile = File.Create(filePath);
                await pickedStream.CopyToAsync(tempFile);
                await tempFile.FlushAsync(); // Ensure all data is written to disk
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying file: {ex.Message}");
                throw new InvalidOperationException($"Kon bestand niet kopiëren: {ex.Message}", ex);
            }
        }

        // Verifieer dat bestand bestaat en niet leeg is
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("Bestand kon niet worden aangemaakt.");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            try
            {
                File.Delete(filePath);
            }
            catch { }
            throw new InvalidOperationException("Bestand is leeg.");
        }

        // Read image dimensions using SkiaSharp
        int width = 0, height = 0;
        try
        {
            using (var stream = File.OpenRead(filePath))
            {
                var bitmap = SkiaSharp.SKBitmap.Decode(stream);
                
                if (bitmap == null)
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch { }
                    throw new InvalidOperationException("Het bestand kon niet als afbeelding worden gelezen. Het bestand is mogelijk beschadigd of geen geldige afbeelding.");
                }
                
                try
                {
                    // Valideer dat de bitmap geldige afmetingen heeft
                    if (bitmap.Width <= 0 || bitmap.Height <= 0)
                    {
                        bitmap.Dispose();
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch { }
                        throw new InvalidOperationException("De afbeelding heeft geen geldige afmetingen.");
                    }
                    
                    width = bitmap.Width;
                    height = bitmap.Height;
                }
                finally
                {
                    bitmap?.Dispose();
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Re-throw invalid operation exceptions (corrupte afbeelding)
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                File.Delete(filePath);
            }
            catch { }
            System.Diagnostics.Debug.WriteLine($"Error reading image dimensions: {ex.Message}");
            throw new InvalidOperationException($"Kon afbeelding niet lezen: {ex.Message}", ex);
        }

        // Maak ImageSource met error handling
        ImageSource? imageSource = null;
        try
        {
            imageSource = ImageSource.FromFile(filePath);
        }
        catch (Exception imgEx)
        {
            // Verwijder het bestand als ImageSource niet kan worden gemaakt
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch { }
            throw new InvalidOperationException($"Kon afbeelding niet laden voor weergave: {imgEx.Message}", imgEx);
        }

        if (imageSource == null)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch { }
            throw new InvalidOperationException("Kon ImageSource niet aanmaken voor de afbeelding.");
        }

        return new PhotoItem
        {
            FileName = result.FileName,
            FilePath = filePath,
            Width = width,
            Height = height,
            ImageSource = imageSource
        };
    }
}
