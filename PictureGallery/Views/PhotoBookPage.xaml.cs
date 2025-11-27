using Microsoft.Maui.Storage;
using PictureGallery.Models;
using System.Collections.ObjectModel;

namespace PictureGallery.Views;

public partial class PhotoBookPage : ContentPage
{
    public PhotoBook PhotoBook { get; set; } = new();

    public PhotoBookPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Voeg standaard templates toe
        TemplatePicker.ItemsSource = new string[] { "Default", "Modern", "Classic" };
        TemplatePicker.SelectedIndex = 0;
    }

    // FR3: Foto toevoegen
    private async void AddPhoto_Clicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Selecteer foto's voor het fotoboek"
            });

            if (result != null)
            {
                foreach (var file in result)
                {
                    var photo = await CreatePhotoItemAsync(file);
                    if (photo != null)
                    {
                        PhotoBook.Pages.Add(new PhotoBookPageItem
                        {
                            Photo = photo,
                            Title = ""
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fout", ex.Message, "OK");
        }
    }

    // Helper: maak PhotoItem aan
    private async Task<PhotoItem?> CreatePhotoItemAsync(FileResult file)
    {
        if (file == null) return null;

        string filePath = file.FullPath ?? Path.Combine(FileSystem.CacheDirectory, file.FileName);
        if (!File.Exists(filePath))
        {
            await using var stream = await file.OpenReadAsync();
            await using var fs = File.Create(filePath);
            await stream.CopyToAsync(fs);
        }

        return new PhotoItem
        {
            FileName = file.FileName,
            FilePath = filePath,
            ImageSource = ImageSource.FromFile(filePath)
        };
    }

    // FR3: Pagina verwijderen
    private void RemovePage_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is PhotoBookPageItem page)
        {
            PhotoBook.Pages.Remove(page);
        }
    }

    // FR3: Pagina omhoog
    private void MoveUp_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is PhotoBookPageItem page)
        {
            int index = PhotoBook.Pages.IndexOf(page);
            if (index > 0)
            {
                PhotoBook.Pages.Move(index, index - 1);
            }
        }
    }

    // FR3: Pagina omlaag
    private void MoveDown_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is PhotoBookPageItem page)
        {
            int index = PhotoBook.Pages.IndexOf(page);
            if (index < PhotoBook.Pages.Count - 1)
            {
                PhotoBook.Pages.Move(index, index + 1);
            }
        }
    }
}
