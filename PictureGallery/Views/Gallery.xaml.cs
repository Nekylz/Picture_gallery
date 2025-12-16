using Microsoft.Maui.Controls;
using PictureGallery.ViewModels;

namespace PictureGallery.Views;

public partial class Gallery : ContentPage
{
    public Gallery()
    {
        InitializeComponent();
        BindingContext = new GalleryViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is GalleryViewModel viewModel)
        {
            await viewModel.LoadPhotosAsync();
            
            // Zet BindingContext expliciet op CollectionView zodat RelativeSource in DataTemplate werkt
            if (PhotosCollection != null)
            {
                PhotosCollection.BindingContext = viewModel;
            }
            
            // Zet BindingContext expliciet op FullscreenOverlay zodat alle bindings daar werken
            var fullscreenOverlay = this.FindByName<Grid>("FullscreenOverlay");
            if (fullscreenOverlay != null)
                {
                fullscreenOverlay.BindingContext = viewModel;
            }
        }
    }

    // UI-specific helpers for responsive design - these can stay in code-behind
    private void HeaderGrid_SizeChanged(object? sender, EventArgs e)
    {
        if (sender is Grid headerGrid)
        {
            // Probeer eerst direct named elements (werkt wanneer ContentPage volledig gebruikt wordt)
            var galleryTitle = GalleryTitle ?? this.FindByName<Label>("GalleryTitle");
            var filterLabelButton = FilterLabelButton ?? this.FindByName<Button>("FilterLabelButton");
            var sortDateButton = SortDateButton ?? this.FindByName<Button>("SortDateButton");
            var filterRatingButton = FilterRatingButton ?? this.FindByName<Button>("FilterRatingButton");
            var selectButton = SelectButton ?? this.FindByName<Button>("SelectButton");
            var headerButtonsLayout = HeaderButtonsLayout ?? this.FindByName<HorizontalStackLayout>("HeaderButtonsLayout");

            if (galleryTitle != null)
            {
                double width = headerGrid.Width;

                if (width > 0)
        {
                    if (width < 600)
                    {
                        galleryTitle.FontSize = 24;
                        if (filterLabelButton != null) filterLabelButton.FontSize = 12;
                        if (sortDateButton != null) sortDateButton.FontSize = 12;
                        if (filterRatingButton != null) filterRatingButton.FontSize = 12;
                        if (selectButton != null) selectButton.FontSize = 12;
                        if (headerButtonsLayout != null) headerButtonsLayout.Spacing = 6;
                    }
                    else if (width < 900)
        {
                        galleryTitle.FontSize = 28;
                        if (filterLabelButton != null) filterLabelButton.FontSize = 13;
                        if (sortDateButton != null) sortDateButton.FontSize = 13;
                        if (filterRatingButton != null) filterRatingButton.FontSize = 13;
                        if (selectButton != null) selectButton.FontSize = 13;
                        if (headerButtonsLayout != null) headerButtonsLayout.Spacing = 8;
        }
                    else
                    {
                        galleryTitle.FontSize = 32;
                        if (filterLabelButton != null) filterLabelButton.FontSize = 14;
                        if (sortDateButton != null) sortDateButton.FontSize = 14;
                        if (filterRatingButton != null) filterRatingButton.FontSize = 14;
                        if (selectButton != null) selectButton.FontSize = 14;
                        if (headerButtonsLayout != null) headerButtonsLayout.Spacing = 10;
                    }
                }
            }
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

    private void PhotosCollection_Loaded(object? sender, EventArgs e)
    {
        PhotosCollection_SizeChanged(sender, e);

        // Zet BindingContext expliciet op CollectionView zodat RelativeSource in DataTemplate werkt
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

    private void Grid_SizeChanged(object? sender, EventArgs e)
    {
        if (sender is Grid grid)
        {
            const double aspectRatio = 380.0 / 280.0;

            if (grid.Width > 0 && grid.Height > 0)
            {
                double expectedHeight = grid.Width / aspectRatio;

                if (Math.Abs(grid.Height - expectedHeight) > 1)
            {
                    grid.HeightRequest = expectedHeight;
        }
    }
        }
    }

    private void ToggleSidebar_Clicked(object? sender, EventArgs e)
    {
        if (Sidebar != null && MainGrid != null && MainGrid.ColumnDefinitions.Count > 0)
        {
            bool isVisible = Sidebar.IsVisible;
            Sidebar.IsVisible = !isVisible;
            
            if (isVisible)
            {
                MainGrid.ColumnDefinitions[0].Width = 0;
            }
            else
            {
                MainGrid.ColumnDefinitions[0].Width = 220;
            }
        }
    }
}
