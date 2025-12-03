using Microsoft.Maui.Controls;

namespace PictureGallery.Views;

public partial class MyMainPage : ContentPage
{
    public MyMainPage()
    {
        InitializeComponent();

        // Standaard Gallery tonen
        Content = new ContentView { Content = new Views.Gallery().Content };
    }

    // Navigatie naar Page1
    void OnPage1Clicked(object sender, EventArgs e)
    {
        Content = new ContentView { Content = new Views.NewPage1().Content };
    }

    // Navigatie naar Gallery
    void OnPage2Clicked(object sender, EventArgs e)
    {
        Content = new ContentView { Content = new Views.Gallery().Content };
    }

    // Navigatie naar PhotoBookPage (kan worden aangeroepen vanuit Gallery)
    public async Task NavigateToPhotoBookAsync()
    {
        if (Navigation != null)
        {
            await Navigation.PushAsync(new PhotoBookPage());
        }
    }
}
