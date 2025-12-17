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
            
            // UseMauiMaps only on platforms that support it without API key requirement
            // Windows requires Bing Maps API key, so we conditionally exclude it
#if ANDROID || IOS || MACCATALYST
            builder.UseMauiMaps();
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
