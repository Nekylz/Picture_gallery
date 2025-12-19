using System;
using System.IO;
using SkiaSharp;

namespace PictureGallery.Tests.Helpers;

/// <summary>
/// Helper methods for creating test data
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a test image file with specified dimensions
    /// </summary>
    public static string CreateTestImageFile(string directory, string fileName = "test.jpg", int width = 100, int height = 100)
    {
        var filePath = Path.Combine(directory, fileName);

        // Create a simple test image with SkiaSharp
        using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Blue);

            // Draw something simple on the canvas
            using (var paint = new SKPaint { Color = SKColors.White })
            {
                canvas.DrawCircle(width / 2, height / 2, Math.Min(width, height) / 4, paint);
            }

            // Export as PNG
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
    /// Creates an empty test file
    /// </summary>
    public static string CreateEmptyTestFile(string directory, string fileName = "empty.txt")
    {
        var filePath = Path.Combine(directory, fileName);
        File.WriteAllText(filePath, "");
        return filePath;
    }

    /// <summary>
    /// Creates a test directory
    /// </summary>
    public static string CreateTestDirectory(string parentDirectory, string? name = null)
    {
        var dirName = name ?? $"TestDir_{Guid.NewGuid()}";
        var fullPath = Path.Combine(parentDirectory, dirName);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Waits for a moment (for async tests that wait on file system)
    /// </summary>
    public static async System.Threading.Tasks.Task DelayAsync(int milliseconds = 100)
    {
        await System.Threading.Tasks.Task.Delay(milliseconds);
    }
}

