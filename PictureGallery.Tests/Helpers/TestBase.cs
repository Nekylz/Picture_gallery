using System;
using System.IO;

namespace PictureGallery.Tests.Helpers;

/// <summary>
/// Base class for all tests that require file system operations
/// Ensures cleanup of test data after each test
/// </summary>
public abstract class TestBase : IDisposable
{
    protected string TestDataDirectory { get; }
    protected string TestDbPath { get; }

    protected TestBase()
    {
        // Create a unique test directory in temp folder
        TestDataDirectory = Path.Combine(
            Path.GetTempPath(), 
            $"PictureGalleryTests_{Guid.NewGuid()}"
        );
        Directory.CreateDirectory(TestDataDirectory);
        TestDbPath = Path.Combine(TestDataDirectory, "test.db3");
    }

    /// <summary>
    /// Cleanup: delete all test data after the test
    /// </summary>
    public virtual void Dispose()
    {
        if (Directory.Exists(TestDataDirectory))
        {
            try
            {
                Directory.Delete(TestDataDirectory, true);
            }
            catch (IOException)
            {
                // Ignore cleanup errors - files may still be in use
                // They will eventually be cleaned up by the OS
            }
        }
    }
}

