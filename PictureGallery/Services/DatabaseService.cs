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

    public DatabaseService()
    {
        _databasePath = Path.Combine(FileSystem.AppDataDirectory, "picturegallery.db3");
    }

    private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        if (_database != null)
            return _database;

        _database = new SQLiteAsyncConnection(_databasePath);
        await InitializeDatabaseAsync();
        return _database;
    }

    private async Task InitializeDatabaseAsync()
    {
        if (_database == null)
            return;

        // Create tables if they don't exist
        await _database.CreateTableAsync<PhotoItem>();
        await _database.CreateTableAsync<PhotoLabel>();
        
        System.Diagnostics.Debug.WriteLine($"Database initialized at: {_databasePath}");
    }

    // ========== PHOTO OPERATIONS ==========

    /// <summary>
    /// Add a new photo to the database
    /// </summary>
    public async Task<int> AddPhotoAsync(PhotoItem photo)
    {
        var db = await GetDatabaseAsync();
        photo.CreatedDate = DateTime.Now;
        var result = await db.InsertAsync(photo);
        // InsertAsync returns the number of rows affected, but with AutoIncrement,
        // the Id property is automatically set on the photo object
        System.Diagnostics.Debug.WriteLine($"Photo saved to database: Id={photo.Id}, FileName={photo.FileName}, FilePath={photo.FilePath}");
        return photo.Id;
    }

    /// <summary>
    /// Get all photos from the database
    /// </summary>
    public async Task<List<PhotoItem>> GetAllPhotosAsync()
    {
        var db = await GetDatabaseAsync();
        var photos = await db.Table<PhotoItem>().OrderByDescending(p => p.CreatedDate).ToListAsync();
        
        System.Diagnostics.Debug.WriteLine($"Loaded {photos.Count} photos from database");
        
        // Load labels and initialize ImageSource for each photo
        foreach (var photo in photos)
        {
            await LoadLabelsForPhotoAsync(photo);
            
            // Only set ImageSource if file exists
            if (photo.FileExists)
            {
                photo.InitializeImageSource();
                System.Diagnostics.Debug.WriteLine($"Photo loaded: Id={photo.Id}, FileName={photo.FileName}, FileExists={photo.FileExists}, ImageSource={photo.ImageSource != null}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Photo file missing: Id={photo.Id}, FileName={photo.FileName}, FilePath={photo.FilePath}");
            }
        }

        return photos;
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
    /// Delete a photo and all its labels
    /// </summary>
    public async Task<int> DeletePhotoAsync(PhotoItem photo)
    {
        var db = await GetDatabaseAsync();
        
        // Delete all labels for this photo first
        await db.Table<PhotoLabel>().DeleteAsync(l => l.PhotoId == photo.Id);
        
        // Delete the photo
        return await db.DeleteAsync(photo);
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

    // ========== LABEL OPERATIONS ==========

    /// <summary>
    /// Add a label to a photo
    /// </summary>
    public async Task<int> AddLabelAsync(int photoId, string labelText)
    {
        var db = await GetDatabaseAsync();
        
        // Check if label already exists for this photo
        var existingLabel = await db.Table<PhotoLabel>()
            .Where(l => l.PhotoId == photoId && l.LabelText == labelText)
            .FirstOrDefaultAsync();

        if (existingLabel != null)
            return 0; // Label already exists

        var label = new PhotoLabel
        {
            PhotoId = photoId,
            LabelText = labelText,
            CreatedDate = DateTime.Now
        };

        return await db.InsertAsync(label);
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
        photo.Labels.Clear();
        var labels = await GetLabelsForPhotoAsync(photo.Id);
        foreach (var label in labels)
        {
            photo.Labels.Add(label.LabelText);
        }
    }

    /// <summary>
    /// Remove a label from a photo
    /// </summary>
    public async Task<int> RemoveLabelAsync(int photoId, string labelText)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PhotoLabel>()
            .DeleteAsync(l => l.PhotoId == photoId && l.LabelText == labelText);
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
    /// Get all unique labels across all photos
    /// </summary>
    public async Task<List<string>> GetAllUniqueLabelsAsync()
    {
        var db = await GetDatabaseAsync();
        var labels = await db.Table<PhotoLabel>().ToListAsync();
        return labels.Select(l => l.LabelText).Distinct().OrderBy(l => l).ToList();
    }

    /// <summary>
    /// Get all photos with a specific label
    /// </summary>
    public async Task<List<PhotoItem>> GetPhotosByLabelAsync(string labelText)
    {
        var db = await GetDatabaseAsync();
        
        // Get photo IDs that have this label
        var labels = await db.Table<PhotoLabel>()
            .Where(l => l.LabelText == labelText)
            .ToListAsync();
        
        var photoIds = labels.Select(l => l.PhotoId).ToList();

        if (!photoIds.Any())
            return new List<PhotoItem>();

        // Get photos with those IDs
        var photos = await db.Table<PhotoItem>()
            .Where(p => photoIds.Contains(p.Id))
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();

        // Load labels and initialize ImageSource for each photo
        foreach (var photo in photos)
        {
            await LoadLabelsForPhotoAsync(photo);
            
            // Only set ImageSource if file exists
            if (photo.FileExists)
            {
                photo.InitializeImageSource();
            }
        }

        return photos;
    }

    // ========== UTILITY METHODS ==========

    /// <summary>
    /// Get the total number of photos
    /// </summary>
    public async Task<int> GetPhotoCountAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PhotoItem>().CountAsync();
    }

    /// <summary>
    /// Clear all data from the database (use with caution!)
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAllAsync<PhotoLabel>();
        await db.DeleteAllAsync<PhotoItem>();
    }
}

