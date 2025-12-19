using Microsoft.Maui.Controls;
using PictureGallery.ViewModels;

namespace PictureGallery.Views;

public partial class MyMainPage : ContentPage
{
    private Gallery? _currentGallery;

    public MyMainPage()
    {
        InitializeComponent();

        // Standaard Gallery tonen - gebruik direct de ContentPage in plaats van alleen Content
        LoadGallery();
    }

    private async void LoadGallery()
    {
        // Maak nieuwe Gallery aan - behoud de instantie zodat named elements werken
        _currentGallery = new Views.Gallery();
        
        // Gebruik de Content en geef BindingContext door
        if (SubPage != null && _currentGallery != null)
        {
            var content = _currentGallery.Content;
            var viewModel = _currentGallery.BindingContext as GalleryViewModel;
            
            // Zet BindingContext op root level - dit trickle-down automatisch naar alle child views
            if (content is View contentView && viewModel != null)
            {
                contentView.BindingContext = viewModel;
            }
            
            SubPage.Content = content;
            
            // Wacht tot de Content geladen is voordat we de timer starten
            if (content is View contentView2)
            {
                contentView2.Loaded += (s, e) =>
                {
                    // Wacht even en zet dan BindingContext op CollectionView
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Task.Delay(100);
                        try
                        {
                            if (_currentGallery != null && viewModel != null)
                            {
                                var photosCollection = _currentGallery.FindByName<CollectionView>("PhotosCollection");
                                if (photosCollection != null)
                                {
                                    photosCollection.BindingContext = viewModel;
                                }
                                
                                var fullscreenOverlay = _currentGallery.FindByName<Grid>("FullscreenOverlay");
                                if (fullscreenOverlay != null)
                                {
                                    fullscreenOverlay.BindingContext = viewModel;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error setting CollectionView BindingContext: {ex.Message}");
                        }
                    });
                };
            }
            
            // Laad photos direct via ViewModel
            if (viewModel != null)
            {
                try
                {
                    await viewModel.LoadPhotosAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading photos: {ex.Message}");
                }
            }
        }
    }

    // Navigatie naar Page1
    void OnPage1Clicked(object sender, EventArgs e)
    {
        var page1 = new Views.NewPage1();
        if (SubPage != null)
        {
            SubPage.Content = page1.Content;
            if (page1.Content is View contentView && page1.BindingContext != null)
            {
                contentView.BindingContext = page1.BindingContext;
            }
        }
        _currentGallery = null;
    }

    // Navigatie naar Gallery
    void OnPage2Clicked(object sender, EventArgs e)
    {
        LoadGallery();
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
