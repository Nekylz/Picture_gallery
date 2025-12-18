using System;
using System.IO;

namespace PictureGallery.Tests.Helpers;

/// <summary>
/// Base class voor alle tests die file system operaties nodig hebben
/// Zorgt voor cleanup van test data na elke test
/// </summary>
public abstract class TestBase : IDisposable
{
    protected string TestDataDirectory { get; }
    protected string TestDbPath { get; }

    protected TestBase()
    {
        // Maak een unieke test directory aan in temp folder
        TestDataDirectory = Path.Combine(
            Path.GetTempPath(), 
            $"PictureGalleryTests_{Guid.NewGuid()}"
        );
        Directory.CreateDirectory(TestDataDirectory);
        TestDbPath = Path.Combine(TestDataDirectory, "test.db3");
    }

    /// <summary>
    /// Cleanup: verwijder alle test data na de test
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
                // Ignore cleanup errors - bestanden kunnen nog in gebruik zijn
                // Ze worden uiteindelijk opgeruimd door het OS
            }
        }
    }
}

