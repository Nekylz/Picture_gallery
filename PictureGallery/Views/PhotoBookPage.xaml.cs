using PictureGallery.ViewModels;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using SkiaSharp;
using System.Linq;
using System.IO;
using System.Threading;

namespace PictureGallery.Views;

public partial class PhotoBookPage : ContentPage
{
    private PhotoBookPageViewModel? ViewModel => BindingContext as PhotoBookPageViewModel;

    public PhotoBookPage() : this(null) { }

    public PhotoBookPage(int? photoBookId)
    {
        InitializeComponent();
        var viewModel = new PhotoBookPageViewModel(photoBookId);
        BindingContext = viewModel;
        
        // Removed CarouselView - all photos on one page
        
        // Subscribe to PhotoBook property changes
        if (viewModel != null)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            viewModel.PhotosDeleted += ViewModel_PhotosDeleted;
            viewModel.PhotosAdded += ViewModel_PhotosAdded;
        }
    }

    private void ViewModel_PhotosAdded()
    {
        System.Diagnostics.Debug.WriteLine("[ViewModel_PhotosAdded] Photos added, forcing full refresh");
        ForceCarouselViewRefresh();
    }

    private void ViewModel_PhotosDeleted()
    {
        System.Diagnostics.Debug.WriteLine("[ViewModel_PhotosDeleted] Photos deleted, forcing full refresh");
        ForceCarouselViewRefresh();
    }

    private void ForceCarouselViewRefresh()
    {
        if (ViewModel?.PhotoBook == null)
        {
            System.Diagnostics.Debug.WriteLine("[ForceCarouselViewRefresh] ViewModel/PhotoBook is null");
            return;
        }

        if (_isRefreshingCarousel || _isRebuildingLayout)
        {
            System.Diagnostics.Debug.WriteLine("[ForceCarouselViewRefresh] Refresh/rebuild already in progress, skipping");
            return;
        }

        _isRefreshingCarousel = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ForceCarouselViewRefresh] Forcing masonry layout rebuild");
                
                // Cancel any pending rebuilds from SizeChanged
                _rebuildDebounceTimer?.Dispose();
                _rebuildDebounceTimer = null;
                
                // Wait for visual tree to be ready, then directly rebuild masonry layout
                await Task.Delay(300);
                
                // Single rebuild attempt - no need for multiple retries
                bool rebuildSuccessful = await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (_isRebuildingLayout)
                    {
                        System.Diagnostics.Debug.WriteLine("[ForceCarouselViewRefresh] Rebuild already in progress, skipping");
                        return false;
                    }
                    
                    // Find PageContainer and rebuild
                    if (PageContainer != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[ForceCarouselViewRefresh] Rebuilding masonry layout");
                        RebuildMasonryLayoutForContainer(PageContainer);
                        return true;
                    }
                    
                    return false;
                });
                
                if (!rebuildSuccessful)
                {
                    System.Diagnostics.Debug.WriteLine("[ForceCarouselViewRefresh] Could not rebuild masonry layout");
                }
                
                // Wait for rebuild to complete before resetting flag
                await Task.Delay(2000); // Give rebuild more time to complete
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _isRefreshingCarousel = false;
                    System.Diagnostics.Debug.WriteLine("[ForceCarouselViewRefresh] Refresh complete - flag reset");
                });
            }
            catch (Exception ex)
            {
                _isRefreshingCarousel = false;
                System.Diagnostics.Debug.WriteLine($"[ForceCarouselViewRefresh] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ForceCarouselViewRefresh] StackTrace: {ex.StackTrace}");
            }
        });
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[ViewModel_PropertyChanged] Property changed: {e.PropertyName}");
        
        // Skip rebuilds if we're already refreshing - PhotosAdded/PhotosDeleted events handle refresh
        if (_isRefreshingCarousel)
        {
            System.Diagnostics.Debug.WriteLine($"[ViewModel_PropertyChanged] Skipping rebuild - refresh already in progress");
            return;
        }
        
        if (e.PropertyName == nameof(PhotoBookPageViewModel.PhotoBook))
        {
            // PhotosAdded/PhotosDeleted events will trigger ForceCarouselViewRefresh
            // which handles the rebuild with proper debouncing
            // So we skip PropertyChanged rebuilds to prevent duplicate rebuilds and flickering
            System.Diagnostics.Debug.WriteLine("[ViewModel_PropertyChanged] PhotoBook changed - PhotosAdded/PhotosDeleted events will handle refresh");
        }
        else if (e.PropertyName == nameof(PhotoBookPageViewModel.IsDeleteMode) ||
                 e.PropertyName == nameof(PhotoBookPageViewModel.IsPdfMode))
        {
            // Mode changes don't need full rebuild - overlays update automatically via binding
            System.Diagnostics.Debug.WriteLine($"[ViewModel_PropertyChanged] {e.PropertyName} changed - no rebuild needed");
        }
    }

    private void RebuildCurrentPageLayoutDebounced()
    {
        if (_isRebuildingLayout)
        {
            System.Diagnostics.Debug.WriteLine("[RebuildCurrentPageLayoutDebounced] Rebuild already in progress, skipping");
            return;
        }

        // Use a small delay to batch multiple rapid rebuilds
        _rebuildDebounceTimer?.Dispose();
        _rebuildDebounceTimer = new System.Threading.Timer(_ =>
        {
            _rebuildDebounceTimer?.Dispose();
            _rebuildDebounceTimer = null;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!_isRebuildingLayout)
                {
                    _isRebuildingLayout = true;
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[RebuildCurrentPageLayoutDebounced] Executing debounced rebuild");
                        RebuildCurrentPageLayout();
                    }
                    finally
                    {
                        // Reset flag after a delay to allow rebuild to complete
                        Task.Delay(500).ContinueWith(__ =>
                        {
                            _isRebuildingLayout = false;
                        });
                    }
                }
            });
        }, null, 150, Timeout.Infinite);
    }

    private void RebuildCurrentPageLayout()
    {
        // All photos on one page - rebuild directly via PageContainer
        if (ViewModel?.PhotoBook == null)
        {
            System.Diagnostics.Debug.WriteLine("[RebuildCurrentPageLayout] ViewModel/PhotoBook is null");
            return;
        }

        if (ViewModel.PhotoBook.Pages.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[RebuildCurrentPageLayout] No pages available");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine("[RebuildCurrentPageLayout] Rebuilding single page layout");
        
        // Rebuild directly via PageContainer
        if (PageContainer != null)
        {
            RebuildMasonryLayoutForContainer(PageContainer);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[RebuildCurrentPageLayout] PageContainer not found");
        }
    }

    private bool TraverseVisualTreeForMasonryRebuild(VisualElement? element)
    {
        if (element == null) return false;
        
        // Check if this element itself is a Grid - check if it has our columns
        if (element is Grid grid)
        {
            var col0 = grid.FindByName<VerticalStackLayout>("Column0");
            var col1 = grid.FindByName<VerticalStackLayout>("Column1");
            var col2 = grid.FindByName<VerticalStackLayout>("Column2");
            if (col0 != null && col1 != null && col2 != null)
            {
                System.Diagnostics.Debug.WriteLine("[TraverseVisualTreeForMasonryRebuild] Found MasonryGrid directly via columns, rebuilding");
                RebuildMasonryLayoutForGrid(grid);
                return true;
            }
        }
        
        // Check if this element is a Border containing MasonryGrid
        if (element is Border border)
        {
            // Try FindByName first
            var masonryGrid = border.FindByName<Grid>("MasonryGrid");
            if (masonryGrid != null)
            {
                System.Diagnostics.Debug.WriteLine("[TraverseVisualTreeForMasonryRebuild] Found MasonryGrid in Border via FindByName, rebuilding");
                RebuildMasonryLayoutForGrid(masonryGrid);
                return true;
            }
            
            // Also check if Border.Content is a Grid with our columns
            if (border.Content is Grid contentGrid)
            {
                var col0 = contentGrid.FindByName<VerticalStackLayout>("Column0");
                var col1 = contentGrid.FindByName<VerticalStackLayout>("Column1");
                var col2 = contentGrid.FindByName<VerticalStackLayout>("Column2");
                if (col0 != null && col1 != null && col2 != null)
                {
                    System.Diagnostics.Debug.WriteLine("[TraverseVisualTreeForMasonryRebuild] Found MasonryGrid via Border.Content columns, rebuilding");
                    RebuildMasonryLayoutForGrid(contentGrid);
                    return true;
                }
            }
        }

        // Recursively check children
        if (element is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is VisualElement visualChild)
                {
                    if (TraverseVisualTreeForMasonryRebuild(visualChild))
                    {
                        return true;
                    }
                }
            }
        }
        
        // Also check Content property for non-Layout elements (e.g., ContentView, ScrollView, etc.)
        if (element is ContentView contentView && contentView.Content is VisualElement contentElement)
        {
            if (TraverseVisualTreeForMasonryRebuild(contentElement))
            {
                return true;
            }
        }
        
        // Check ScrollView.Content
        if (element is ScrollView scrollView && scrollView.Content is VisualElement scrollContent)
        {
            if (TraverseVisualTreeForMasonryRebuild(scrollContent))
            {
                return true;
            }
        }
        
        return false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        System.Diagnostics.Debug.WriteLine("[PhotoBookPage] OnAppearing called");
        
        // Reset flags in case they're stuck from a previous session
        _isRebuildingLayout = false;
        _isRefreshingCarousel = false;
        System.Diagnostics.Debug.WriteLine("[PhotoBookPage] Reset rebuild flags");
        
        if (ViewModel != null)
        {
            await ViewModel.LoadPhotoBookAsync();
            
            // Wait a bit longer to ensure visual tree is ready
            await Task.Delay(800);
            
            // Try multiple times to ensure rebuild happens
            for (int attempt = 0; attempt < 5; attempt++)
            {
                await Task.Delay(300);
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (ViewModel.PhotoBook != null && PageContainer != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PhotoBookPage] PhotoBook loaded with {ViewModel.PhotoBook.Pages.Count} page(s), attempt {attempt + 1}, Rebuilding: {_isRebuildingLayout}, Refreshing: {_isRefreshingCarousel}");
                        
                        // Force reset flags if they've been stuck for too long
                        if (attempt >= 2 && (_isRebuildingLayout || _isRefreshingCarousel))
                        {
                            System.Diagnostics.Debug.WriteLine($"[PhotoBookPage] Force resetting stuck rebuild flags (attempt {attempt + 1})");
                            _isRebuildingLayout = false;
                            _isRefreshingCarousel = false;
                        }
                        
                        // Only trigger rebuild if we're not already rebuilding
                        if (!_isRebuildingLayout && !_isRefreshingCarousel)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PhotoBookPage] Triggering rebuild after load (attempt {attempt + 1})");
                            ForceCarouselViewRefresh();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[PhotoBookPage] Rebuild already in progress, skipping attempt {attempt + 1}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[PhotoBookPage] PhotoBook or PageContainer not ready yet (attempt {attempt + 1}) - PhotoBook: {ViewModel?.PhotoBook != null}, PageContainer: {PageContainer != null}");
                    }
                });
                
                // If rebuild was successful, break out of loop
                if (attempt > 1)
                {
                    await Task.Delay(200); // Give rebuild time to complete
                    if (!_isRefreshingCarousel && !_isRebuildingLayout)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PhotoBookPage] Rebuild completed, breaking out of loop");
                        break;
                    }
                }
            }
        }
    }

    private bool _isRebuildingLayout = false;
    private bool _isRefreshingCarousel = false;
    private System.Threading.Timer? _rebuildDebounceTimer;

    private async void BackButton_Clicked(object? sender, EventArgs e)
    {
        if (Navigation != null && Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
        }
        else if (Application.Current?.MainPage != null)
        {
            if (Application.Current.MainPage.Navigation != null && Application.Current.MainPage.Navigation.NavigationStack.Count > 1)
            {
                await Application.Current.MainPage.Navigation.PopAsync();
            }
        }
    }

    // Removed OnPageChanged - all photos on one page

    private void PageContainer_Loaded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[PageContainer_Loaded] Page container loaded");
        
        // Ignore if rebuild or refresh is already in progress
        if (_isRebuildingLayout || _isRefreshingCarousel)
        {
            System.Diagnostics.Debug.WriteLine("[PageContainer_Loaded] Rebuild/refresh in progress, skipping");
            return;
        }
        
        if (sender is Border border)
        {
            // Only rebuild if PhotoBook is loaded and we're not already rebuilding
            Task.Delay(300).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (ViewModel?.PhotoBook != null && !_isRebuildingLayout && !_isRefreshingCarousel)
                    {
                        System.Diagnostics.Debug.WriteLine("[PageContainer_Loaded] Executing rebuild for loaded container");
                        RebuildMasonryLayoutForContainer(border);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[PageContainer_Loaded] Skipping rebuild - ViewModel: {ViewModel != null}, PhotoBook: {ViewModel?.PhotoBook != null}, Rebuilding: {_isRebuildingLayout}, Refreshing: {_isRefreshingCarousel}");
                    }
                });
            });
        }
    }

    private void MasonryGrid_SizeChanged(object? sender, EventArgs e)
    {
        // Completely ignore SizeChanged events during rebuilds or refreshes to prevent flickering
        if (_isRebuildingLayout || _isRefreshingCarousel)
        {
            System.Diagnostics.Debug.WriteLine("[MasonryGrid_SizeChanged] Rebuild/refresh in progress, ignoring SizeChanged event");
            return;
        }
        
        if (sender is Grid grid && ViewModel != null)
        {
            // Debounce size changed events as they fire frequently, especially when adding multiple photos
            // Increased delay significantly to prevent cascading rebuilds on Windows
            _rebuildDebounceTimer?.Dispose();
            _rebuildDebounceTimer = new System.Threading.Timer(_ =>
            {
                _rebuildDebounceTimer?.Dispose();
                _rebuildDebounceTimer = null;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Double check - rebuild might have started while timer was waiting
                    if (!_isRebuildingLayout && !_isRefreshingCarousel)
                    {
                        System.Diagnostics.Debug.WriteLine("[MasonryGrid_SizeChanged] Executing debounced rebuild");
                        RebuildMasonryLayoutForGrid(grid);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[MasonryGrid_SizeChanged] Rebuild/refresh started during debounce, skipping");
                    }
                });
            }, null, 1000, Timeout.Infinite); // Increased to 1000ms to prevent flickering
        }
    }

    private void RebuildMasonryLayoutForContainer(Border container)
    {
        System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForContainer] ========== START ==========");
        
        if (ViewModel?.PhotoBook == null)
        {
            System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForContainer] ViewModel or PhotoBook is null");
            return;
        }

        // Find the MasonryGrid within the container - retry if not found immediately
        var masonryGrid = container.FindByName<Grid>("MasonryGrid");
        if (masonryGrid == null)
        {
            System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForContainer] MasonryGrid not found immediately, retrying...");
            // Retry after a small delay
            Task.Delay(100).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    masonryGrid = container.FindByName<Grid>("MasonryGrid");
                    if (masonryGrid != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForContainer] MasonryGrid found on retry, rebuilding");
                        RebuildMasonryLayoutForGrid(masonryGrid);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForContainer] MasonryGrid still not found after retry");
                    }
                });
            });
            return;
        }

        System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForContainer] MasonryGrid found, rebuilding immediately");
        RebuildMasonryLayoutForGrid(masonryGrid);
    }

    private void RebuildMasonryLayoutForGrid(Grid masonryGrid)
    {
        System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForGrid] ========== START ==========");
        
        // Prevent multiple simultaneous rebuilds - set flag immediately to block SizeChanged events
        if (_isRebuildingLayout)
        {
            System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForGrid] Rebuild already in progress, skipping");
            return;
        }
        
        if (ViewModel?.PhotoBook == null)
        {
            System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForGrid] ViewModel or PhotoBook is null");
            return;
        }

        // Set rebuild flag immediately to prevent SizeChanged events from triggering cascading rebuilds
        _isRebuildingLayout = true;

        // PhotoCarousel removed - all photos on one page

        // Find columns within the grid
        var column0 = masonryGrid.FindByName<VerticalStackLayout>("Column0");
        var column1 = masonryGrid.FindByName<VerticalStackLayout>("Column1");
        var column2 = masonryGrid.FindByName<VerticalStackLayout>("Column2");

        if (column0 == null || column1 == null || column2 == null)
        {
            System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForGrid] Columns not found on first try - Column0: {0}, Column1: {1}, Column2: {2}. Retrying...", 
                column0 != null, column1 != null, column2 != null);
            
            // Retry after a delay - visual tree might not be fully ready
            Task.Delay(200).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        column0 = masonryGrid.FindByName<VerticalStackLayout>("Column0");
                        column1 = masonryGrid.FindByName<VerticalStackLayout>("Column1");
                        column2 = masonryGrid.FindByName<VerticalStackLayout>("Column2");
                        
                        if (column0 == null || column1 == null || column2 == null)
                        {
                            System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForGrid] Columns still not found after retry - Column0: {0}, Column1: {1}, Column2: {2}", 
                                column0 != null, column1 != null, column2 != null);
                            _isRebuildingLayout = false;
                            return;
                        }
                        
                        System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayoutForGrid] Columns found on retry, building layout");
                        BuildMasonryLayoutWithColumns(masonryGrid, column0, column1, column2);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RebuildMasonryLayoutForGrid] Error during retry: {ex.Message}");
                        _isRebuildingLayout = false;
                    }
                });
            });
            return;
        }
        
        BuildMasonryLayoutWithColumns(masonryGrid, column0, column1, column2);
    }

    private void BuildMasonryLayoutWithColumns(Grid masonryGrid, VerticalStackLayout column0, VerticalStackLayout column1, VerticalStackLayout column2)
    {
        if (ViewModel?.PhotoBook == null)
        {
            System.Diagnostics.Debug.WriteLine("[BuildMasonryLayoutWithColumns] ViewModel or PhotoBook is null");
            return;
        }

        // Get first (and only) page photos - all photos on one page
        var totalPages = ViewModel.PhotoBook.Pages.Count;
        
        System.Diagnostics.Debug.WriteLine($"[BuildMasonryLayoutWithColumns] Total pages: {totalPages} (using first page only)");
        
        // Validate
        if (totalPages == 0)
        {
            System.Diagnostics.Debug.WriteLine("[BuildMasonryLayoutWithColumns] No pages available");
            return;
        }

        var currentPage = ViewModel.PhotoBook.Pages[0]; // Always use first page
        if (currentPage == null)
        {
            System.Diagnostics.Debug.WriteLine($"[BuildMasonryLayoutWithColumns] Current page at index 0 is null");
            return;
        }
        
        if (currentPage.Photos == null)
        {
            System.Diagnostics.Debug.WriteLine($"[BuildMasonryLayoutWithColumns] Current page Photos collection is null");
            return;
        }

            System.Diagnostics.Debug.WriteLine($"[BuildMasonryLayoutWithColumns] Building layout for {currentPage.Photos.Count} photos");

        // Flag is already set in RebuildMasonryLayoutForGrid, but ensure it's set here too for direct calls
        _isRebuildingLayout = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Clear all columns - suppress SizeChanged during clear by setting flag first
                // This prevents cascading rebuilds when children are removed
                column0.Children.Clear();
                column1.Children.Clear();
                column2.Children.Clear();

            // Determine number of columns based on available width
            int numberOfColumns = GetNumberOfColumns(masonryGrid);
            
            // Get column definitions from grid
            var col0 = masonryGrid.ColumnDefinitions.Count > 0 ? masonryGrid.ColumnDefinitions[0] : null;
            var col1 = masonryGrid.ColumnDefinitions.Count > 1 ? masonryGrid.ColumnDefinitions[1] : null;
            var col2 = masonryGrid.ColumnDefinitions.Count > 2 ? masonryGrid.ColumnDefinitions[2] : null;
            
            // Hide unused columns - batch these changes to minimize layout passes
            if (col0 != null && col0.Width != (numberOfColumns > 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(0)))
                col0.Width = numberOfColumns > 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            if (col1 != null && col1.Width != (numberOfColumns > 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(0)))
                col1.Width = numberOfColumns > 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            if (col2 != null && col2.Width != (numberOfColumns > 2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0)))
                col2.Width = numberOfColumns > 2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

            // Calculate column widths - use actual width or fallback
            double gridWidth = masonryGrid.Width > 0 ? masonryGrid.Width : 800; // Fallback width
            double availableWidth = gridWidth - (numberOfColumns - 1) * 15; // Subtract spacing
            double columnWidth = availableWidth > 0 ? availableWidth / numberOfColumns : 200;

            // Distribute photos across columns using masonry algorithm
            var columnHeights = new double[numberOfColumns];
            var columns = new List<VerticalStackLayout> { column0, column1, column2 };

            int photoIndex = 0;
            foreach (var photo in currentPage.Photos)
            {
                // Find column with minimum height
                int targetColumn = 0;
                double minHeight = columnHeights[0];
                for (int i = 1; i < numberOfColumns; i++)
                {
                    if (columnHeights[i] < minHeight)
                    {
                        minHeight = columnHeights[i];
                        targetColumn = i;
                    }
                }

                // Create photo view
                var photoBorder = CreatePhotoView(photo, columnWidth);
                
                if (photoBorder != null)
                {
                    // Add to column - this may trigger SizeChanged, but we ignore it while _isRebuildingLayout is true
                    columns[targetColumn].Children.Add(photoBorder);

                    // Calculate photo height based on aspect ratio
                    double photoHeight = CalculatePhotoHeight(photo, columnWidth);
                    columnHeights[targetColumn] += photoHeight + 15; // Add spacing
                    
                    photoIndex++;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[RebuildMasonryLayout] Failed to create view for photo {photoIndex}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[BuildMasonryLayout] ========== COMPLETE - Added {photoIndex} photos ==========");
            }
            finally
            {
                // Always reset the rebuild flag after a delay to ensure all SizeChanged events have been processed
                // This prevents immediate SizeChanged-triggered rebuilds after we finish
                Task.Delay(2000).ContinueWith(_ =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _isRebuildingLayout = false;
                        System.Diagnostics.Debug.WriteLine("[BuildMasonryLayout] Rebuild flag reset - SizeChanged events will now be processed again");
                    });
                });
            }
        });
    }

    private void RebuildMasonryLayout()
    {
        // Force rebuild of current page's masonry layout
        System.Diagnostics.Debug.WriteLine("[RebuildMasonryLayout] Force rebuilding current page layout");
        RebuildCurrentPageLayout();
    }

    private int GetNumberOfColumns(Grid? masonryGrid)
    {
        if (masonryGrid == null || masonryGrid.Width <= 0)
            return 3; // Default

        double availableWidth = masonryGrid.Width - 60; // Account for padding/margins
        double minColumnWidth = 200; // Minimum column width

        int columns = (int)Math.Floor((availableWidth + 15) / (minColumnWidth + 15));
        return Math.Max(2, Math.Min(3, columns)); // Between 2 and 3 columns
    }

    private double CalculatePhotoHeight(Models.PhotoItem photo, double width)
    {
        if (photo.Width <= 0 || photo.Height <= 0 || width <= 0)
            return width; // Square fallback

        double aspectRatio = (double)photo.Height / photo.Width;
        return width * aspectRatio;
    }

    private View CreatePhotoView(Models.PhotoItem photo, double width)
    {
        if (photo == null)
        {
            System.Diagnostics.Debug.WriteLine("[CreatePhotoView] Photo is null");
            return new Label { Text = "Photo is null", HeightRequest = 100, BackgroundColor = Colors.Red };
        }

        if (photo.ImageSource == null)
        {
            System.Diagnostics.Debug.WriteLine($"[CreatePhotoView] Photo {photo.Id} ({photo.FileName}) has null ImageSource. FilePath: {photo.FilePath}, FileExists: {photo.FileExists}");
            
            // Try to initialize ImageSource if file exists
            if (photo.FileExists && !string.IsNullOrEmpty(photo.FilePath))
            {
                try
                {
                    photo.InitializeImageSource();
                    if (photo.ImageSource == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CreatePhotoView] Failed to initialize ImageSource after retry");
                        return new Label { Text = $"Failed: {photo.FileName}", HeightRequest = 100, BackgroundColor = Colors.Orange };
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreatePhotoView] Exception initializing ImageSource: {ex.Message}");
                    return new Label { Text = $"Error: {ex.Message}", HeightRequest = 100, BackgroundColor = Colors.Red };
                }
            }
            else
            {
                return new Label { Text = $"No Image: {photo.FileName}", HeightRequest = 100, BackgroundColor = Colors.Yellow };
            }
        }

        System.Diagnostics.Debug.WriteLine($"[CreatePhotoView] Creating view for photo {photo.Id} ({photo.FileName}), ImageSource: {photo.ImageSource != null}, Width: {width}");

        var border = new Border
        {
            StrokeThickness = 2,
            Stroke = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(5),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start,
            WidthRequest = width
        };

        var grid = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start
        };

        // Photo Image
        var image = new Image
        {
            Source = photo.ImageSource,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Color.FromArgb("#F0F0F0"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start
        };
        grid.Children.Add(image);

        // Selection overlay - always add it, but control visibility
        var overlay = new Border
        {
            StrokeThickness = 3,
            Stroke = Color.FromArgb("#F44336"),
            BackgroundColor = Color.FromArgb("#40F44336"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            IsVisible = photo.IsSelected && (ViewModel?.IsDeleteMode ?? false)
        };
        
        var checkmark = new Label
        {
            Text = "✓",
            FontSize = 48,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            FontAttributes = FontAttributes.Bold
        };
        
        overlay.Content = checkmark;
        grid.Children.Add(overlay);

        // PDF mode overlay (green)
        var pdfOverlay = new Border
        {
            StrokeThickness = 3,
            Stroke = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#404CAF50"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            IsVisible = photo.IsSelected && (ViewModel?.IsPdfMode ?? false)
        };
        
        var pdfCheckmark = new Label
        {
            Text = "✓",
            FontSize = 48,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            FontAttributes = FontAttributes.Bold
        };
        
        pdfOverlay.Content = pdfCheckmark;
        grid.Children.Add(pdfOverlay);

        border.Content = grid;

        // Subscribe to photo property changes to update overlay visibility
        photo.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(photo.IsSelected))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    overlay.IsVisible = photo.IsSelected && (ViewModel?.IsDeleteMode ?? false);
                    pdfOverlay.IsVisible = photo.IsSelected && (ViewModel?.IsPdfMode ?? false);
                });
            }
        };

        // Add tap gesture - should work in both delete and PDF modes
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[CreatePhotoView] Photo tapped: {photo.FileName}, IsDeleteMode: {ViewModel?.IsDeleteMode}, IsPdfMode: {ViewModel?.IsPdfMode}");
            if (ViewModel != null && (ViewModel.IsDeleteMode || ViewModel.IsPdfMode))
            {
                ViewModel.PhotoTappedCommand?.Execute(photo);
                // Update overlay immediately
                overlay.IsVisible = photo.IsSelected && ViewModel.IsDeleteMode;
                pdfOverlay.IsVisible = photo.IsSelected && ViewModel.IsPdfMode;
            }
        };
        border.GestureRecognizers.Add(tapGesture);

        return border;
    }

    // Removed OnNextPageTapped and OnPrevPageTapped - all photos on one page

    private async void ExportPdf_Clicked(object sender, EventArgs e)
    {
        if (ViewModel == null || ViewModel.PhotoBook == null) return;

        try
        {
            var selectedPhotos = ViewModel.PhotoBook.Pages
                .SelectMany(p => p.Photos)
                .Where(p => p.IsSelected)
                .Select(p => p.FilePath)
                .ToArray();

            if (selectedPhotos.Length == 0)
            {
                await DisplayAlert("No selection", "Select at least one photo.", "OK");
                return;
            }

            var folderResult = await FolderPicker.Default.PickAsync(default);
            if (!folderResult.IsSuccessful) return;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string pdfPath = Path.Combine(folderResult.Folder.Path, $"PhotoBook_{timestamp}.pdf");

            ProgressFrame.IsVisible = true;
            PdfProgressBar.Progress = 0;
            ProgressLabel.Text = "0% (0 of 0 photos)";

            await GeneratePdfWithProgress(selectedPhotos, pdfPath);

            ProgressFrame.IsVisible = false;

            bool share = await DisplayAlert("Success", $"PDF saved in:\n{pdfPath}\n\nDo you want to share the file?", "Share", "Close");

            if (share)
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Share Photo Book PDF",
                    File = new ShareFile(pdfPath)
                });
            }
            else
            {
                await Launcher.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(pdfPath)
                });
            }
        }
        catch (Exception ex)
        {
            ProgressFrame.IsVisible = false;
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            ViewModel?.CancelPdfModeCommand.Execute(null);
        }
    }

    private async Task GeneratePdfWithProgress(string[] images, string outputPath)
    {
        int totalImages = images.Length;
        int processed = 0;
        int skipped = 0;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            PdfProgressBar.Progress = 0;
            ProgressLabel.Text = $"0% (0 of {totalImages} photos)";
        });

        await Task.Delay(100);

        var doc = new PdfDocument();

        foreach (var imgPath in images)
        {
            try
            {
                if (string.IsNullOrEmpty(imgPath) || !File.Exists(imgPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] File does not exist: {imgPath}");
                    skipped++;
                    processed++;
                    continue;
                }

                // Verify file is readable and not empty
                var fileInfo = new FileInfo(imgPath);
                if (fileInfo.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] File is empty: {imgPath}");
                    skipped++;
                    processed++;
                    continue;
                }

                System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] Processing image: {imgPath}");

                // Try to load the image - first try PdfSharpCore directly, then use SkiaSharp as fallback
                XImage? img = null;
                string? tempPngPath = null;
                try
                {
                    // Make sure the path is absolute and accessible
                    var absolutePath = Path.IsPathRooted(imgPath) ? imgPath : Path.GetFullPath(imgPath);
                    
                    System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] Attempting to load image from: {absolutePath}");
                    
                    // Try to load image directly with PdfSharpCore first
                    try
                    {
                        img = XImage.FromFile(absolutePath);
                        
                        // Verify image dimensions
                        if (img != null && img.PixelWidth > 0 && img.PixelHeight > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] Successfully loaded with PdfSharpCore: {absolutePath}");
                        }
                        else
                        {
                            if (img != null)
                            {
                                img.Dispose();
                                img = null;
                            }
                            throw new Exception("Invalid image dimensions");
                        }
                    }
                    catch (Exception pdfEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] PdfSharpCore failed, trying SkiaSharp: {pdfEx.Message}");
                        
                        // Fallback: Use SkiaSharp to decode and convert to temporary PNG file
                        using (var fileStream = File.OpenRead(absolutePath))
                        {
                            using (var skBitmap = SKBitmap.Decode(fileStream))
                            {
                                if (skBitmap == null || skBitmap.Width <= 0 || skBitmap.Height <= 0)
                                {
                                    throw new Exception("SkiaSharp failed to decode image");
                                }
                                
                                // Create temporary PNG file
                                tempPngPath = Path.Combine(Path.GetTempPath(), $"pdf_export_{Guid.NewGuid()}.png");
                                
                                using (var image = SKImage.FromBitmap(skBitmap))
                                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                                {
                                    using (var fileStream2 = File.Create(tempPngPath))
                                    {
                                        data.SaveTo(fileStream2);
                                    }
                                }
                                
                                // Now load the temporary PNG with PdfSharpCore
                                img = XImage.FromFile(tempPngPath);
                                
                                System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] Successfully converted with SkiaSharp and loaded: {absolutePath}");
                            }
                        }
                    }

                    if (img == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] Failed to load image: {absolutePath}");
                        skipped++;
                        processed++;
                        continue;
                    }

                    var page = doc.AddPage();
                    
                    // Calculate page size based on image dimensions and resolution
                    double width = img.PixelWidth;
                    double height = img.PixelHeight;
                    
                    // Handle resolution - if resolution is 0 or invalid, use default 72 DPI
                    double horizontalResolution = img.HorizontalResolution > 0 ? img.HorizontalResolution : 72;
                    double verticalResolution = img.VerticalResolution > 0 ? img.VerticalResolution : 72;
                    
                    // Convert pixels to points (72 points per inch)
                    page.Width = width * 72 / horizontalResolution;
                    page.Height = height * 72 / verticalResolution;
                    
                    // Ensure minimum page size
                    if (page.Width <= 0) page.Width = 612; // 8.5 inches
                    if (page.Height <= 0) page.Height = 792; // 11 inches

                    using (var gfx = XGraphics.FromPdfPage(page))
                    {
                        gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                    }
                    
                    img.Dispose();
                    img = null;
                    
                    // Clean up temporary PNG file if created
                    if (tempPngPath != null && File.Exists(tempPngPath))
                    {
                        try
                        {
                            File.Delete(tempPngPath);
                        }
                        catch (Exception delEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] Failed to delete temp file {tempPngPath}: {delEx.Message}");
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] Successfully added image to PDF: {absolutePath}");
                }
                catch (Exception imgEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] Error loading image {imgPath}: {imgEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] StackTrace: {imgEx.StackTrace}");
                    
                    if (img != null)
                    {
                        try { img.Dispose(); } catch { }
                        img = null;
                    }
                    
                    // Clean up temporary PNG file if created
                    if (tempPngPath != null && File.Exists(tempPngPath))
                    {
                        try
                        {
                            File.Delete(tempPngPath);
                        }
                        catch { }
                    }
                    
                    skipped++;
                    processed++;
                    continue;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeneratePdfWithProgress] Unexpected error processing {imgPath}: {ex.Message}");
                skipped++;
                processed++;
                continue;
            }

            processed++;
            double progress = (double)processed / totalImages;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PdfProgressBar.Progress = progress;
                ProgressLabel.Text = $"{(int)(progress * 100)}% ({processed} of {totalImages} photos)";
            });

            await Task.Delay(50);
        }

        if (skipped > 0)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("Warning", $"{skipped} photo(s) could not be added to the PDF.", "OK");
            });
        }

        if (doc.PageCount == 0)
        {
            doc.Close();
            throw new Exception("No photos could be added to the PDF. Check if the images are valid.");
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ProgressLabel.Text = "Saving file...";
            PdfProgressBar.Progress = 1.0;
        });

        doc.Save(outputPath);
        doc.Close();
    }
}
