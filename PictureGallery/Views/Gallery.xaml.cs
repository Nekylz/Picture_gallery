namespace PictureGallery.Views;

public partial class Gallery : ContentPage
{
	public Gallery()
	{
		InitializeComponent();
	}

    /// <summary>
    /// Handles the event triggered to upload a media file by allowing the user to select a PNG file from the device.
    /// </summary>
    /// <remarks>This method opens a file picker dialog to allow the user to select a PNG file. The supported
    /// file types vary by platform: iOS supports "public.png", Android supports "image/png", WinUI supports ".png", and
    /// macOS supports "png". If a file is selected, its full path is retrieved, and the file name is displayed in the
    /// associated UI element.</remarks>
    private async void UploadMedia(object sender, EventArgs e)
    {
        var pngFileType = new FilePickerFileType(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.iOS, new[] { "public.png" } },
            { DevicePlatform.Android, new[] { "image/png" } },
            { DevicePlatform.WinUI, new[] { ".png" } },
            { DevicePlatform.MacCatalyst, new[] { "png", "PNG" } } //Handles uppercase files too more robust
        });

        var result = await FilePicker.PickAsync(new PickOptions
        {
            FileTypes = pngFileType,
            PickerTitle = "Select a PNG file"
        });

        if (result != null)
        {
            // Do something with the selected PNG file
            var filePath = result.FullPath;

            FileName.Text = $"Selected file: {result.FileName}";
            // Show the image
            SelectedImage.Source = ImageSource.FromFile(filePath);
            SelectedImage.IsVisible = true;
        }
    }
}