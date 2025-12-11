using PictureGallery.ViewModels;

namespace PictureGallery.Views;

public partial class PhotoBookPage : ContentPage
{
    public PhotoBookPage() : this(null)
    {
    }

    public PhotoBookPage(int? photoBookId)
    {
        InitializeComponent();
        BindingContext = new PhotoBookPageViewModel(photoBookId);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is PhotoBookPageViewModel viewModel)
        {
            await viewModel.LoadPhotoBookAsync();
        }
    }
}
