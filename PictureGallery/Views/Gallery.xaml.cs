using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PictureGallery.Views;

public class PhotoItem
{
    public string FileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double FileSizeMb { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public ImageSource? ImageSource { get; set; }

    public string DimensionsText => $"Image Dimensions: {Width} x {Height}";
    public string FileSizeText => $"File Size: {FileSizeMb:F1} MB";
}

public partial class Gallery : ContentPage
{
    private static readonly string[] AllowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
    public ObservableCollection<PhotoItem> Photos { get; } = new();

	public Gallery()
	{
		InitializeComponent();
        BindingContext = this;
	}

    private PhotoItem? currentPhoto;

    /// <summary>
    /// Handles the event triggered to upload a media file by allowing the user to select a PNG file from the device.
    /// Sets the selected image, file name, dimensions, and file size on the UI.
    /// </summary>
    private async void UploadMedia(object sender, EventArgs e)
    {
        try
        {
            if (DeviceInfo.Platform == DevicePlatform.MacCatalyst)
            {
                var status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.StorageRead>();
                    if (status != PermissionStatus.Granted)
                    {
                        await DisplayAlert("Toegang geweigerd",
                            "Bestandstoegang is nodig om een foto te kunnen kiezen.",
                            "OK");
                        return;
                    }
                }
            }

            IEnumerable<FileResult>? results = null;

            try
            {
                results = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    return await FilePicker.Default.PickMultipleAsync(new PickOptions
                    {
                        FileTypes = FilePickerFileType.Images,
                        PickerTitle = "Select images (PNG / JPG)"
                    });
                });
            }
            catch (FeatureNotSupportedException)
            {
                await DisplayAlert("Niet ondersteund", "Bestanden kiezen wordt niet ondersteund op dit apparaat.", "OK");
                return;
            }
            catch (PermissionException)
            {
                await DisplayAlert("Toegang geweigerd", "Bestandstoegang is geweigerd.", "OK");
                return;
            }
            catch (Exception pickerEx)
            {
                await DisplayAlert("Foutmelding", $"Kon de bestandskiezer niet openen.\n\n{pickerEx.Message}", "OK");
                return;
            }

            if (results != null && results.Any())
            {
                var addedPhotos = new List<PhotoItem>();

                foreach (var result in results)
                {
                    if (!IsSupportedImage(result.FileName))
                    {
                        continue;
                    }

                    var photo = await CreatePhotoItemAsync(result);
                    if (photo != null)
                    {
                        addedPhotos.Add(photo);
                    }
                }

                if (addedPhotos.Count == 0)
                {
                    await DisplayAlert("Geen geldige bestanden", "Selecteer minimaal één PNG- of JPG-bestand.", "OK");
                    return;
                }

                // Add all photos to collection on main thread to ensure UI updates
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var photo in addedPhotos)
                    {
                        Photos.Add(photo);
                    }
                    
                    // Update photo count label
                    PhotoCountLabel.IsVisible = Photos.Count > 0;
                    
                    // Debug: Check collection count
                    System.Diagnostics.Debug.WriteLine($"Total photos in collection: {Photos.Count}");
                    
                    // Force CollectionView to refresh
                    PhotosCollection.ItemsSource = null;
                    PhotosCollection.ItemsSource = Photos;
                });

                // Keep reference to the last added photo (optional for future actions)
                currentPhoto = addedPhotos.Last();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Foutmelding",
                $"Er ging iets mis tijdens het uploaden.\n\nError: {ex.Message}",
                "OK");
        }
    }
    private async Task<PhotoItem?> CreatePhotoItemAsync(FileResult result)
    {
        var filePath = result.FullPath;

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            await using var pickedStream = await result.OpenReadAsync();
            filePath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid()}_{result.FileName}");
            await using var tempFile = File.Create(filePath);
            await pickedStream.CopyToAsync(tempFile);
        }

        int width = 0;
        int height = 0;
        using (var stream = File.OpenRead(filePath))
        using (var bitmap = SKBitmap.Decode(stream))
        {
            if (bitmap != null)
            {
                width = bitmap.Width;
                height = bitmap.Height;
            }
        }

        var fileSize = new FileInfo(filePath).Length;
        var fileSizeMB = fileSize / (1024.0 * 1024.0);

        return new PhotoItem
        {
            FileName = result.FileName,
            FilePath = filePath,
            ImageSource = ImageSource.FromFile(filePath),
            Width = width,
            Height = height,
            FileSizeMb = fileSizeMB
        };
    }

    private void CloseFullscreen(object sender, EventArgs e)
    {
        FullscreenOverlay.IsVisible = false;
    }

    private void OnPhotoTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is PhotoItem tappedPhoto)
        {
            currentPhoto = tappedPhoto;
            ShowPhotoOverlay(tappedPhoto);
        }
    }

    private void ShowPhotoOverlay(PhotoItem photo)
    {
        if (photo.ImageSource == null)
        {
            return;
        }

        FullscreenImage.Source = photo.ImageSource;
        OverlayFileName.Text = photo.FileName;
        OverlayDimensions.Text = photo.DimensionsText;
        OverlayFileSize.Text = photo.FileSizeText;
        FullscreenOverlay.IsVisible = true;
    }

    private bool IsSupportedImage(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}