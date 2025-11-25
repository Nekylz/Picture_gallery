using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using SkiaSharp;

namespace PictureGallery.Views;

public partial class Gallery : ContentPage
{
	public Gallery()
	{
		InitializeComponent();
	}

    private string fileNameText = string.Empty;
    private string imgDimensionsText = string.Empty;
    private string fileSizeText = string.Empty;

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

            FileResult? result = null;

            try
            {
                result = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    return await FilePicker.Default.PickAsync(new PickOptions
                    {
                        FileTypes = FilePickerFileType.Images,
                        PickerTitle = "Select an image"
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

            if (result != null)
            {
                if (!Path.GetExtension(result.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    await DisplayAlert("Ongeldig bestand", "Selecteer een PNG-bestand.", "OK");
                    return;
                }
                var filePath = result.FullPath;

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    // On iOS/MacCatalyst the picker often returns a stream without a filesystem path.
                    await using var pickedStream = await result.OpenReadAsync();
                    filePath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid()}_{result.FileName}");
                    await using var tempFile = File.Create(filePath);
                    await pickedStream.CopyToAsync(tempFile);
                }

                //FileName.Text = $"Selected file: {result.FileName}";
                fileNameText = $"Selected file: {result.FileName}";
                SelectedImage.Source = ImageSource.FromFile(filePath);
                SelectedImage.IsVisible = true;

                // Get image dimensions for PNG
                int width = 0;
                int height = 0;
                using (var stream = File.OpenRead(filePath))
                {
                    using (var bitmap = SKBitmap.Decode(stream))
                    {
                        if (bitmap != null)
                        {
                            width = bitmap.Width;
                            height = bitmap.Height;
                        }
                    }
                }

                var imageDimensions = (width, height);

                // Get file size in bytes
                long fileSize = new FileInfo(filePath).Length;

                // Convert to mb as a decimal
                double fileSizeMB = fileSize / (1024.0 * 1024.0);

                //PhotoDimensions.Text = $"Image Dimensions: {imageDimensions.width} x {imageDimensions.height}";
                imgDimensionsText = $"Image Dimensions: {imageDimensions.width} x {imageDimensions.height}";
                //FileSize.Text = $"File Size: {fileSizeMB:F1} MB";
                fileSizeText = $"File Size: {fileSizeMB:F1} MB";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Foutmelding",
                $"Er ging iets mis tijdens het uploaden.\n\nError: {ex.Message}",
                "OK");
        }
    }
    /// <summary>
    /// Handles the event triggered when the fullscreen button is pressed.
    /// Sets the source of the fullscreen image to the selected image and makes the fullscreen overlay visible.
    /// </summary>
    private void FullscreenButton(object sender, EventArgs e)
    {
        if (SelectedImage.Source != null)
        {
            FullscreenImage.Source = SelectedImage.Source;

            // NEW: Update fullscreen labels
            OverlayFileName.Text = fileNameText;
            OverlayDimensions.Text = imgDimensionsText;
            OverlayFileSize.Text = fileSizeText;

            FullscreenOverlay.IsVisible = true;
        }
    }
     /// <summary>
     /// Closes the fullscreen overlay.
     /// </summary>
    private void CloseFullscreen(object sender, EventArgs e)
    {
        FullscreenOverlay.IsVisible = false;
    }

}