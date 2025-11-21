using ExifLib;
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
            var pngFileType = new FilePickerFileType(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.iOS, new[] { "public.png" } },
            { DevicePlatform.Android, new[] { "image/png" } },
            { DevicePlatform.WinUI, new[] { ".png" } },
            { DevicePlatform.MacCatalyst, new[] { "png", "PNG" } }
        });

            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = pngFileType,
                PickerTitle = "Select a PNG file"
            });

            if (result != null)
            {
                var filePath = result.FullPath;

                if (!File.Exists(filePath)) {
                    await DisplayAlert("Foutmelding",
                        "Het geselecteerde bestand kon niet worden gevonden.",
                        "OK");
                    return;
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