using Microsoft.Maui.Controls;
using PictureGallery.ViewModels;
using PictureGallery.Configuration;

namespace PictureGallery.Views;

public partial class Gallery : ContentPage
{
    private WebView? _webViewMap;
    
    public Gallery()
    {
        InitializeComponent();
        var viewModel = new GalleryViewModel();
        BindingContext = viewModel;
        
        // Abonneer op ViewModel events voor MVVM communicatie
        viewModel.MapLocationUpdateRequested += OnMapLocationUpdateRequested;
        viewModel.RequestShowCreatePhotoBookModal += OnRequestShowCreatePhotoBookModal;

        // Stel modal events in
        CreatePhotoBookModal.OnCreate += OnPhotoBookCreated;
        CreatePhotoBookModal.OnCancel += OnPhotoBookCanceled;

        InitializeMap();
    }

    private void OnRequestShowCreatePhotoBookModal()
    {
        if (BindingContext is GalleryViewModel viewModel)
        {
            CreatePhotoBookModal.Reset();
            viewModel.IsCreatePhotoBookModalVisible = true;
        }
    }

    private void OnPhotoBookCreated(object? sender, string result)
    {
        if (BindingContext is GalleryViewModel viewModel)
        {
            var parts = result.Split('|');
            if (parts.Length >= 2)
            {
                var name = parts[0].Trim();
                var description = parts[1].Trim();
                viewModel.IsCreatePhotoBookModalVisible = false;
                viewModel.OnPhotoBookCreatedForExport(name, description);
            }
        }
    }

    private void OnPhotoBookCanceled(object? sender, EventArgs e)
    {
        if (BindingContext is GalleryViewModel viewModel)
        {
            viewModel.IsCreatePhotoBookModalVisible = false;
        }
    }
    
    private void OnMapLocationUpdateRequested(double lat, double lon)
    {
        UpdateWebMapLocation(lat, lon);
    }
    
    private void InitializeMap()
    {
        if (MapBorder == null || MapPlaceholderLabel == null)
            return;
            
        // Windows en macOS: Gebruik WebView met Mapbox
        try
        {
            _webViewMap = new WebView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Source = new HtmlWebViewSource
                {
                    Html = GetMapboxMapHtml(0, 0) // Standaard naar wereldweergave
                },
                BackgroundColor = Colors.Transparent
            };
            
            // Wacht tot WebView volledig geladen is voordat we het tonen
            _webViewMap.Navigated += (s, e) =>
            {
                if (e.Result == WebNavigationResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine("[WebView Map] Navigation successful");
                    // Korte vertraging om ervoor te zorgen dat content gerenderd is
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            MapPlaceholderLabel.IsVisible = false;
                        });
                    });
                }
            };
            
            MapBorder.Content = _webViewMap;
            MapPlaceholderLabel.IsVisible = true;
            MapPlaceholderLabel.Text = "Loading map...";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing web map: {ex.Message}");
            MapPlaceholderLabel.Text = "Map initialization failed";
            MapPlaceholderLabel.IsVisible = true;
        }
    }
    
    private string GetMapboxMapHtml(double lat, double lon)
    {
        // Standaard naar centrum van Nederland als geen coördinaten gegeven zijn
        if (lat == 0 && lon == 0)
        {
            lat = 52.1326; // Amsterdam
            lon = 5.2913;
        }
        
        var apiKey = MapboxConfig.ApiKey;
        var hasValidKey = MapboxConfig.HasValidApiKey;
        
        if (!hasValidKey)
        {
            // Retourneer HTML met bericht over API key
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            margin: 0;
            padding: 20px;
            font-family: Arial, sans-serif;
            display: flex;
            align-items: center;
            justify-content: center;
            height: 100vh;
            background: #f4f4f4;
        }}
        .message {{
            text-align: center;
            max-width: 400px;
        }}
        .message h3 {{
            color: #333;
            margin-bottom: 10px;
        }}
        .message p {{
            color: #666;
            line-height: 1.6;
        }}
    </style>
</head>
<body>
    <div class='message'>
        <h3>Map API Key Required</h3>
        <p>To display maps, please configure your Mapbox API key in <code>MapboxConfig.cs</code> or set the <code>MAPBOX_API_KEY</code> environment variable.</p>
        <p><a href='https://account.mapbox.com/' target='_blank' style='color: #007bff;'>Get your free API key here</a></p>
    </div>
</body>
</html>";
        }
        
        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <link href='https://api.mapbox.com/mapbox-gl-js/v3.0.1/mapbox-gl.css' rel='stylesheet' />
    <script src='https://api.mapbox.com/mapbox-gl-js/v3.0.1/mapbox-gl.js'></script>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        html, body {{
            margin: 0;
            padding: 0;
            width: 100%;
            height: 100%;
            overflow: hidden;
        }}
        [id='mapdiv'] {{
            width: 100%;
            height: 100%;
            position: absolute;
            top: 0;
            left: 0;
        }}
        .mapboxgl-map {{
            width: 100% !important;
            height: 100% !important;
        }}
    </style>
</head>
<body>
    <div id='mapdiv'></div>
    <script>
        (function() {{
            var mapInitialized = false;
            
            function initMap() {{
                if (mapInitialized) return;
                
                var mapDiv = document.getElementById('mapdiv');
                if (!mapDiv || typeof mapboxgl === 'undefined') {{
                    setTimeout(initMap, 50);
                    return;
                }}
                
                try {{
                    // Zet API key - al geëscapeerd in C# string interpolatie
                    mapboxgl.accessToken = ""{apiKey.Replace("\\", "\\\\").Replace("\"", "\\\"")}"";
                    
                    var map = new mapboxgl.Map({{
                        container: 'mapdiv',
                        style: 'mapbox://styles/mapbox/streets-v12',
                        center: [{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}],
                        zoom: 13
                    }});
                    
                    // Voeg navigatie controls toe
                    map.addControl(new mapboxgl.NavigationControl(), 'top-right');
                    
                    // Zorg dat map correct resize't
                    map.on('load', function() {{
                        map.resize();
                        console.log('Mapbox map loaded successfully');
                    }});
                    
                    // Behandel resize
                    window.addEventListener('resize', function() {{
                        if (map) map.resize();
                    }});
                    
                    mapInitialized = true;
                }} catch (error) {{
                    console.error('Mapbox init error:', error);
                    if (mapDiv) {{
                        mapDiv.innerHTML = '<div style=""padding: 20px; text-align: center;""><h3>Map Error</h3><p>Failed to initialize map. Please check your API key.</p><p>Error: ' + (error.message || 'Unknown error') + '</p></div>';
                    }}
                }}
            }}
            
            // Wacht tot Mapbox GL JS geladen is
            if (document.readyState === 'loading') {{
                document.addEventListener('DOMContentLoaded', function() {{
                    setTimeout(initMap, 100);
                }});
            }} else {{
                setTimeout(initMap, 100);
            }}
            
            window.addEventListener('load', function() {{
                setTimeout(initMap, 200);
            }});
            
            setTimeout(initMap, 300);
        }})();
    </script>
</body>
</html>";
        return htmlContent;
    }
    
    public void UpdateWebMapLocation(double lat, double lon)
    {
        if (_webViewMap != null)
        {
            _webViewMap.Source = new HtmlWebViewSource
            {
                Html = GetMapboxMapHtml(lat, lon)
            };
        }
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
