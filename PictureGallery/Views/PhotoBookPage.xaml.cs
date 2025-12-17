using PictureGallery.Models;
using PictureGallery.Services;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Views;

namespace PictureGallery.Views;

public partial class PhotoBookPage : ContentPage
{
    // Houdt het fotoboek en alle pagina's bij
    public PhotoBook PhotoBook { get; set; } = new();

    // Maximum aantal foto's per pagina
    private static readonly int MaxPhotosPerPage = 12;

    // Aantal foto's waarna snelle PDF relevant wordt
    private const int FastPdfThreshold = 8;

    // Houdt bij of de upgrade popup deze sessie al is getoond
    private bool _upgradePopupShownThisSession = false;

    // PDF modus binding
    public static readonly BindableProperty IsPdfModeProperty =
        BindableProperty.Create(
            nameof(IsPdfMode),
            typeof(bool),
            typeof(PhotoBookPage),
            false);

    public bool IsPdfMode
    {
        get => (bool)GetValue(IsPdfModeProperty);
        set => SetValue(IsPdfModeProperty, value);
    }

    public PhotoBookPage()
    {
        InitializeComponent();
        BindingContext = this;

        PlanFrame.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnPlanTappedAsync())
        });

        PhotoCarousel.PositionChanged += OnPageChanged;

        ResetUpgradeForTest(); // for test om upgrade te resetten na opstarten

        // Eerste pagina aanmaken
        PhotoBook.Pages.Add(new PhotoBookPageModel());
    }

    public PhotoBookPage(int photoBookId) : this()
    {
        // Kan later gebruikt worden om bestaand fotoboek te laden
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdatePlanBar();
    }

    // Past de tekst van de plan balk aan
    private void UpdatePlanBar()
    {
        bool fastUnlocked = Preferences.Get("FastPdfUnlocked", false);

        if (fastUnlocked)
        {
            PlanTitle.Text = "Plan";
            PlanSubtitle.Text = "Fast conversion";
        }
        else
        {
            PlanTitle.Text = "Upgrade";
            PlanSubtitle.Text = "Current plan: Basic";
        }
    }

    // Plan balk aanklikken
    private async Task OnPlanTappedAsync()
    {
        bool fastUnlocked = Preferences.Get("FastPdfUnlocked", false);

        if (fastUnlocked)
        {
            await DisplayAlert("Plan", "Fast conversion is al actief.", "OK");
            return;
        }

        var popup = new PlanPopup();
        var result = await this.ShowPopupAsync(popup);

        if (result is string password)
        {
            if (password == "DEVGROUP2")
            {
                Preferences.Set("FastPdfUnlocked", true);
                UpdatePlanBar();
                
                await DisplayAlert(
                    "Upgrade voltooid",
                    "Fast conversie is nu geactiveerd. Bedankt en veel plezier met het gebruik.",
                    "OK");
            }
            else if (!string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Fout", "Onjuist wachtwoord.", "OK");
            }
        }
    }

    // Foto's toevoegen
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
                var page = PhotoBook.Pages.Last();

                if (page.Photos.Count >= MaxPhotosPerPage)
                {
                    page = new PhotoBookPageModel();
                    PhotoBook.Pages.Add(page);
                }

                page.Photos.Add(photo);
                PhotoCarousel.Position = PhotoBook.Pages.Count - 1;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fout", ex.Message, "OK");
        }
    }

    // Controleert toegestane extensies
    private bool IsSupportedImage(string fileName)
    {
        var allowed = new[] { ".png", ".jpg", ".jpeg" };
        return allowed.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
    }

    // Maakt PhotoItem aan en zet bestand in cache
    private async Task<PhotoItem> CreatePhotoItemAsync(FileResult result)
    {
        var filePath = result.FullPath;

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            await using var stream = await result.OpenReadAsync();
            filePath = Path.Combine(
                FileSystem.CacheDirectory,
                $"{Guid.NewGuid()}_{result.FileName}");

            await using var file = File.Create(filePath);
            await stream.CopyToAsync(file);
        }

        using var testStream = File.OpenRead(filePath);
        var bitmap = SkiaSharp.SKBitmap.Decode(testStream);

        if (bitmap == null)
            throw new InvalidOperationException("Ongeldige afbeelding.");

        return new PhotoItem
        {
            FileName = result.FileName,
            FilePath = filePath,
            Width = bitmap.Width,
            Height = bitmap.Height,
            ImageSource = ImageSource.FromFile(filePath)
        };
    }

    // Pagina indicator bijwerken
    private void OnPageChanged(object sender, PositionChangedEventArgs e)
    {
        int current = e.CurrentPosition + 1;
        int total = PhotoBook.Pages.Count;
        PageIndicator.Text = $"Pagina {current} van {total}";
    }

    // Start delete modus
    private void StartDeleteMode_Clicked(object sender, EventArgs e)
    {
        DeleteToolbar.IsVisible = true;
    }

    // Annuleer delete modus
    private void CancelDeleteMode_Clicked(object sender, EventArgs e)
    {
        DeleteToolbar.IsVisible = false;

        foreach (var page in PhotoBook.Pages)
        foreach (var photo in page.Photos)
            photo.IsSelected = false;
    }

    // Verwijdert geselecteerde foto's
    private void DeleteSelectedPhotos_Clicked(object sender, EventArgs e)
    {
        foreach (var page in PhotoBook.Pages)
        {
            var toRemove = page.Photos.Where(p => p.IsSelected).ToList();
            foreach (var photo in toRemove)
                page.Photos.Remove(photo);
        }

        CancelDeleteMode_Clicked(sender, e);
    }

    // Foto aanklikken
    private void OnPhotoTapped(object sender, TappedEventArgs e)
    {
        if (!DeleteToolbar.IsVisible && !IsPdfMode)
            return;

        if (sender is BindableObject b && b.BindingContext is PhotoItem photo)
            photo.IsSelected = !photo.IsSelected;
    }

    // Vorige pagina
    private void PrevPage(object sender, EventArgs e)
    {
        if (PhotoCarousel.Position > 0)
            PhotoCarousel.Position--;
    }

    // Volgende pagina
    private void NextPage(object sender, EventArgs e)
    {
        if (PhotoCarousel.Position < PhotoBook.Pages.Count - 1)
            PhotoCarousel.Position++;
    }

    // PDF modus starten
    private void StartPdfMode_Clicked(object sender, EventArgs e)
    {
        IsPdfMode = true;
        PdfToolbar.IsVisible = true;

        foreach (var page in PhotoBook.Pages)
        foreach (var photo in page.Photos)
            photo.IsSelected = true;
    }

    private void CancelPdfMode_Clicked(object sender, EventArgs e)
    {
        IsPdfMode = false;
        PdfToolbar.IsVisible = false;

        foreach (var page in PhotoBook.Pages)
        foreach (var photo in page.Photos)
            photo.IsSelected = false;
    }

    // PDF export
    private async void ExportPdf_Clicked(object sender, EventArgs e)
    {
        try
        {
            var selectedPhotos = PhotoBook.Pages
                .SelectMany(p => p.Photos)
                .Where(p => p.IsSelected)
                .Select(p => p.FilePath)
                .ToArray();

            if (selectedPhotos.Length == 0)
            {
                await DisplayAlert("Geen selectie", "Selecteer foto's.", "OK");
                return;
            }

            var folder = await FolderPicker.Default.PickAsync(default);
            if (!folder.IsSuccessful)
                return;

            string pdfPath = Path.Combine(
                folder.Folder.Path,
                $"Fotoboek_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            ProgressFrame.IsVisible = true;
            PdfProgressBar.Progress = 0;
            ProgressLabel.Text = $"0% (0 van {selectedPhotos.Length} foto's)";

            bool fastUnlocked = Preferences.Get("FastPdfUnlocked", false);
            bool exceedsLimit = selectedPhotos.Length > FastPdfThreshold;

            if (exceedsLimit && !fastUnlocked && !_upgradePopupShownThisSession)
            {
                _upgradePopupShownThisSession = true;

                bool upgrade = await DisplayAlert(
                    "Langzame conversie",
                    "Upgrade voor snelle PDF conversie?",
                    "Upgrade",
                    "Doorgaan");

                if (upgrade)
                {
                    var popup = new PlanPopup();
                    var result = await this.ShowPopupAsync(popup);

                    if (result is string password && password == "DEVGROUP2")
                    {
                        Preferences.Set("FastPdfUnlocked", true);
                        fastUnlocked = true;
                        UpdatePlanBar();
                    }
                }
            }

            bool useFastPdf = fastUnlocked && exceedsLimit;

            if (useFastPdf)
                await GenerateFastPdfWithProgress(selectedPhotos, pdfPath);
            else
                await GeneratePdfWithProgress(selectedPhotos, pdfPath);

            await DisplayAlert("Succes", $"PDF opgeslagen:\n{pdfPath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fout", ex.Message, "OK");
        }
        finally
        {
            ProgressFrame.IsVisible = false;
            CancelPdfMode_Clicked(sender, e);
        }
    }

    // Snelle PDF conversie met progress
    private async Task GenerateFastPdfWithProgress(string[] images, string outputPath)
    {
        await Task.Run(() =>
        {
            SkiaSharpPdfService.ImagesToPdf(
                images,
                outputPath,
                (processed, total) =>
                {
                    double progress = (double)processed / total;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        PdfProgressBar.Progress = progress;
                        ProgressLabel.Text =
                            $"{(int)(progress * 100)}% ({processed} van {total} foto's)";
                    });
                });
        });
    }

    // Langzame PDF conversie
    private async Task GeneratePdfWithProgress(string[] images, string outputPath)
    {
        await Task.Run(() =>
        {
            var doc = new PdfSharpCore.Pdf.PdfDocument();

            int total = images.Length;
            int processed = 0;

            foreach (var imgPath in images)
            {
                using var img = PdfSharpCore.Drawing.XImage.FromFile(imgPath);
                var page = doc.AddPage();
                page.Width = img.PixelWidth;
                page.Height = img.PixelHeight;

                using var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
                gfx.DrawImage(img, 0, 0);

                processed++;
                double progress = (double)processed / total;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PdfProgressBar.Progress = progress;
                    ProgressLabel.Text =
                        $"{(int)(progress * 100)}% ({processed} van {total} foto's)";
                });
            }

            doc.Save(outputPath);
        });
    }

    // Reset upgrade status voor test
    private void ResetUpgradeForTest()
    {
        Preferences.Remove("FastPdfUnlocked");
    }
}
