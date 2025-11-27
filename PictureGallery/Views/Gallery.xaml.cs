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

public partial class Gallery : ContentPage
{
    private static readonly string[] AllowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
    public ObservableCollection<PhotoItem> Photos { get; } = new();

    private PhotoItem? currentPhoto;

    public Gallery()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private async void UploadMedia(object sender, EventArgs e)
    {
        try
        {
            IEnumerable<FileResult>? results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Select images (PNG / JPG)"
            });

            if (results != null && results.Any())
            {
                var addedPhotos = new List<PhotoItem>();

                foreach (var result in results)
                {
                    if (!IsSupportedImage(result.FileName))
                        continue;

                    var photo = await CreatePhotoItemAsync(result);
                    if (photo != null)
                        addedPhotos.Add(photo);
                }

                foreach (var photo in addedPhotos)
                    Photos.Add(photo);

                PhotoCountLabel.IsVisible = Photos.Count > 0;
                PhotosCollection.ItemsSource = null;
                PhotosCollection.ItemsSource = Photos;

                currentPhoto = addedPhotos.LastOrDefault();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
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

        int width = 0, height = 0;
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
            return;

        FullscreenImage.Source = photo.ImageSource;
        OverlayFileName.Text = photo.FileName;
        OverlayDimensions.Text = photo.DimensionsText;
        OverlayFileSize.Text = photo.FileSizeText;
        FullscreenOverlay.IsVisible = true;

        DisplayLabels(photo);
    }

    private void CloseFullscreen(object sender, EventArgs e)
    {
        FullscreenOverlay.IsVisible = false;
    }

    private void AddLabelButton_Clicked(object sender, EventArgs e)
    {
        if (currentPhoto == null)
            return;

        var newLabel = LabelEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(newLabel))
        {
            currentPhoto.Labels.Add(newLabel);
            LabelEntry.Text = string.Empty;
            DisplayLabels(currentPhoto);
        }
    }

    private void DisplayLabels(PhotoItem photo)
    {
        LabelContainer.Children.Clear();
        foreach (var label in photo.Labels)
        {
            LabelContainer.Children.Add(new Label
            {
                Text = label,
                BackgroundColor = Colors.LightGray,
                TextColor = Colors.Black,
                Padding = new Thickness(5)
            });
        }
    }

    private bool IsSupportedImage(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
