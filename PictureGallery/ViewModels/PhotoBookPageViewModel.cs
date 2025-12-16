using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using PictureGallery.Models;
using PictureGallery.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PictureGallery.ViewModels;

public partial class PhotoBookPageViewModel : BaseViewModel
{
    // Removed MaxPhotosPerPage - all photos on one page
    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg" };

    private readonly DatabaseService _databaseService;
    private readonly int? _photoBookId;

    [ObservableProperty]
    private PhotoBook photoBook = new();

    [ObservableProperty]
    private bool isPdfMode;

    [ObservableProperty]
    private bool isDeleteMode;

    [ObservableProperty]
    private string pageIndicatorText = "Pagina 1 van 1";

    [ObservableProperty]
    private bool showPrevArrow;

    [ObservableProperty]
    private bool showNextArrow;

    [ObservableProperty]
    private int currentPagePosition;

    partial void OnCurrentPagePositionChanged(int value)
    {
        // Removed page navigation - all photos on one page
    }

    public PhotoBookPageViewModel(int? photoBookId = null)
    {
        _databaseService = new DatabaseService();
        _photoBookId = photoBookId;
        Title = "Photobook";

        AddPhotoCommand = new AsyncRelayCommand(AddPhotoAsync);
        StartDeleteModeCommand = new RelayCommand(() => 
        { 
            IsDeleteMode = true; 
            IsPdfMode = false; 
            ClearSelections();
            OnPropertyChanged(nameof(PhotoBook)); // Notify UI to rebuild
        });
        CancelDeleteModeCommand = new RelayCommand(() => 
        { 
            IsDeleteMode = false; 
            ClearSelections();
            OnPropertyChanged(nameof(PhotoBook)); // Notify UI to rebuild
        });
        DeleteSelectedPhotosCommand = new AsyncRelayCommand(DeleteSelectedPhotosAsync);
        StartPdfModeCommand = new RelayCommand(() => 
        { 
            IsPdfMode = true; 
            IsDeleteMode = false; 
            SelectAllPhotos();
            OnPropertyChanged(nameof(PhotoBook)); // Notify UI to rebuild
        });
        CancelPdfModeCommand = new RelayCommand(() => 
        { 
            IsPdfMode = false; 
            ClearSelections();
            OnPropertyChanged(nameof(PhotoBook)); // Notify UI to rebuild
        });
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync);
        PhotoTappedCommand = new RelayCommand<PhotoItem>(OnPhotoTapped);
        // Removed NextPageCommand and PrevPageCommand - all photos on one page
    }

    public ICommand AddPhotoCommand { get; }
    public ICommand StartDeleteModeCommand { get; }
    public ICommand CancelDeleteModeCommand { get; }
    public ICommand DeleteSelectedPhotosCommand { get; }
    public ICommand StartPdfModeCommand { get; }
    public ICommand CancelPdfModeCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand PhotoTappedCommand { get; }
    // Removed NextPageCommand and PrevPageCommand - all photos on one page

    public async Task LoadPhotoBookAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] ========== START ========== PhotoBookId: {_photoBookId?.ToString() ?? "null"}");
        
        try
        {
            IsBusy = true;

            // Case 1: No PhotoBookId - create empty PhotoBook
            if (!_photoBookId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] No PhotoBookId, creating empty PhotoBook");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    PhotoBook = new PhotoBook();
                    PhotoBook.Pages.Clear();
                    PhotoBook.Pages.Add(new PhotoBookPageModel());
                    UpdateUI();
                    OnPropertyChanged(nameof(PhotoBook));
                });
                return;
            }

            // Case 2: Load existing PhotoBook
            System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] Loading PhotoBook with Id: {_photoBookId.Value}");
            
            // Step 1: Load PhotoBook metadata
            var loadedPhotoBook = await _databaseService.GetPhotoBookByIdAsync(_photoBookId.Value);
            if (loadedPhotoBook == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] ERROR - PhotoBook {_photoBookId.Value} not found in database");
                await ShowAlertAsync("Error", "Photo book not found in database.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] PhotoBook metadata loaded: Id={loadedPhotoBook.Id}, Name={loadedPhotoBook.Name}");

            // Step 2: Load photos (GetPhotosByPhotoBookIdAsync now returns ONLY photos with valid ImageSource)
            var photos = await _databaseService.GetPhotosByPhotoBookIdAsync(_photoBookId.Value);
            System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] Received {photos.Count} photos from DatabaseService (all should have valid ImageSource)");

            // Step 3: Build Pages structure on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] Building Pages structure on main thread");
                
                PhotoBook = loadedPhotoBook;
                PhotoBook.Pages.Clear();

                if (photos.Count == 0)
                {
                    PhotoBook.Pages.Add(new PhotoBookPageModel());
                    System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] No photos, created empty page");
                }
                else
                {
                    // All photos on one page
                    var singlePage = new PhotoBookPageModel();
                    
                    foreach (var photo in photos)
                    {
                        // Verify ImageSource (should be set by DatabaseService)
                        if (photo.ImageSource == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] WARNING - Photo {photo.Id} has null ImageSource, skipping");
                            continue;
                        }

                        // Add photo to the single page
                        singlePage.Photos.Add(photo);
                        System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] Added photo {photo.Id} (ImageSource={photo.ImageSource != null}) to page");
                    }

                    // Add the single page with all photos
                    if (singlePage.Photos.Count > 0)
                    {
                        PhotoBook.Pages.Add(singlePage);
                        System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] Added single page with {singlePage.Photos.Count} photos");
                    }
                }

                UpdateUI();
                OnPropertyChanged(nameof(PhotoBook));
                
                System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] COMPLETE - PhotoBook has {PhotoBook.Pages.Count} pages with {PhotoBook.Pages.Sum(p => p.Photos.Count)} total photos");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] StackTrace: {ex.StackTrace}");
                await ShowAlertAsync("Error", $"Could not load photo book: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            System.Diagnostics.Debug.WriteLine($"[LoadPhotoBookAsync] ========== FINALLY ==========");
        }
    }

    private async Task AddPhotoAsync()
    {
        try
        {
            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Select images"
            });

            if (results == null || !results.Any()) return;

            IsBusy = true;

            foreach (var result in results)
            {
                if (!IsSupportedImage(result.FileName)) continue;

                PhotoItem? photo = null;
                try
                {
                    photo = await CreatePhotoItemAsync(result);
                }
                catch (Exception ex)
                {
                    await ShowAlertAsync("Error", $"Photo '{result.FileName}' could not be loaded: {ex.Message}");
                    continue;
                }

                if (photo == null || !photo.IsValid) continue;

                photo.PhotoBookId = _photoBookId;

                try
                {
                    await _databaseService.AddPhotoAsync(photo);
                }
                catch (Exception ex)
                {
                    await ShowAlertAsync("Error", $"Could not save photo: {ex.Message}");
                    continue;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Ensure we have at least one page
                    if (PhotoBook.Pages.Count == 0)
                    {
                        PhotoBook.Pages.Add(new PhotoBookPageModel());
                    }
                    
                    var page = PhotoBook.Pages[0]; // Always use the first (and only) page

                    if (photo.FileExists && photo.ImageSource == null)
                    {
                        photo.InitializeImageSource();
                    }

                    page.Photos.Add(photo);

                    if (PhotoBook.Pages[0].Photos.Count == 1 && photo.ImageSource != null)
                    {
                        PhotoBook.ThumbnailImage = photo.ImageSource;
                    }
                });

                if (_photoBookId.HasValue)
                {
                    await _databaseService.UpdatePhotoBookAsync(PhotoBook);
                }
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateUI();
                
                // Create a new PhotoBook instance to force UI update
                // Copy all pages to new instance
                var updatedPhotoBook = new PhotoBook
                {
                    Id = PhotoBook.Id,
                    Name = PhotoBook.Name,
                    Description = PhotoBook.Description,
                    CreatedDate = PhotoBook.CreatedDate,
                    UpdatedDate = PhotoBook.UpdatedDate,
                    ThumbnailImage = PhotoBook.ThumbnailImage
                };
                
                // Copy all pages and photos
                foreach (var page in PhotoBook.Pages)
                {
                    var newPage = new PhotoBookPageModel { Title = page.Title };
                    foreach (var photo in page.Photos)
                    {
                        newPage.Photos.Add(photo);
                    }
                    updatedPhotoBook.Pages.Add(newPage);
                }
                
                // Replace PhotoBook instance - this forces UI to update
                PhotoBook = updatedPhotoBook;
                
                // Trigger event to notify View that photos were added (this will trigger refresh)
                PhotosAdded?.Invoke();
                
                // Then notify property change
                OnPropertyChanged(nameof(PhotoBook));
                
                System.Diagnostics.Debug.WriteLine($"[AddPhotoAsync] Photos added - Pages: {PhotoBook.Pages.Count}, Total photos: {PhotoBook.Pages.Sum(p => p.Photos.Count)}");
            });
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Fout", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteSelectedPhotosAsync()
    {
        var photosToDelete = PhotoBook.Pages.SelectMany(p => p.Photos).Where(p => p.IsSelected).ToList();
        if (photosToDelete.Count == 0)
        {
            await ShowAlertAsync("No selection", "Select at least one photo.");
            return;
        }

        var confirmed = await ShowConfirmationAsync("Delete", $"Are you sure you want to delete {photosToDelete.Count} photo(s)?", "Yes", "No");
        if (!confirmed) return;

        // First, delete from database and file system (before removing from UI collections)
        foreach (var photo in photosToDelete)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DeleteSelectedPhotosAsync] Deleting photo {photo.Id}: {photo.FileName}");
                
                // Delete from database (this also deletes labels and file)
                await _databaseService.DeletePhotoAsync(photo);
                System.Diagnostics.Debug.WriteLine($"[DeleteSelectedPhotosAsync] Successfully deleted photo {photo.Id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeleteSelectedPhotosAsync] ERROR deleting photo {photo.Id}: {ex.Message}");
            }
        }

        // Now remove from UI collections and clean up empty pages
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Remove photos from pages
            foreach (var page in PhotoBook.Pages.ToList())
            {
                var selected = page.Photos.Where(p => p.IsSelected).ToList();
                foreach (var photo in selected)
                {
                    page.Photos.Remove(photo);
                }
            }

            // Ensure we have at least one page
            if (PhotoBook.Pages.Count == 0)
            {
                PhotoBook.Pages.Add(new PhotoBookPageModel());
            }
            
            // All photos are on one page, so no need to adjust position

            IsDeleteMode = false;
            ClearSelections();
            UpdateUI();
            
            System.Diagnostics.Debug.WriteLine($"[DeleteSelectedPhotosAsync] Photo deletion complete - Pages: {PhotoBook.Pages.Count}, Total photos: {PhotoBook.Pages.Sum(p => p.Photos.Count)}");
        });

        // Update database after UI changes
        if (_photoBookId.HasValue)
        {
            await _databaseService.UpdatePhotoBookAsync(PhotoBook);
        }

        // Notify UI that PhotoBook has changed (only once, after all changes are complete)
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Create a new PhotoBook instance to force UI update
            // Copy all pages to new instance
            var updatedPhotoBook = new PhotoBook
            {
                Id = PhotoBook.Id,
                Name = PhotoBook.Name,
                Description = PhotoBook.Description,
                CreatedDate = PhotoBook.CreatedDate,
                UpdatedDate = PhotoBook.UpdatedDate,
                ThumbnailImage = PhotoBook.ThumbnailImage
            };
            
            // Copy all pages and photos
            foreach (var page in PhotoBook.Pages)
            {
                var newPage = new PhotoBookPageModel { Title = page.Title };
                foreach (var photo in page.Photos)
                {
                    newPage.Photos.Add(photo);
                }
                updatedPhotoBook.Pages.Add(newPage);
            }
            
            // Replace PhotoBook instance - this forces UI to update
            PhotoBook = updatedPhotoBook;
            
            // Trigger refresh event first - this will set the refresh flag in the View
            PhotosDeleted?.Invoke();
            
            // Then notify property change
            OnPropertyChanged(nameof(PhotoBook));
        });
    }

    // Events to notify View that photos were added/deleted and layout needs refresh
    public event Action? PhotosDeleted;
    public event Action? PhotosAdded;

    private async Task ExportPdfAsync()
    {
        // Handled in View code-behind
        await Task.CompletedTask;
    }

    private void OnPhotoTapped(PhotoItem? photo)
    {
        if (photo == null || (!IsDeleteMode && !IsPdfMode)) return;
        photo.IsSelected = !photo.IsSelected;
    }

    // Removed NextPageRequested and PrevPageRequested events - all photos on one page

    public void OnPagePositionChanged(int position)
    {
        // Removed - all photos on one page
    }

    private void UpdateUI()
    {
        // Removed page navigation UI updates - all photos on one page
    }

    private void ClearSelections()
    {
        foreach (var page in PhotoBook.Pages)
        {
            foreach (var photo in page.Photos)
            {
                photo.IsSelected = false;
            }
        }
    }

    private void SelectAllPhotos()
    {
        foreach (var page in PhotoBook.Pages)
        {
            foreach (var photo in page.Photos)
            {
                photo.IsSelected = true;
            }
        }
    }

    private bool IsSupportedImage(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<PhotoItem> CreatePhotoItemAsync(FileResult result)
    {
        // ALWAYS copy file to app directory (never use external paths - they become inaccessible after app restart)
        var photosDirectory = Path.Combine(FileSystem.AppDataDirectory, "Photos");
        if (!Directory.Exists(photosDirectory))
        {
            Directory.CreateDirectory(photosDirectory);
        }

        var extension = Path.GetExtension(result.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(photosDirectory, uniqueFileName);

        System.Diagnostics.Debug.WriteLine($"[CreatePhotoItemAsync] Copying file from '{result.FullPath}' to app directory: '{filePath}'");

        try
        {
            await using (var pickedStream = await result.OpenReadAsync())
            {
                await using (var permanentFile = File.Create(filePath))
                {
                    await pickedStream.CopyToAsync(permanentFile);
                    await permanentFile.FlushAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreatePhotoItemAsync] Error copying file: {ex.Message}");
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch { }
            throw new InvalidOperationException($"Kon bestand niet kopiëren: {ex.Message}", ex);
        }

        await Task.Delay(50); // Wait for file system

        if (!File.Exists(filePath))
        {
            System.Diagnostics.Debug.WriteLine($"[CreatePhotoItemAsync] File was not created: {filePath}");
            throw new InvalidOperationException("Bestand kon niet worden aangemaakt.");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            System.Diagnostics.Debug.WriteLine($"[CreatePhotoItemAsync] File is empty: {filePath}");
            try { File.Delete(filePath); } catch { }
            throw new InvalidOperationException("Bestand is leeg.");
        }

        System.Diagnostics.Debug.WriteLine($"[CreatePhotoItemAsync] File copied successfully: {filePath}, Size: {fileInfo.Length} bytes");

        int width = 0, height = 0;
        try
        {
            using var stream = File.OpenRead(filePath);
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                bitmap?.Dispose();
                try { File.Delete(filePath); } catch { }
                throw new InvalidOperationException("Geen geldige afbeelding.");
            }
            width = bitmap.Width;
            height = bitmap.Height;
            bitmap.Dispose();
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            try { File.Delete(filePath); } catch { }
            throw new InvalidOperationException($"Kon afbeelding niet lezen: {ex.Message}", ex);
        }

        var imageSource = ImageSource.FromFile(filePath);
        if (imageSource == null)
        {
            try { File.Delete(filePath); } catch { }
            throw new InvalidOperationException("Kon ImageSource niet aanmaken.");
        }

        return new PhotoItem
        {
            FileName = result.FileName,
            FilePath = filePath,
            Width = width,
            Height = height,
            FileSizeMb = fileInfo.Length / (1024.0 * 1024.0),
            ImageSource = imageSource
        };
    }

    private async Task ShowAlertAsync(string title, string message)
    {
        if (Application.Current?.MainPage != null)
        {
            await Application.Current.MainPage.DisplayAlert(title, message, "OK");
        }
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message, string accept, string cancel)
    {
        if (Application.Current?.MainPage != null)
        {
            return await Application.Current.MainPage.DisplayAlert(title, message, accept, cancel);
        }
        return false;
    }
}
