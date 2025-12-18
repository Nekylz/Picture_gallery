using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using PictureGallery.Models;
using SQLite;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace PictureGallery.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _database;
    private readonly string _databasePath;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

    public DatabaseService()
    {
        _databasePath = Path.Combine(FileSystem.AppDataDirectory, "picturegallery.db3");
    }

    /// <summary>
    /// Haalt de database connectie op, maakt deze aan als deze nog niet bestaat
    /// </summary>
    private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        if (_database != null)
            return _database;

        await _initLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
        if (_database != null)
            return _database;

        _database = new SQLiteAsyncConnection(_databasePath);
        await InitializeDatabaseAsync();
        return _database;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Initialiseert de database en maakt tabellen aan als ze nog niet bestaan
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        if (_database == null)
            return;

        // Maak tabellen aan als ze nog niet bestaan
        await _database.CreateTableAsync<PhotoItem>();
        await _database.CreateTableAsync<PhotoLabel>();
        await _database.CreateTableAsync<PhotoBook>();
        
        System.Diagnostics.Debug.WriteLine($"Database initialized at: {_databasePath}");
    }

    // ========== PHOTO OPERATIONS ==========

    /// <summary>
    /// Voegt een nieuwe foto toe aan de database
    /// </summary>
    public async Task<int> AddPhotoAsync(PhotoItem photo)
    {
        var db = await GetDatabaseAsync();
        photo.CreatedDate = DateTime.Now;
        var result = await db.InsertAsync(photo);
        // InsertAsync retourneert het aantal aangepaste rijen, maar met AutoIncrement
        // wordt de Id property automatisch ingesteld op het photo object
        System.Diagnostics.Debug.WriteLine($"Photo saved to database: Id={photo.Id}, FileName={photo.FileName}, FilePath={photo.FilePath}");
        return photo.Id;
    }

    /// <summary>
    /// Get all photos from the database (excludes photos that are only in PhotoBooks)
    /// </summary>
    public async Task<List<PhotoItem>> GetAllPhotosAsync()
    {
        var db = await GetDatabaseAsync();
        // Only get photos that are NOT in a PhotoBook (PhotoBookId is null or 0)
        var photos = await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId == null || p.PhotoBookId == 0)
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();
        
        System.Diagnostics.Debug.WriteLine($"Loaded {photos.Count} photos from database");
        
        // Laad labels en initialiseer ImageSource voor elke foto
        // Filter out photos that cannot be loaded
        var validPhotos = new List<PhotoItem>();
        foreach (var photo in photos)
        {
            await LoadLabelsForPhotoAsync(photo);
            
            // Stel alleen ImageSource in als bestand bestaat
            if (photo.FileExists)
            {
                photo.InitializeImageSource();
                
                // Only include photos with valid ImageSource
                if (photo.ImageSource != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Photo loaded: Id={photo.Id}, FileName={photo.FileName}, FileExists={photo.FileExists}, ImageSource={photo.ImageSource != null}");
                    validPhotos.Add(photo);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Photo skipped: Id={photo.Id}, FileName={photo.FileName} - ImageSource could not be created");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Photo file missing: Id={photo.Id}, FileName={photo.FileName}, FilePath={photo.FilePath}");
            }
        }

        return validPhotos;
    }

    /// <summary>
    /// Get a photo by ID
    /// </summary>
    public async Task<PhotoItem?> GetPhotoByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        var photo = await db.Table<PhotoItem>().Where(p => p.Id == id).FirstOrDefaultAsync();
        
        if (photo != null)
        {
            await LoadLabelsForPhotoAsync(photo);
            
            // Only set ImageSource if file exists
            if (photo.FileExists)
            {
                photo.InitializeImageSource();
            }
        }

        return photo;
    }

    /// <summary>
    /// Update an existing photo
    /// </summary>
    public async Task<int> UpdatePhotoAsync(PhotoItem photo)
    {
        var db = await GetDatabaseAsync();
        return await db.UpdateAsync(photo);
    }

    /// <summary>
    /// Delete a photo, all its labels, and the file from disk
    /// </summary>
    public async Task<int> DeletePhotoAsync(PhotoItem photo)
    {
        var db = await GetDatabaseAsync();
        
        // Delete all labels for this photo first
        await db.Table<PhotoLabel>().DeleteAsync(l => l.PhotoId == photo.Id);
        
        // Delete the photo file from disk
        if (!string.IsNullOrEmpty(photo.FilePath) && File.Exists(photo.FilePath))
        {
            try
            {
                File.Delete(photo.FilePath);
                System.Diagnostics.Debug.WriteLine($"DeletePhotoAsync: Deleted file {photo.FilePath} for photo {photo.Id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeletePhotoAsync: Error deleting file {photo.FilePath}: {ex.Message}");
                // Continue with database deletion even if file deletion fails
            }
        }
        
        // Delete the photo from database
        var result = await db.DeleteAsync(photo);
        System.Diagnostics.Debug.WriteLine($"DeletePhotoAsync: Deleted photo {photo.Id} from database");
        
        return result;
    }

    /// <summary>
    /// Delete a photo by ID
    /// </summary>
    public async Task<int> DeletePhotoByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        
        // Delete all labels for this photo first
        await db.Table<PhotoLabel>().DeleteAsync(l => l.PhotoId == id);
        
        // Delete the photo
        return await db.Table<PhotoItem>().DeleteAsync(p => p.Id == id);
    }

    // Labels

    /// <summary>
    /// Voegt een label toe aan een foto (hoofdletterongevoelig)
    /// Retourneert het label ID als succesvol, of 0 als label al bestaat
    /// </summary>
    public async Task<int> AddLabelAsync(int photoId, string labelText)
    {
        var db = await GetDatabaseAsync();
        
        // Haal alle labels voor deze foto op en controleer hoofdletterongevoelig
        var existingLabels = await db.Table<PhotoLabel>()
            .Where(l => l.PhotoId == photoId)
            .ToListAsync();

        // Controleer of label al bestaat (hoofdletterongevoelig)
        // Gebruik StringComparison.OrdinalIgnoreCase voor case-insensitive vergelijking
        var existingLabel = existingLabels
            .FirstOrDefault(l => string.Equals(l.LabelText, labelText, StringComparison.OrdinalIgnoreCase));

        if (existingLabel != null)
        {
            System.Diagnostics.Debug.WriteLine($"Label '{labelText}' already exists for photo {photoId} (existing: '{existingLabel.LabelText}')");
            return 0; // Label bestaat al (hoofdletterongevoelig)
        }

        var label = new PhotoLabel
        {
            PhotoId = photoId,
            LabelText = labelText,
            CreatedDate = DateTime.Now
        };

        var result = await db.InsertAsync(label);
        System.Diagnostics.Debug.WriteLine($"Label '{labelText}' added successfully with ID: {label.Id}, InsertAsync returned: {result}");
        
        // Retourneer het label ID (label.Id wordt ingesteld door InsertAsync voor AutoIncrement)
        // Als beide 0 zijn, retourneer 0 om failure aan te geven (niet 1)
        return label.Id > 0 ? label.Id : result;
    }

    /// <summary>
    /// Get all labels for a specific photo
    /// </summary>
    public async Task<List<PhotoLabel>> GetLabelsForPhotoAsync(int photoId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PhotoLabel>()
            .Where(l => l.PhotoId == photoId)
            .OrderBy(l => l.LabelText)
            .ToListAsync();
    }

    /// <summary>
    /// Load labels into a photo's Labels collection
    /// </summary>
    public async Task LoadLabelsForPhotoAsync(PhotoItem photo)
    {
        try
        {
            var labels = await GetLabelsForPhotoAsync(photo.Id);
            
            // Clear en add op main thread voor thread safety met ObservableCollection
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                photo.Labels.Clear();
                foreach (var label in labels)
                {
                    photo.Labels.Add(label.LabelText);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading labels for photo {photo.Id}: {ex.Message}");
            // Zet lege collectie bij error
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                photo.Labels.Clear();
            });
        }
    }

    /// <summary>
    /// Verwijdert een label van een foto (hoofdletterongevoelig)
    /// </summary>
    public async Task<int> RemoveLabelAsync(int photoId, string labelText)
    {
        var db = await GetDatabaseAsync();
        
        // Haal alle labels voor deze foto op
        var labels = await db.Table<PhotoLabel>()
            .Where(l => l.PhotoId == photoId)
            .ToListAsync();
        
        // Zoek label hoofdletterongevoelig
        // Gebruik StringComparison.OrdinalIgnoreCase voor case-insensitive vergelijking
        var labelToDelete = labels
            .FirstOrDefault(l => string.Equals(l.LabelText, labelText, StringComparison.OrdinalIgnoreCase));
        
        if (labelToDelete != null)
        {
            return await db.DeleteAsync(labelToDelete);
        }
        
        return 0;
    }

    /// <summary>
    /// Verwijdert een label van alle foto's en uit de database
    /// </summary>
    /// <param name="labelText"></param>
    /// <returns></returns>
    public async Task<int> DeleteLabelFromAllPhotosAsync(string labelText)
    {
        var db = await GetDatabaseAsync();

        // Haal alle labels op en filter hoofdletterongevoelig
        var allLabels = await db.Table<PhotoLabel>().ToListAsync();
        var matchingLabels = allLabels
            .Where(l => string.Equals(l.LabelText, labelText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!matchingLabels.Any())
            return 0;

        // Verwijder het label van alle foto's door elke matching label te verwijderen
        int deletedCount = 0;
        foreach (var label in matchingLabels)
        {
            deletedCount += await db.DeleteAsync(label);
        }
        return deletedCount;
    }

    /// <summary>
    /// Remove a label by ID
    /// </summary>
    public async Task<int> RemoveLabelByIdAsync(int labelId)
    {
        var db = await GetDatabaseAsync();
        return await db.DeleteAsync<PhotoLabel>(labelId);
    }

    /// <summary>
    /// Haalt alle unieke labels op van alle foto's (hoofdletterongevoelig distinct)
    /// </summary>
    public async Task<List<string>> GetAllUniqueLabelsAsync()
    {
        var db = await GetDatabaseAsync();
        var labels = await db.Table<PhotoLabel>().ToListAsync();
        
        // Groepeer op hoofdletterongevoelige label tekst en neem het eerste voorkomen
        // Gebruik StringComparer.OrdinalIgnoreCase voor case-insensitive groepering
        var uniqueLabels = labels
            .GroupBy(l => l.LabelText, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First().LabelText) // Behoud originele hoofdletters van eerste voorkomen
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        return uniqueLabels;
    }

    /// <summary>
    /// Haalt alle foto's op met een specifiek label (hoofdletterongevoelig)
    /// </summary>
    public async Task<List<PhotoItem>> GetPhotosByLabelAsync(string labelText)
    {
        var db = await GetDatabaseAsync();
        
        // Haal alle labels op en filter hoofdletterongevoelig
        // We doen dit in memory omdat SQLite case-sensitive is en we case-insensitive willen filteren
        var allLabels = await db.Table<PhotoLabel>().ToListAsync();
        var matchingLabels = allLabels
            .Where(l => string.Equals(l.LabelText, labelText, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        var photoIds = matchingLabels.Select(l => l.PhotoId).ToList();

        if (!photoIds.Any())
            return new List<PhotoItem>();

        // Haal foto's op met die IDs, gesorteerd op nieuwste eerst
        var photos = await db.Table<PhotoItem>()
            .Where(p => photoIds.Contains(p.Id))
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();

        // Laad labels en initialiseer ImageSource voor elke foto
        // Filter out photos that cannot be loaded
        var validPhotos = new List<PhotoItem>();
        foreach (var photo in photos)
        {
            await LoadLabelsForPhotoAsync(photo);
            
            // Stel alleen ImageSource in als bestand bestaat
            if (photo.FileExists)
            {
                photo.InitializeImageSource();
                
                // Only include photos with valid ImageSource
                if (photo.ImageSource != null)
                {
                    validPhotos.Add(photo);
                }
            }
        }

        return validPhotos;
    }

    // UTILS

    /// <summary>
    /// Get the total number of photos (excludes photos in PhotoBooks)
    /// </summary>
    public async Task<int> GetPhotoCountAsync()
    {
        var db = await GetDatabaseAsync();
        // Only count photos that are NOT in a PhotoBook (PhotoBookId is null or 0)
        return await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId == null || p.PhotoBookId == 0)
            .CountAsync();
    }

    /// <summary>
    /// Get the total number of photos that are in PhotoBooks
    /// </summary>
    public async Task<int> GetPhotoBookPhotoCountAsync()
    {
        var db = await GetDatabaseAsync();
        // Only count photos that ARE in a PhotoBook (PhotoBookId is not null and not 0)
        return await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId != null && p.PhotoBookId != 0)
            .CountAsync();
    }

    /// <summary>
    /// Delete all photos from the database (use with caution!)
    /// </summary>
    public async Task DeleteAllPhotosAsync()
    {
        var db = await GetDatabaseAsync();
        // Delete all labels first
        await db.DeleteAllAsync<PhotoLabel>();
        // Then delete all photos
        await db.DeleteAllAsync<PhotoItem>();
        System.Diagnostics.Debug.WriteLine("All photos deleted from database");
    }

    /// <summary>
    /// Clear all data from the database (use with caution!)
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAllAsync<PhotoLabel>();
        await db.DeleteAllAsync<PhotoItem>();
        await db.DeleteAllAsync<PhotoBook>();
    }

   // PHOTOBOOK

    /// <summary>
    /// Voegt een nieuwe PhotoBook toe aan de database
    /// </summary>
    public async Task<int> AddPhotoBookAsync(PhotoBook photoBook)
    {
        var db = await GetDatabaseAsync();
        photoBook.CreatedDate = DateTime.Now;
        photoBook.UpdatedDate = DateTime.Now;
        var result = await db.InsertAsync(photoBook);
        System.Diagnostics.Debug.WriteLine($"PhotoBook saved to database: Id={photoBook.Id}, Name={photoBook.Name}");
        return photoBook.Id;
    }

    /// <summary>
    /// Haalt alle PhotoBooks op uit de database
    /// </summary>
    public async Task<List<PhotoBook>> GetAllPhotoBooksAsync()
    {
        var db = await GetDatabaseAsync();
        var photoBooks = await db.Table<PhotoBook>().OrderByDescending(pb => pb.UpdatedDate).ToListAsync();
        
        System.Diagnostics.Debug.WriteLine($"Loaded {photoBooks.Count} PhotoBooks from database");
        return photoBooks;
    }

    /// <summary>
    /// Haalt een PhotoBook op basis van ID op
    /// </summary>
    public async Task<PhotoBook?> GetPhotoBookByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PhotoBook>().Where(pb => pb.Id == id).FirstOrDefaultAsync();
    }
    
    /// <summary>
    /// Haalt de eerste foto op die bij een PhotoBook hoort (voor thumbnail)
    /// </summary>
    public async Task<PhotoItem?> GetFirstPhotoByPhotoBookIdAsync(int photoBookId)
    {
        var db = await GetDatabaseAsync();
        var photo = await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId == photoBookId)
            .OrderBy(p => p.CreatedDate)
            .FirstOrDefaultAsync();
        
        if (photo != null && photo.FileExists)
        {
            photo.InitializeImageSource();
        }
        
        return photo;
    }

    /// <summary>
    /// Haalt alle foto's op die bij een PhotoBook horen
    /// </summary>
    public async Task<List<PhotoItem>> GetPhotosByPhotoBookIdAsync(int photoBookId)
    {
        System.Diagnostics.Debug.WriteLine($"[GetPhotosByPhotoBookIdAsync] START - PhotoBookId: {photoBookId}");
        
        var db = await GetDatabaseAsync();
        
        // Step 1: Load photos from database
        var photos = await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId == photoBookId)
            .OrderBy(p => p.CreatedDate)
            .ToListAsync();
        
        System.Diagnostics.Debug.WriteLine($"[GetPhotosByPhotoBookIdAsync] Found {photos.Count} photos in database for PhotoBook {photoBookId}");
        
        // Step 2: Load labels and initialize ImageSource for each photo
        var validPhotos = new List<PhotoItem>();
        
        foreach (var photo in photos)
        {
            System.Diagnostics.Debug.WriteLine($"[GetPhotosByPhotoBookIdAsync] Processing photo {photo.Id}: FileName={photo.FileName}, FilePath={photo.FilePath}");
            
            // Load labels
            await LoadLabelsForPhotoAsync(photo);
            
            // Check if file exists
            if (!photo.FileExists)
            {
                System.Diagnostics.Debug.WriteLine($"[GetPhotosByPhotoBookIdAsync] SKIP photo {photo.Id} - File does not exist at path: {photo.FilePath}");
                continue;
            }
            
            // Initialize ImageSource (this is critical for UI display)
            try
            {
                photo.InitializeImageSource();
                
                if (photo.ImageSource == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[GetPhotosByPhotoBookIdAsync] SKIP photo {photo.Id} - ImageSource is null after initialization");
                    continue;
                }
                
                System.Diagnostics.Debug.WriteLine($"[GetPhotosByPhotoBookIdAsync] SUCCESS photo {photo.Id} - ImageSource initialized: {photo.ImageSource != null}");
                validPhotos.Add(photo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetPhotosByPhotoBookIdAsync] ERROR initializing ImageSource for photo {photo.Id}: {ex.Message}");
                continue;
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[GetPhotosByPhotoBookIdAsync] COMPLETE - Returning {validPhotos.Count} valid photos out of {photos.Count} total");
        
        return validPhotos;
    }

    /// <summary>
    /// Werkt een bestaande PhotoBook bij
    /// </summary>
    public async Task<int> UpdatePhotoBookAsync(PhotoBook photoBook)
    {
        var db = await GetDatabaseAsync();
        photoBook.UpdatedDate = DateTime.Now;
        return await db.UpdateAsync(photoBook);
    }

    /// <summary>
    /// Verwijdert een PhotoBook uit de database en alle foto's die erbij horen
    /// </summary>
    public async Task<int> DeletePhotoBookAsync(PhotoBook photoBook)
    {
        var db = await GetDatabaseAsync();
        
        // First, delete all photos that belong to this PhotoBook
        var photos = await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId == photoBook.Id)
            .ToListAsync();
        
        System.Diagnostics.Debug.WriteLine($"DeletePhotoBookAsync: Deleting {photos.Count} photos from PhotoBook {photoBook.Id}");
        
        foreach (var photo in photos)
        {
            // Delete labels first
            await db.Table<PhotoLabel>().DeleteAsync(l => l.PhotoId == photo.Id);
            
            // Delete the photo file from disk
            if (!string.IsNullOrEmpty(photo.FilePath) && File.Exists(photo.FilePath))
            {
                try
                {
                    File.Delete(photo.FilePath);
                    System.Diagnostics.Debug.WriteLine($"DeletePhotoBookAsync: Deleted file {photo.FilePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DeletePhotoBookAsync: Error deleting file {photo.FilePath}: {ex.Message}");
                }
            }
            
            // Delete the photo from database
            await db.DeleteAsync(photo);
        }
        
        // Finally, delete the PhotoBook itself
        var result = await db.DeleteAsync(photoBook);
        System.Diagnostics.Debug.WriteLine($"DeletePhotoBookAsync: Deleted PhotoBook {photoBook.Id}");
        
        return result;
    }

    /// <summary>
    /// Verwijdert een PhotoBook op basis van ID
    /// </summary>
    public async Task<int> DeletePhotoBookByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PhotoBook>().DeleteAsync(pb => pb.Id == id);
    }

    /// <summary>
    /// Haalt het totaal aantal PhotoBooks op
    /// </summary>
    public async Task<int> GetPhotoBookCountAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PhotoBook>().CountAsync();
    }
}

