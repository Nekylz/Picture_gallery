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
    /// Retrieves the SQLite database connection, initializing it if necessary
    /// </summary>
    private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        if (_database != null)
            return _database;

        await _initLock.WaitAsync();
        try
        {
            // Double-check after acquiring the lock
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
    /// Initializes the database and creates tables if they don't exist
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        if (_database == null)
            return;

        // Create tables if they don't exist
        await _database.CreateTableAsync<PhotoItem>();
        await _database.CreateTableAsync<PhotoLabel>();
        await _database.CreateTableAsync<PhotoBook>();

        // MIGRATION: Add Latitude and Longitude columns if they don't exist
        try
        {
            await _database.ExecuteAsync("ALTER TABLE Photos ADD COLUMN Latitude REAL DEFAULT 0");
            System.Diagnostics.Debug.WriteLine("Added Latitude column to Photos table");
        }
        catch (Exception)
        {
            // Column already exists, ignore error
        }

        try
        {
            await _database.ExecuteAsync("ALTER TABLE Photos ADD COLUMN Longitude REAL DEFAULT 0");
            System.Diagnostics.Debug.WriteLine("Added Longitude column to Photos table");
        }
        catch (Exception)
        {
            // Column already exists, ignore error
        }

        System.Diagnostics.Debug.WriteLine($"Database initialized at: {_databasePath}");
    }

    /// <summary>
    /// add a new photo to the database
    /// </summary>
    public async Task<int> AddPhotoAsync(PhotoItem photo)
    {
        var db = await GetDatabaseAsync();
        photo.CreatedDate = DateTime.Now;
        var result = await db.InsertAsync(photo);
        // InsertAsync returns the number of rows added (1 if successful)
        System.Diagnostics.Debug.WriteLine($"Photo saved to database: Id={photo.Id}, FileName={photo.FileName}, FilePath={photo.FilePath}");
        return photo.Id;
    }

    /// <summary>
    /// Retrieves all photos from the database (excludes photos that are only in PhotoBooks)
    /// </summary>
    public async Task<List<PhotoItem>> GetAllPhotosAsync()
    {
        var db = await GetDatabaseAsync();
        // Only retrieve photos that are NOT in a PhotoBook (PhotoBookId is null or 0)
        var photos = await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId == null || p.PhotoBookId == 0)
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();
        
        System.Diagnostics.Debug.WriteLine($"Loaded {photos.Count} photos from database");

        // Load labels and initialize ImageSource for each photo
        // Filter out photos that cannot be loaded
        var validPhotos = new List<PhotoItem>();
        foreach (var photo in photos)
        {
            await LoadLabelsForPhotoAsync(photo);

            // Only set ImageSource if file exists
            if (photo.FileExists)
            {
                photo.InitializeImageSource();

                // Only add photos with valid ImageSource
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
    /// Retrieves a photo by its ID
    /// </summary>
    public async Task<PhotoItem?> GetPhotoByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        var photo = await db.Table<PhotoItem>().Where(p => p.Id == id).FirstOrDefaultAsync();
        
        if (photo != null)
        {
            await LoadLabelsForPhotoAsync(photo);

            // Set all properties and ImageSource if file exists    
            if (photo.FileExists)
            {
                photo.InitializeImageSource();
            }
        }

        return photo;
    }

    /// <summary>
    /// Updates an existing photo
    /// </summary>
    public async Task<int> UpdatePhotoAsync(PhotoItem photo)
    {
        var db = await GetDatabaseAsync();
        return await db.UpdateAsync(photo);
    }

    /// <summary>
    /// Deletes a photo, all its labels, and the file from disk
    /// </summary>
    public async Task<int> DeletePhotoAsync(PhotoItem photo)
    {
        var db = await GetDatabaseAsync();

        // First, delete all labels for this photo
        await db.Table<PhotoLabel>().DeleteAsync(l => l.PhotoId == photo.Id);

        // Remove the photo file from disk
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

        // Remove the photo from the database
        var result = await db.DeleteAsync(photo);
        System.Diagnostics.Debug.WriteLine($"DeletePhotoAsync: Deleted photo {photo.Id} from database");
        
        return result;
    }

    /// <summary>
    /// Deletes a photo by its ID
    /// </summary>
    public async Task<int> DeletePhotoByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();

        // First, delete all labels for this photo
        await db.Table<PhotoLabel>().DeleteAsync(l => l.PhotoId == id);

        // Remove the photo
        return await db.Table<PhotoItem>().DeleteAsync(p => p.Id == id);
    }


    /// <summary>
    /// Adds a label to a photo if it doesn't already exist (case-insensitive)
    /// Returns the label ID if successful, or 0 if the label already exists
    /// </summary>
    public async Task<int> AddLabelAsync(int photoId, string labelText)
    {
        var db = await GetDatabaseAsync();

        // Retrieve all labels for this photo and check case-insensitive    
        var existingLabels = await db.Table<PhotoLabel>()
            .Where(l => l.PhotoId == photoId)
            .ToListAsync();

        // Check if label already exists (case-insensitive)
        // Use StringComparison.OrdinalIgnoreCase for case-insensitive comparison
        var existingLabel = existingLabels
            .FirstOrDefault(l => string.Equals(l.LabelText, labelText, StringComparison.OrdinalIgnoreCase));

        if (existingLabel != null)
        {
            System.Diagnostics.Debug.WriteLine($"Label '{labelText}' already exists for photo {photoId} (existing: '{existingLabel.LabelText}')");
            return 0; // Label already exists (case-insensitive)
        }

        var label = new PhotoLabel
        {
            PhotoId = photoId,
            LabelText = labelText,
            CreatedDate = DateTime.Now
        };

        var result = await db.InsertAsync(label);
        System.Diagnostics.Debug.WriteLine($"Label '{labelText}' added successfully with ID: {label.Id}, InsertAsync returned: {result}");

        // Return the label ID (label.Id is set by InsertAsync for AutoIncrement)
        // If both are 0, return 0 to indicate failure (not 1)
        return label.Id > 0 ? label.Id : result;
    }

    /// <summary>
    /// Retrieves all labels for a specific photo
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
    /// Loads labels into the Labels collection of a photo
    /// </summary>
    public async Task LoadLabelsForPhotoAsync(PhotoItem photo)
    {
        try
        {
            var labels = await GetLabelsForPhotoAsync(photo.Id);

            // Clear and add on main thread for thread safety with ObservableCollection
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
            // Set empty collection on error
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                photo.Labels.Clear();
            });
        }
    }

    /// <summary>
    /// Removes a label from a photo (case-insensitive)
    /// </summary>
    public async Task<int> RemoveLabelAsync(int photoId, string labelText)
    {
        var db = await GetDatabaseAsync();

        // Retrieve all labels for this photo
        var labels = await db.Table<PhotoLabel>()
            .Where(l => l.PhotoId == photoId)
            .ToListAsync();

        // Find label case-insensitively
        // Use StringComparison.OrdinalIgnoreCase for case-insensitive comparison
        var labelToDelete = labels
            .FirstOrDefault(l => string.Equals(l.LabelText, labelText, StringComparison.OrdinalIgnoreCase));
        
        if (labelToDelete != null)
        {
            return await db.DeleteAsync(labelToDelete);
        }
        
        return 0;
    }

    /// <summary>
    /// Removes a label from all photos and from the database
    /// </summary>
    /// <param name="labelText"></param>
    /// <returns></returns>
    public async Task<int> DeleteLabelFromAllPhotosAsync(string labelText)
    {
        var db = await GetDatabaseAsync();

        // Retrieve all labels and filter case-insensitively
        var allLabels = await db.Table<PhotoLabel>().ToListAsync();
        var matchingLabels = allLabels
            .Where(l => string.Equals(l.LabelText, labelText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!matchingLabels.Any())
            return 0;

        // Remove the label from all photos by deleting each matching label
        int deletedCount = 0;
        foreach (var label in matchingLabels)
        {
            deletedCount += await db.DeleteAsync(label);
        }
        return deletedCount;
    }

    /// <summary>
    /// Removes a label based on ID
    /// </summary>
    public async Task<int> RemoveLabelByIdAsync(int labelId)
    {
        var db = await GetDatabaseAsync();
        return await db.DeleteAsync<PhotoLabel>(labelId);
    }

    /// <summary>
    /// Retrieves all unique labels from all photos (case-insensitive distinct)
    /// </summary>
    public async Task<List<string>> GetAllUniqueLabelsAsync()
    {
        var db = await GetDatabaseAsync();
        var labels = await db.Table<PhotoLabel>().ToListAsync();

        // Group by case-insensitive label text and take the first occurrence
        // Use StringComparer.OrdinalIgnoreCase for case-insensitive grouping
        var uniqueLabels = labels
            .GroupBy(l => l.LabelText, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First().LabelText) // Preserve original casing of first occurrence
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        return uniqueLabels;
    }

    /// <summary>
    /// Retrieves all photos with a specific label (case-insensitive)
    /// </summary>
    public async Task<List<PhotoItem>> GetPhotosByLabelAsync(string labelText)
    {
        var db = await GetDatabaseAsync();

        // Retrieve all labels and filter case-insensitively
        // We do this in memory because SQLite is case-sensitive and we want to filter case-insensitively
        var allLabels = await db.Table<PhotoLabel>().ToListAsync();
        var matchingLabels = allLabels
            .Where(l => string.Equals(l.LabelText, labelText, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        var photoIds = matchingLabels.Select(l => l.PhotoId).ToList();

        if (!photoIds.Any())
            return new List<PhotoItem>();

        // Retrieve photos with those IDs, sorted by newest first
        var photos = await db.Table<PhotoItem>()
            .Where(p => photoIds.Contains(p.Id))
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();

        // Load labels and initialize ImageSource for each photo
        // Filter out photos that cannot be loaded
        var validPhotos = new List<PhotoItem>();
        foreach (var photo in photos)
        {
            await LoadLabelsForPhotoAsync(photo);

            // Set ImageSource only if file exists
            if (photo.FileExists)
            {
                photo.InitializeImageSource();

                // Add only photos with valid ImageSource
                if (photo.ImageSource != null)
                {
                    validPhotos.Add(photo);
                }
            }
        }

        return validPhotos;
    }

    /// <summary>
    /// Retrieves the total number of photos (excludes photos in PhotoBooks)
    /// </summary>
    public async Task<int> GetPhotoCountAsync()
    {
        var db = await GetDatabaseAsync();
        // Count only photos that are NOT in a PhotoBook (PhotoBookId is null or 0)
        return await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId == null || p.PhotoBookId == 0)
            .CountAsync();
    }

    /// <summary>
    /// Retrieves the total number of photos that are in PhotoBooks
    /// </summary>
    public async Task<int> GetPhotoBookPhotoCountAsync()
    {
        var db = await GetDatabaseAsync();
        // Count only photos that are in a PhotoBook (PhotoBookId is not null and not 0)
        return await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId != null && p.PhotoBookId != 0)
            .CountAsync();
    }

    /// <summary>
    /// Deletes all photos from the database (use with caution!)
    /// </summary>
    public async Task DeleteAllPhotosAsync()
    {
        var db = await GetDatabaseAsync();
        // First delete all labels
        await db.DeleteAllAsync<PhotoLabel>();
        // Then delete all photos
        await db.DeleteAllAsync<PhotoItem>();
        System.Diagnostics.Debug.WriteLine("All photos deleted from database");
    }

    /// <summary>
    /// Deletes all data from the database (photos, labels, photobooks)
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAllAsync<PhotoLabel>();
        await db.DeleteAllAsync<PhotoItem>();
        await db.DeleteAllAsync<PhotoBook>();
    }


    /// <summary>
    /// Adds a new PhotoBook to the database
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
    /// Retrieves all PhotoBooks from the database
    /// </summary>
    public async Task<List<PhotoBook>> GetAllPhotoBooksAsync()
    {
        var db = await GetDatabaseAsync();
        var photoBooks = await db.Table<PhotoBook>().OrderByDescending(pb => pb.UpdatedDate).ToListAsync();
        
        System.Diagnostics.Debug.WriteLine($"Loaded {photoBooks.Count} PhotoBooks from database");
        return photoBooks;
    }

    /// <summary>
    /// Retrieves a PhotoBook by its ID
    /// </summary>
    public async Task<PhotoBook?> GetPhotoBookByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PhotoBook>().Where(pb => pb.Id == id).FirstOrDefaultAsync();
    }
    
    /// <summary>
    /// Retrieves the first photo that belongs to a PhotoBook (for thumbnail)
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
    /// Retrieves all photos that belong to a PhotoBook
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

            // Initialize ImageSource (this is crucial for UI display)
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
    /// Updates an existing PhotoBook
    /// </summary>
    public async Task<int> UpdatePhotoBookAsync(PhotoBook photoBook)
    {
        var db = await GetDatabaseAsync();
        photoBook.UpdatedDate = DateTime.Now;
        return await db.UpdateAsync(photoBook);
    }

    /// <summary>
    /// Deletes a PhotoBook from the database and all associated photos
    /// </summary>
    public async Task<int> DeletePhotoBookAsync(PhotoBook photoBook)
    {
        var db = await GetDatabaseAsync();

        // First delete all photos associated with this PhotoBook
        var photos = await db.Table<PhotoItem>()
            .Where(p => p.PhotoBookId == photoBook.Id)
            .ToListAsync();
        
        System.Diagnostics.Debug.WriteLine($"DeletePhotoBookAsync: Deleting {photos.Count} photos from PhotoBook {photoBook.Id}");
        
        foreach (var photo in photos)
        {
            // First delete labels
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

            // Delete the photo from the database
            await db.DeleteAsync(photo);
        }

        // Finally delete the PhotoBook itself
        var result = await db.DeleteAsync(photoBook);
        System.Diagnostics.Debug.WriteLine($"DeletePhotoBookAsync: Deleted PhotoBook {photoBook.Id}");
        
        return result;
    }

    /// <summary>
    /// Deletes a PhotoBook based on ID
    /// </summary>
    public async Task<int> DeletePhotoBookByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PhotoBook>().DeleteAsync(pb => pb.Id == id);
    }

    /// <summary>
    /// Retrieves the total number of PhotoBooks
    /// </summary>
    public async Task<int> GetPhotoBookCountAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PhotoBook>().CountAsync();
    }
}

