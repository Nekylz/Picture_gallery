using PictureGallery.ViewModels;

namespace PictureGallery.Views;

public partial class PhotoBookManagementPage : ContentPage
{
    public PhotoBookManagementPage()
    {
        InitializeComponent();
        BindingContext = new PhotoBookManagementViewModel();
        
        // Setup modal events - these need to be wired through the ViewModel
        CreatePhotoBookModal.OnCreate += OnPhotoBookCreated;
        CreatePhotoBookModal.OnCancel += OnPhotoBookCanceled;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is PhotoBookManagementViewModel viewModel)
        {
            await viewModel.LoadPhotoBooksAsync();
            await viewModel.UpdateStatisticsAsync();
        }
    }

    private void OnPhotoBookCreated(object? sender, string result)
    {
        if (BindingContext is PhotoBookManagementViewModel viewModel)
        {
            var parts = result.Split('|');
            if (parts.Length >= 2)
            {
                var name = parts[0].Trim();
                var description = parts[1].Trim();
                viewModel.OnPhotoBookCreated(name, description);
            }
        }
    }

    private void OnPhotoBookCanceled(object? sender, EventArgs e)
    {
        if (BindingContext is PhotoBookManagementViewModel viewModel)
        {
            viewModel.OnPhotoBookCanceled();
        }
    }

    // UI-specific helper for responsive design - can stay in code-behind
    private void PhotoBooksCollection_SizeChanged(object? sender, EventArgs e)
    {
        if (sender is CollectionView collectionView && collectionView.ItemsLayout is Microsoft.Maui.Controls.GridItemsLayout gridLayout)
        {
            double width = collectionView.Width;

            int span = 3; // default

            if (width > 0)
            {
                double availableWidth = width - 40;
                double minCardWidth = 250;
                double spacingPerItem = 16;

                int calculatedSpan = (int)Math.Floor((availableWidth + spacingPerItem) / (minCardWidth + spacingPerItem));

                span = Math.Max(1, Math.Min(3, calculatedSpan));

                if (gridLayout.Span != span)
                {
                    gridLayout.Span = span;
                    System.Diagnostics.Debug.WriteLine($"Updated PhotoBooksCollection Span to {span} for width {width}");
                }
            }
        }
    }
}
