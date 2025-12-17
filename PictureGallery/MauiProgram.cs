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
            
            // Maps are handled via WebView with Mapbox on Windows and macOS
            
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
