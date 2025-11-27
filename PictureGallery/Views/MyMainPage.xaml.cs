using Microsoft.Maui.Controls;

namespace PictureGallery.Views;

public partial class MyMainPage : ContentPage
{
    public MyMainPage()
    {
        InitializeComponent();

        // Standaard Gallery tonen
        SubPage.Content = new ContentView { Content = new Views.Gallery().Content };
    }

    // Navigatie naar Page1
    void OnPage1Clicked(object sender, EventArgs e)
    {
        SubPage.Content = new ContentView { Content = new Views.NewPage1().Content };
    }

    // Navigatie naar Gallery
    void OnPage2Clicked(object sender, EventArgs e)
    {
        SubPage.Content = new ContentView { Content = new Views.Gallery().Content };
    }

    // Navigatie naar Fotoboek (PhotoBookPage)
    async void OpenPhotoBook_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new PhotoBookPage());
    }
}
