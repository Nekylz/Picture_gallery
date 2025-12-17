using System;

namespace PictureGallery.Configuration;

/// <summary>
/// Configuration for Mapbox map service
/// To get your Mapbox API key:
/// 1. Go to https://account.mapbox.com/
/// 2. Sign up or log in
/// 3. Navigate to "Access tokens" in your account
/// 4. Copy your default public token or create a new one
/// 5. Set it below or use environment variable
/// </summary>
public static class MapboxConfig
{
    // TODO: Replace with your Mapbox API key or set via environment variable
    // You can get a free API key at: https://account.mapbox.com/
    // Free tier includes 50,000 map loads per month
    private const string DefaultMapboxApiKey = "pk.eyJ1IjoiZGFhbndsdCIsImEiOiJjbWo5eXp3OWUwMWRnM2RzOXcyNTRoZGQ3In0.IMbvf1SbpqdP4894bh1mxw";
    
    /// <summary>
    /// Gets the Mapbox API key from environment variable or default value
    /// Set MAPBOX_API_KEY environment variable to use your key
    /// </summary>
    public static string ApiKey
    {
        get
        {
            var envKey = Environment.GetEnvironmentVariable("MAPBOX_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey) && envKey != DefaultMapboxApiKey)
            {
                return envKey;
            }
            return DefaultMapboxApiKey;
        }
    }
    
    /// <summary>
    /// Checks if a valid API key is configured
    /// Validates that the key is not empty, has minimum length, and starts with "pk." (Mapbox public token)
    /// </summary>
    public static bool HasValidApiKey => !string.IsNullOrWhiteSpace(ApiKey) && 
                                         ApiKey.Length > 10 &&
                                         ApiKey.StartsWith("pk.", StringComparison.Ordinal);
}

