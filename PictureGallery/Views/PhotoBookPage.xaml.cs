using PictureGallery.Models;
using PictureGallery.Services;
using CommunityToolkit.Maui.Storage;
using System.Linq;

namespace PictureGallery.Views;

public partial class PhotoBookPage : ContentPage
{
    // Houdt alle pagina's en foto's bij
    public PhotoBook PhotoBook { get; set; } = new();

    // Maximum aantal foto's per pagina
    private static readonly int MaxPhotosPerPage = 12;
    
    private readonly DatabaseService _databaseService;
    private readonly int? _photoBookId;

    public PhotoBookPage() : this(null)
    {
    }

    public PhotoBookPage(int? photoBookId)
    {
        _photoBookId = photoBookId;
        _databaseService = new DatabaseService();
        
        InitializeComponent();
        BindingContext = this;

        PhotoCarousel.PositionChanged += OnPageChanged;

        // Load photo book if ID is provided, otherwise create empty
        _ = LoadPhotoBookAsync();
    }
    
    private async Task LoadPhotoBookAsync()
    {
        try
        {
            if (_photoBookId.HasValue)
            {
                var loadedPhotoBook = await _databaseService.GetPhotoBookByIdAsync(_photoBookId.Value);
                if (loadedPhotoBook != null)
                {
                    PhotoBook = loadedPhotoBook;
                    
                    // Initialize ImageSource for all photos
                    foreach (var page in PhotoBook.Pages)
                    {
                        foreach (var photo in page.Photos)
                        {
                            if (photo.FileExists)
                            {
                                photo.InitializeImageSource();
                            }
                        }
                    }
                }
            }
            
            // If no photo book loaded or no ID, create first page
            if (PhotoBook.Pages.Count == 0)
            {
                PhotoBook.Pages.Add(new PhotoBookPageModel());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading PhotoBook: {ex.Message}");
            // Create first page on error
            if (PhotoBook.Pages.Count == 0)
            {
                PhotoBook.Pages.Add(new PhotoBookPageModel());
            }
        }
    }

    /// <summary>
    /// Handelt klik op de "Add Photo" knop af
    /// Laat gebruiker meerdere foto's selecteren en voegt ze toe aan de laatste beschikbare pagina
    /// </summary>
    private async void AddPhoto_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Laat gebruiker meerdere afbeeldingen selecteren
            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Selecteer afbeeldingen"
            });

            if (results == null || !results.Any())
                return;

            foreach (var result in results)
            {
                // Alleen PNG en JPG toestaan
                if (!IsSupportedImage(result.FileName))
                    continue;

                PhotoItem? photo = null;

                try
                {
                    // Foto inlezen en opslaan in cache
                    photo = await CreatePhotoItemAsync(result);
                }
                catch (Exception photoEx)
                {
                    await DisplayAlert(
                        "Corrupte foto",
                        $"De foto '{result.FileName}' kon niet worden geladen.",
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

                // Haal laatste pagina op
                var page = PhotoBook.Pages.Last();

                // Nieuwe pagina aanmaken indien vol
                if (page.Photos.Count >= MaxPhotosPerPage)
                {
                    page = new PhotoBookPageModel();
                    PhotoBook.Pages.Add(page);
                }

                // Foto opslaan in database
                try
                {
                    await _databaseService.AddPhotoAsync(photo);
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving photo to database: {dbEx.Message}");
                    await DisplayAlert("Database Fout", $"Kon foto niet opslaan: {dbEx.Message}", "OK");
                    continue;
                }

                // Alles op main thread voor UI updates
                await Microsoft.Maui.Controls.Application.Current?.Dispatcher.DispatchAsync(() =>
                {
                    // Initialize ImageSource voor de foto VOORDAT we toevoegen
                    if (photo.FileExists)
                    {
                        photo.InitializeImageSource();
                    }

                    // Foto toevoegen aan pagina (triggert UI update)
                    page.Photos.Add(photo);
                });

                // PhotoBook updaten in database als het al een ID heeft
                if (_photoBookId.HasValue)
                {
                    try
                    {
                        await _databaseService.UpdatePhotoBookAsync(PhotoBook);
                    }
                    catch (Exception updateEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating PhotoBook: {updateEx.Message}");
                        // Niet fatal, foto is al toegevoegd aan UI
                    }
                }

                // Ga automatisch naar laatste pagina
                PhotoCarousel.Position = PhotoBook.Pages.Count - 1;
            }
            
            // Force UI refresh - ObservableCollection should auto-update, but ensure CarouselView refreshes
            await Microsoft.Maui.Controls.Application.Current?.Dispatcher.DispatchAsync(() =>
            {
                // Refresh CarouselView to show new photos
                var currentPosition = PhotoCarousel.Position;
                var itemsSource = PhotoCarousel.ItemsSource;
                PhotoCarousel.ItemsSource = null;
                PhotoCarousel.ItemsSource = itemsSource;
                if (currentPosition >= 0 && currentPosition < PhotoBook.Pages.Count)
                {
                    PhotoCarousel.Position = currentPosition;
                }
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fout", ex.Message, "OK");
        }
    }

    // Controleert of extensie toegestaan is
    private bool IsSupportedImage(string fileName)
    {
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) &&
               allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Zet FilePicker-resultaat om naar PhotoItem
    /// Kopieert afbeelding naar cache indien nodig
    /// </summary>
    private async Task<PhotoItem> CreatePhotoItemAsync(FileResult result)
    {
        var filePath = result.FullPath;

        // Onvolledig pad oplossen door bestand in cache op te slaan
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            try
            {
                await using var pickedStream = await result.OpenReadAsync();
                filePath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid()}_{result.FileName}");
                await using var tempFile = File.Create(filePath);
                await pickedStream.CopyToAsync(tempFile);
                await tempFile.FlushAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Kon bestand niet kopiëren: {ex.Message}", ex);
            }
        }

        if (!File.Exists(filePath))
            throw new InvalidOperationException("Bestand kon niet worden aangemaakt.");

        // Bestand mag niet leeg zijn
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            try { File.Delete(filePath); } catch { }
            throw new InvalidOperationException("Bestand is leeg.");
        }

        // Afbeeldingsgrootte testen via SkiaSharp
        int width = 0, height = 0;

        try
        {
            using var stream = File.OpenRead(filePath);
            var bitmap = SkiaSharp.SKBitmap.Decode(stream);

            if (bitmap == null)
            {
                try { File.Delete(filePath); } catch { }
                throw new InvalidOperationException("Het bestand kon niet als afbeelding worden gelezen.");
            }

            if (bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                bitmap.Dispose();
                try { File.Delete(filePath); } catch { }
                throw new InvalidOperationException("De afbeelding heeft geen geldige afmetingen.");
            }

            width = bitmap.Width;
            height = bitmap.Height;

            bitmap.Dispose();
        }
        catch (Exception ex)
        {
            try { File.Delete(filePath); } catch { }
            throw new InvalidOperationException($"Kon afbeelding niet lezen: {ex.Message}", ex);
        }

        // ImageSource maken voor weergave
        ImageSource? imageSource = null;

        try
        {
            imageSource = ImageSource.FromFile(filePath);
        }
        catch (Exception imgEx)
        {
            try { File.Delete(filePath); } catch { }
            throw new InvalidOperationException($"Kon afbeelding niet laden voor weergave: {imgEx.Message}", imgEx);
        }

        if (imageSource == null)
        {
            try { File.Delete(filePath); } catch { }
            throw new InvalidOperationException("Kon ImageSource niet aanmaken voor de afbeelding.");
        }

        //  PhotoItem teruggeven
        return new PhotoItem
        {
            FileName = result.FileName,
            FilePath = filePath,
            Width = width,
            Height = height,
            ImageSource = imageSource
        };
    }

    // Gaat 1 pagina vooruit
    private void NextPage(object sender, EventArgs e)
    {
        if (PhotoCarousel.Position < PhotoBook.Pages.Count - 1)
            PhotoCarousel.Position++;
    }

    // Gaat 1 pagina terug
    private void PrevPage(object sender, EventArgs e)
    {
        if (PhotoCarousel.Position > 0)
            PhotoCarousel.Position--;
    }

    // Houdt pagina indicator bij en toont navigatie pijlen
    private void OnPageChanged(object sender, PositionChangedEventArgs e)
    {
        int current = e.CurrentPosition + 1;
        int total = PhotoBook.Pages.Count;

        PageIndicator.Text = $"Pagina {current} van {total}";

        bool singlePage = total <= 1;

        PrevArrow.IsVisible = !singlePage && PhotoCarousel.Position > 0;
        NextArrow.IsVisible = !singlePage && PhotoCarousel.Position < total - 1;
    }

    // Start delete modus
    private void StartDeleteMode_Clicked(object sender, EventArgs e)
    {
        // Selectie leegmaken bij opstarten
        foreach (var page in PhotoBook.Pages)
        foreach (var photo in page.Photos)
            photo.IsSelected = false;

        DeleteToolbar.IsVisible = true;
    }

    // Stop delete modus
    private void CancelDeleteMode_Clicked(object sender, EventArgs e)
    {
        DeleteToolbar.IsVisible = false;

        foreach (var page in PhotoBook.Pages)
        foreach (var photo in page.Photos)
            photo.IsSelected = false;
    }

    // Verwijdert alle geselecteerde foto's
    private void DeleteSelectedPhotos_Clicked(object sender, EventArgs e)
    {
        foreach (var page in PhotoBook.Pages)
        {
            var toRemove = page.Photos.Where(p => p.IsSelected).ToList();

            foreach (var photo in toRemove)
                page.Photos.Remove(photo);
        }

        // Lege pagina's verwijderen behalve de eerste
        for (int i = PhotoBook.Pages.Count - 1; i > 0; i--)
        {
            if (PhotoBook.Pages[i].Photos.Count == 0)
                PhotoBook.Pages.RemoveAt(i);
        }

        CancelDeleteMode_Clicked(sender, e);
    }

    // Klik op foto voor selectie (alleen actief in delete of pdf modus)
    private void OnPhotoTapped(object sender, TappedEventArgs e)
    {
        if (!DeleteToolbar.IsVisible && !IsPdfMode)
            return;

        if (sender is BindableObject bindable && bindable.BindingContext is PhotoItem photo)
        {
            // Toggle selectie
            photo.IsSelected = !photo.IsSelected;
        }
    }

    // Bindable property voor PDF modus
    public static readonly BindableProperty IsPdfModeProperty =
        BindableProperty.Create(nameof(IsPdfMode), typeof(bool), typeof(PhotoBookPage), false);

    public bool IsPdfMode
    {
        get => (bool)GetValue(IsPdfModeProperty);
        set => SetValue(IsPdfModeProperty, value);
    }

    // Start PDF selectie modus
    private void StartPdfMode_Clicked(object sender, EventArgs e)
    {
        DeleteToolbar.IsVisible = false;

        IsPdfMode = true;

        // Alles standaard geselecteerd
        foreach (var page in PhotoBook.Pages)
        foreach (var photo in page.Photos)
            photo.IsSelected = true;

        PdfToolbar.IsVisible = true;
    }

    // Stop PDF modus
    private void CancelPdfMode_Clicked(object sender, EventArgs e)
    {
        IsPdfMode = false;

        foreach (var page in PhotoBook.Pages)
        foreach (var photo in page.Photos)
            photo.IsSelected = false;

        PdfToolbar.IsVisible = false;
    }

    // Exporteert geselecteerde foto's als PDF
    private async void ExportPdf_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Alle geselecteerde foto's verzamelen
            var selectedPhotos = PhotoBook.Pages
                .SelectMany(p => p.Photos)
                .Where(p => p.IsSelected)
                .Select(p => p.FilePath)
                .ToArray();

            if (selectedPhotos.Length == 0)
            {
                await DisplayAlert("Geen selectie", "Selecteer minstens één foto.", "OK");
                return;
            }

            // Laat gebruiker folder kiezen
            var folderResult = await FolderPicker.Default.PickAsync(default);
            
            if (!folderResult.IsSuccessful)
                return;

            // Maak bestandsnaam met tijdstempel
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string pdfPath = Path.Combine(folderResult.Folder.Path, $"Fotoboek_{timestamp}.pdf");

            // Toon progress indicator
            ProgressFrame.IsVisible = true;
            PdfProgressBar.Progress = 0;
            ProgressLabel.Text = "0% (0 van 0 foto's)";

            // PDF genereren met progress updates
            await GeneratePdfWithProgress(selectedPhotos, pdfPath);

            // Verberg progress
            ProgressFrame.IsVisible = false;

            // Vraag of gebruiker wil delen
            bool share = await DisplayAlert(
                "Succes", 
                $"PDF opgeslagen in:\n{pdfPath}\n\nWil je het bestand delen?", 
                "Delen", 
                "Sluiten");

            if (share)
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Deel Fotoboek PDF",
                    File = new ShareFile(pdfPath)
                });
            }
            else
            {
                // Open PDF als gebruiker niet wil delen
                await Launcher.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(pdfPath)
                });
            }
        }
        catch (Exception ex)
        {
            ProgressFrame.IsVisible = false;
            await DisplayAlert("Fout", ex.Message, "OK");
        }
        finally
        {
            // Modus uitzetten en selectie leegmaken
            IsPdfMode = false;
            PdfToolbar.IsVisible = false;

            foreach (var page in PhotoBook.Pages)
            foreach (var photo in page.Photos)
                photo.IsSelected = false;
        }
    }

    // Genereert PDF met progress updates
    private async Task GeneratePdfWithProgress(string[] images, string outputPath)
    {
        int totalImages = images.Length;
        int processed = 0;

        // Update eerste keer voor initialisatie
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            PdfProgressBar.Progress = 0;
            ProgressLabel.Text = $"0% (0 van {totalImages} foto's)";
        });

        // Kleine vertraging zodat UI kan renderen
        await Task.Delay(100);

        // Maak PDF document aan
        var doc = new PdfSharpCore.Pdf.PdfDocument();

        foreach (var imgPath in images)
        {
            if (!File.Exists(imgPath))
            {
                processed++;
                continue;
            }

            using (var img = PdfSharpCore.Drawing.XImage.FromFile(imgPath))
            {
                var page = doc.AddPage();
                page.Width = img.PixelWidth * 72 / img.HorizontalResolution;
                page.Height = img.PixelHeight * 72 / img.VerticalResolution;

                using (var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page))
                {
                    gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                }
            }

            processed++;
            double progress = (double)processed / totalImages;

            // Update UI op met betere feedback
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PdfProgressBar.Progress = progress;
                ProgressLabel.Text = $"{(int)(progress * 100)}% ({processed} van {totalImages} foto's)";
            });

            // Kleine vertraging zodat UI kan updaten 
            await Task.Delay(50);
        }

        // Toon dat bestand wordt opgeslagen
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ProgressLabel.Text = "Bestand wordt opgeslagen...";
            PdfProgressBar.Progress = 1.0;
        });

        doc.Save(outputPath);
        doc.Close();
    }
}