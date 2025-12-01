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
            LabelContainer.Children.Add(CreateLabelChip(label));
        }
    }

    private View CreateLabelChip(string label)
    {
        var textLabel = new Label
        {
            Text = label,
            FontSize = 16,
            TextColor = Colors.Black,
            VerticalOptions = LayoutOptions.Center
        };

        var removeButton = new Button
        {
            Text = "×",
            WidthRequest = 22,
            HeightRequest = 22,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            CornerRadius = 11,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Colors.Black,
            CommandParameter = label,
            Margin = new Thickness(4, 0, 0, 0)
        };
        removeButton.Clicked += RemoveLabelButton_Clicked;

        var horizontal = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center
        };
        horizontal.Children.Add(textLabel);
        horizontal.Children.Add(removeButton);

        return new Frame
        {
            Padding = new Thickness(14, 8),
            Margin = new Thickness(0, 0, 8, 0),
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            CornerRadius = 20,
            HasShadow = false,
            Content = horizontal
        };
    }

    private void RemoveLabelButton_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button button &&
            button.CommandParameter is string label &&
            currentPhoto != null &&
            currentPhoto.Labels.Contains(label))
        {
            currentPhoto.Labels.Remove(label);
            DisplayLabels(currentPhoto);
        }
    }

    private bool IsSupportedImage(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private void ToggleSidebar_Clicked(object? sender, EventArgs e)
    {
        if (Sidebar != null && MainGrid != null && MainGrid.ColumnDefinitions.Count > 0)
        {
            bool isVisible = Sidebar.IsVisible;
            Sidebar.IsVisible = !isVisible;
            
            // Adjust column width: 220 when open, 0 when closed
            if (isVisible)
            {
                // Sidebar is open, close it
                MainGrid.ColumnDefinitions[0].Width = 0;
            }
            else
            {
                // Sidebar is closed, open it
                MainGrid.ColumnDefinitions[0].Width = 220;
            }
        }
    }

    private void ImportPhoto_Tapped(object? sender, TappedEventArgs e)
    {
        // Call the existing UploadMedia method
        UploadMedia(sender ?? this, EventArgs.Empty);
    }
}
