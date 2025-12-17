using PictureGallery.ViewModels;

namespace PictureGallery.Views;

public partial class SelectPhotosFromGalleryPage : ContentPage
{
    public SelectPhotosFromGalleryPage(SelectPhotosFromGalleryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void PhotosCollection_Loaded(object? sender, EventArgs e)
    {
        PhotosCollection_SizeChanged(sender, e);

        // Set BindingContext explicitly on CollectionView so RelativeSource in DataTemplate works
        if (PhotosCollection != null && BindingContext != null)
        {
            PhotosCollection.BindingContext = BindingContext;
        }

        try
        {
            if (Microsoft.Maui.Devices.DeviceInfo.Platform == Microsoft.Maui.Devices.DevicePlatform.WinUI)
            {
                Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
                {
                    var currentSource = PhotosCollection.ItemsSource;
                    PhotosCollection.ItemsSource = null;
                    PhotosCollection.ItemsSource = currentSource;
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in PhotosCollection_Loaded: {ex.Message}");
        }
    }

    private void PhotosCollection_SizeChanged(object? sender, EventArgs e)
    {
        if (sender is CollectionView collectionView && collectionView.ItemsLayout is Microsoft.Maui.Controls.GridItemsLayout gridLayout)
        {
            double width = collectionView.Width;

            int span = 4;

            if (width > 0)
            {
                double availableWidth = width - 40;
                double minThumbnailWidth = 150;
                double spacingPerItem = 16;

                int calculatedSpan = (int)Math.Floor((availableWidth + spacingPerItem) / (minThumbnailWidth + spacingPerItem));

                span = Math.Max(2, Math.Min(4, calculatedSpan));

                if (gridLayout.Span != span)
                {
                    gridLayout.Span = span;
                    System.Diagnostics.Debug.WriteLine($"Updated GridItemsLayout Span to {span} for width {width}");
                }
            }
        }
    }

    private void Grid_SizeChanged(object? sender, EventArgs e)
    {
        if (sender is Grid grid)
        {
            const double aspectRatio = 380.0 / 280.0; // Same aspect ratio as Gallery

            if (grid.Width > 0)
            {
                double expectedHeight = grid.Width / aspectRatio;
                grid.HeightRequest = expectedHeight;
            }
        }
    }
}

