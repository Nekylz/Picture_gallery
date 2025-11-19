namespace PictureGallery.Views;

public partial class MyMainPage : ContentPage
{
    public MyMainPage()
    {
        InitializeComponent();
    }

    void OnPage1Clicked(object sender, EventArgs e)
    {
        // Fix: Use a container (e.g., ContentView) to host the ContentPage
        SubPage.Content = new ContentView { Content = new Views.NewPage1().Content };
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

                FileName.Text = $"Selected file: {result.FileName}";
                SelectedImage.Source = ImageSource.FromFile(filePath);
                SelectedImage.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Foutmelding",
                $"Er ging iets mis tijdens het uploaden.\n\nError: {ex.Message}",
                "OK");
        }
    }

}