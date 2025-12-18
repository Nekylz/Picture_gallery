using System;
using System.IO;
using SkiaSharp;

namespace PictureGallery.Tests.Helpers;

/// <summary>
/// Helper methods voor het creëren van test data
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Maakt een test image bestand aan met opgegeven afmetingen
    /// </summary>
    public static string CreateTestImageFile(string directory, string fileName = "test.jpg", int width = 100, int height = 100)
    {
        var filePath = Path.Combine(directory, fileName);
        
        // Maak een eenvoudige test image met SkiaSharp
        using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Blue);
            
            // Teken iets simpel op de canvas
            using (var paint = new SKPaint { Color = SKColors.White })
            {
                canvas.DrawCircle(width / 2, height / 2, Math.Min(width, height) / 4, paint);
            }
            
            // Exporteer als PNG
            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.Create(filePath))
            {
                data.SaveTo(stream);
            }
        }
        
        return filePath;
    }

    /// <summary>
    /// Maakt een leeg test bestand aan
    /// </summary>
    public static string CreateEmptyTestFile(string directory, string fileName = "empty.txt")
    {
        var filePath = Path.Combine(directory, fileName);
        File.WriteAllText(filePath, "");
        return filePath;
    }

    /// <summary>
    /// Maakt een test directory aan
    /// </summary>
    public static string CreateTestDirectory(string parentDirectory, string? name = null)
    {
        var dirName = name ?? $"TestDir_{Guid.NewGuid()}";
        var fullPath = Path.Combine(parentDirectory, dirName);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Wacht even (voor async tests die op file system wachten)
    /// </summary>
    public static async System.Threading.Tasks.Task DelayAsync(int milliseconds = 100)
    {
        await System.Threading.Tasks.Task.Delay(milliseconds);
    }
}

