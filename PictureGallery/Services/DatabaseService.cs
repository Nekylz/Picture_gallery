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
    /// Get all photos from the database
    /// </summary>
    public async Task<List<PhotoItem>> GetAllPhotosAsync()
    {
        var db = await GetDatabaseAsync();
        var photos = await db.Table<PhotoItem>().OrderByDescending(p => p.CreatedDate).ToListAsync();
        
        System.Diagnostics.Debug.WriteLine($"Loaded {photos.Count} photos from database");
        
        // Laad labels en initialiseer ImageSource voor elke foto
        foreach (var photo in photos)
        {
            await LoadLabelsForPhotoAsync(photo);
            
            // Stel alleen ImageSource in als bestand bestaat
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
        photo.Labels.Clear();
        var labels = await GetLabelsForPhotoAsync(photo.Id);
        foreach (var label in labels)
        {
            photo.Labels.Add(label.LabelText);
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
        foreach (var photo in photos)
        {
            await LoadLabelsForPhotoAsync(photo);
            
            // Stel alleen ImageSource in als bestand bestaat
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

