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

                var photo = await CreatePhotoItemAsync(result);

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
            await using var pickedStream = await result.OpenReadAsync();
            filePath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid()}_{result.FileName}");
            await using var tempFile = File.Create(filePath);
            await pickedStream.CopyToAsync(tempFile);
        }

        return new PhotoItem
        {
            FileName = result.FileName,
            FilePath = filePath,
            ImageSource = ImageSource.FromFile(filePath)
        };
    }
}
