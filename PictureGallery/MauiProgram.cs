using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;

namespace PictureGallery
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit();
            
            // UseMauiMaps on supported platforms
            // For Windows, we'll use WebView with Leaflet maps instead
#if ANDROID || IOS || MACCATALYST
            builder.UseMauiMaps();
#endif
#if WINDOWS
            // Windows: Maps will be handled via WebView with Leaflet
            // No API key required for OpenStreetMap
#endif
            
            builder.ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                // REQUIRED FOR ARROWS
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });
#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
