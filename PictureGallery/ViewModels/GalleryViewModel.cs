using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using PictureGallery.Models;
using PictureGallery.Services;
using PictureGallery.Views;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PictureGallery.ViewModels;

public enum RatingSortOrder
{
    None,
    Highest,
    Lowest
}

public partial class GalleryViewModel : BaseViewModel
{
    private static readonly string[] AllowedExtensions = new[] { ".png", ".jpg", ".jpeg" };

    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private ObservableCollection<PhotoItem> photos = new();

    [ObservableProperty]
    private ObservableCollection<string> availableLabels = new();

    [ObservableProperty]
    private PhotoItem? currentPhoto;

    // Selection mode properties
    [ObservableProperty]
    private bool isSelectionMode;

    [ObservableProperty]
    private int selectedPhotosCount;

    private HashSet<PhotoItem> _selectedPhotos = new();

    // Filter and sort properties
    [ObservableProperty]
    private string? selectedLabelFilter;

    [ObservableProperty]
    private bool sortNewestFirst = true;

    [ObservableProperty]
    private RatingSortOrder ratingSortOrder = RatingSortOrder.None;

    [ObservableProperty]
    private string filterLabelButtonText = "Filter: Label";

    [ObservableProperty]
    private string sortDateButtonText = "Newest ↑";

    [ObservableProperty]
    private string filterRatingButtonText = "Filter: Rating";

    [ObservableProperty]
    private string selectButtonText = "Select";

    [ObservableProperty]
    private bool selectionActionsButtonVisible;

    [ObservableProperty]
    private bool hasPhotos;

    [ObservableProperty]
    private bool isFullscreenOverlayVisible;

    [ObservableProperty]
    private ImageSource? fullscreenImageSource;

    [ObservableProperty]
    private string overlayFileName = string.Empty;

    [ObservableProperty]
    private string overlayDimensions = string.Empty;

    [ObservableProperty]
    private string overlayFileSize = string.Empty;

    [ObservableProperty]
    private int currentPhotoRating;

    [ObservableProperty]
    private string labelEntryText = string.Empty;

    private List<PhotoItem> _allPhotos = new(); // Alle foto's (voor filtering)

    // Event for map location updates (MVVM communication)
    public event Action<double, double>? MapLocationUpdateRequested;

    public GalleryViewModel()
    {
        _databaseService = new DatabaseService();
        Title = "Gallery";

        // Initialize commands
        UploadMediaCommand = new AsyncRelayCommand(UploadMediaAsync);
        PhotoTappedCommand = new AsyncRelayCommand<PhotoItem>(OnPhotoTappedAsync);
        StarClickedCommand = new AsyncRelayCommand<string>(StarClickedAsync);
        AddLabelCommand = new AsyncRelayCommand(AddLabelAsync);
        RemoveLabelCommand = new AsyncRelayCommand<string>(RemoveLabelAsync);
        SelectButtonCommand = new RelayCommand(ToggleSelectionMode);
        SelectionActionsCommand = new AsyncRelayCommand(SelectionActionsAsync);
        FilterLabelCommand = new AsyncRelayCommand(FilterLabelAsync);
        SortDateCommand = new RelayCommand(ToggleSortDate);
        FilterRatingCommand = new RelayCommand(ToggleFilterRating);
        CloseFullscreenCommand = new RelayCommand(CloseFullscreen);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        OpenPhotoBookCommand = new AsyncRelayCommand(OpenPhotoBookAsync);
        ImportPhotoCommand = new AsyncRelayCommand(UploadMediaAsync);
        LabelDropdownCommand = new AsyncRelayCommand(LabelDropdownAsync);
        RemoveLabelSidebarCommand = new AsyncRelayCommand(RemoveLabelSidebarAsync);

        // Load photos on initialization
        _ = LoadPhotosFromDatabaseAsync();
    }

    #region Commands

    public ICommand UploadMediaCommand { get; }
    public ICommand PhotoTappedCommand { get; }
    public ICommand StarClickedCommand { get; }
    public ICommand AddLabelCommand { get; }
    public ICommand RemoveLabelCommand { get; }
    public ICommand SelectButtonCommand { get; }
    public ICommand SelectionActionsCommand { get; }
    public ICommand FilterLabelCommand { get; }
    public ICommand SortDateCommand { get; }
    public ICommand FilterRatingCommand { get; }
    public ICommand CloseFullscreenCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand OpenPhotoBookCommand { get; }
    public ICommand ImportPhotoCommand { get; }
    public ICommand LabelDropdownCommand { get; }
    public ICommand RemoveLabelSidebarCommand { get; }

    #endregion

    #region Public Methods

    public async Task LoadPhotosAsync()
    {
        await LoadPhotosFromDatabaseAsync();
    }

    #endregion

    #region Private Methods - Photo Loading

    private async Task LoadPhotosFromDatabaseAsync()
    {
        try
        {
            IsBusy = true;
            System.Diagnostics.Debug.WriteLine("Loading photos from database...");
            var loadedPhotos = await _databaseService.GetAllPhotosAsync();

            System.Diagnostics.Debug.WriteLine($"Retrieved {loadedPhotos.Count} photos from database");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _allPhotos = loadedPhotos;
                UpdateAvailableLabels();
                ApplyFiltersAndSort();
                HasPhotos = Photos.Count > 0;

                System.Diagnostics.Debug.WriteLine($"Added {Photos.Count} photos to UI collection (filtered from {_allPhotos.Count})");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading photos: {ex.Message}\n{ex.StackTrace}");
            await ShowAlertAsync("Error", $"Failed to load photos: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Private Methods - Photo Upload

    private async Task UploadMediaAsync()
    {
        try
        {
            IEnumerable<FileResult>? results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Select images (PNG / JPG)"
            });

            if (results == null || !results.Any())
                return;

            var addedPhotos = new List<PhotoItem>();
            IsBusy = true;

            foreach (var result in results)
            {
                if (!IsSupportedImage(result.FileName))
                    continue;

                PhotoItem? photo = null;
                try
                {
                    photo = await CreatePhotoItemAsync(result);
                }
                catch (Exception photoEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating photo from {result.FileName}: {photoEx.Message}");
                    await ShowAlertAsync("Corrupt Image", $"The image '{result.FileName}' could not be loaded. The file is corrupted.");
                    continue;
                }

                if (photo == null || !photo.IsValid)
                    continue;

                try
                {
                    await _databaseService.AddPhotoAsync(photo);
                    System.Diagnostics.Debug.WriteLine($"Photo saved to database with ID: {photo.Id}");
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Database error: {dbEx.Message}");
                    await ShowAlertAsync("Database Error", $"Failed to save photo: {dbEx.Message}");
                    continue;
                }

                if (photo.ImageSource == null && photo.FileExists)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            photo.ImageSource = ImageSource.FromFile(photo.FilePath);
                        });
                    }
                    catch (Exception imgEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to create ImageSource: {imgEx.Message}");
                        await ShowAlertAsync("Corrupt Image", $"The image '{photo.FileName}' could not be loaded. The file is corrupted.");
                        try
                        {
                            await _databaseService.DeletePhotoAsync(photo);
                        }
                        catch { }
                        continue;
                    }
                }

                if (photo.ImageSource != null && photo.FileExists)
                {
                    System.Diagnostics.Debug.WriteLine($"Adding photo: {photo.FileName}, Path: {photo.FilePath}, ImageSource: {photo.ImageSource != null}, Id: {photo.Id}");
                    addedPhotos.Add(photo);
                }
            }

            if (addedPhotos.Any())
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var photo in addedPhotos)
                    {
                        _allPhotos.Insert(0, photo);
                    }
                    UpdateAvailableLabels();
                    ApplyFiltersAndSort();
                    HasPhotos = Photos.Count > 0;
                });

                CurrentPhoto = addedPhotos.FirstOrDefault();
            }

            IsBusy = false;
        }
        catch (Exception ex)
        {
            IsBusy = false;
            await ShowAlertAsync("Error", ex.Message);
        }
    }

    private async Task<PhotoItem?> CreatePhotoItemAsync(FileResult result)
    {
        var photosDirectory = Path.Combine(FileSystem.AppDataDirectory, "Photos");
        if (!Directory.Exists(photosDirectory))
        {
            Directory.CreateDirectory(photosDirectory);
        }

        var extension = Path.GetExtension(result.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var permanentPath = Path.Combine(photosDirectory, uniqueFileName);

        try
        {
            await using (var pickedStream = await result.OpenReadAsync())
            {
                await using (var permanentFile = File.Create(permanentPath))
                {
                    await pickedStream.CopyToAsync(permanentFile);
                    await permanentFile.FlushAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying file: {ex.Message}");
            try
            {
                if (File.Exists(permanentPath))
                    File.Delete(permanentPath);
            }
            catch { }
            throw new InvalidOperationException($"Could not copy file: {ex.Message}", ex);
        }

        await Task.Delay(50);

        if (!File.Exists(permanentPath))
        {
            System.Diagnostics.Debug.WriteLine($"File was not created: {permanentPath}");
            return null;
        }

        var fileInfo = new FileInfo(permanentPath);
        if (fileInfo.Length == 0)
        {
            System.Diagnostics.Debug.WriteLine($"File is empty: {permanentPath}");
            return null;
        }

        int width = 0, height = 0;
        int retries = 3;
        bool success = false;

        while (retries > 0 && !success)
        {
            try
            {
                using (var stream = File.OpenRead(permanentPath))
                {
                    var bitmap = SKBitmap.Decode(stream);

                    if (bitmap == null)
                    {
                        retries = 0;
                        throw new InvalidOperationException("The file could not be read as an image. The file may be corrupted or not a valid image.");
                    }

                    try
                    {
                        if (bitmap.Width <= 0 || bitmap.Height <= 0)
                        {
                            bitmap.Dispose();
                            retries = 0;
                            throw new InvalidOperationException("The image has no valid dimensions.");
                        }

                        width = bitmap.Width;
                        height = bitmap.Height;
                        success = true;
                    }
                    finally
                    {
                        bitmap?.Dispose();
                    }
                }
            }
            catch (IOException ex)
            {
                retries--;
                if (retries > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"File locked, retrying... ({retries} attempts left)");
                    await Task.Delay(100);
                }
                else
                {
                    try
                    {
                        if (File.Exists(permanentPath))
                            File.Delete(permanentPath);
                    }
                    catch { }
                    throw new InvalidOperationException($"Could not read image after {3 + retries} attempts: {ex.Message}", ex);
                }
            }
            catch (InvalidOperationException)
            {
                retries = 0;
                try
                {
                    if (File.Exists(permanentPath))
                        File.Delete(permanentPath);
                }
                catch { }
                throw;
            }
            catch (Exception ex)
            {
                retries = 0;
                try
                {
                    if (File.Exists(permanentPath))
                        File.Delete(permanentPath);
                }
                catch { }
                System.Diagnostics.Debug.WriteLine($"Error reading image dimensions: {ex.Message}");
                throw new InvalidOperationException($"Could not read image: {ex.Message}", ex);
            }
        }

        if (!success)
        {
            try
            {
                if (File.Exists(permanentPath))
                    File.Delete(permanentPath);
            }
            catch { }
            throw new InvalidOperationException("Could not read image: invalid or corrupted file.");
        }

        var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

        var photo = new PhotoItem
        {
            FileName = result.FileName,
            FilePath = permanentPath,
            Width = width,
            Height = height,
            FileSizeMb = fileSizeMB
        };

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    photo.ImageSource = ImageSource.FromFile(permanentPath);
                }
                catch (Exception imgEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating ImageSource: {imgEx.Message}");
                    try
                    {
                        if (File.Exists(permanentPath))
                            File.Delete(permanentPath);
                    }
                    catch { }
                    throw new InvalidOperationException($"Could not load image for display: {imgEx.Message}", imgEx);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MainThread.InvokeOnMainThreadAsync for ImageSource: {ex.Message}");
            throw;
        }

        if (photo.ImageSource == null)
        {
            try
            {
                if (File.Exists(permanentPath))
                    File.Delete(permanentPath);
            }
            catch { }
            throw new InvalidOperationException("Could not create ImageSource for the image.");
        }

        System.Diagnostics.Debug.WriteLine($"Created photo: {photo.FileName}, Path: {photo.FilePath}, ImageSource: {photo.ImageSource != null}, Size: {width}x{height}");

        return photo;
    }

    private bool IsSupportedImage(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Private Methods - Photo Display

    private async Task OnPhotoTappedAsync(PhotoItem? photo)
    {
        if (photo == null)
            return;

        if (IsSelectionMode)
        {
            if (_selectedPhotos.Contains(photo))
            {
                _selectedPhotos.Remove(photo);
                photo.IsSelected = false;
            }
            else
            {
                _selectedPhotos.Add(photo);
                photo.IsSelected = true;
            }
            SelectedPhotosCount = _selectedPhotos.Count;
            SelectButtonText = $"Cancel ({SelectedPhotosCount})";
        }
        else
        {
            // Zet CurrentPhoto NIET hier, laat ShowPhotoOverlayAsync dat doen na het laden van labels
            await ShowPhotoOverlayAsync(photo);
        }
    }

    private async Task ShowPhotoOverlayAsync(PhotoItem photo)
    {
        try
        {
            if (photo.ImageSource == null)
                photo.InitializeImageSource();

            if (photo.ImageSource == null)
                return;

            // Laad labels VOORDAT we CurrentPhoto zetten om race conditions te voorkomen
            if (photo.Id > 0)
            {
                await _databaseService.LoadLabelsForPhotoAsync(photo);
            }

            // Zet alle properties op main thread om thread safety te garanderen
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CurrentPhoto = photo; // Zet CurrentPhoto NA het laden van labels
                FullscreenImageSource = photo.ImageSource;
                OverlayFileName = photo.FileName;
                OverlayDimensions = photo.DimensionsText;
                OverlayFileSize = photo.FileSizeText;
                CurrentPhotoRating = photo.Rating;
                IsFullscreenOverlayVisible = true;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ShowPhotoOverlayAsync: {ex.Message}\n{ex.StackTrace}");
            await ShowAlertAsync("Error", $"Could not open photo: {ex.Message}");
        }
    }

    private void CloseFullscreen()
    {
        IsFullscreenOverlayVisible = false;
        // Optioneel: reset CurrentPhoto om problemen te voorkomen bij volgende open
        // CurrentPhoto = null; // Uncomment als nodig
    }

    #endregion

    #region Private Methods - Rating

    private async Task StarClickedAsync(string? ratingStr)
    {
        if (CurrentPhoto == null || string.IsNullOrEmpty(ratingStr) || !int.TryParse(ratingStr, out int rating))
            return;

        try
        {
            if (CurrentPhoto.Rating == rating)
            {
                CurrentPhoto.Rating = 0;
            }
            else
            {
                CurrentPhoto.Rating = rating;
            }

            await _databaseService.UpdatePhotoAsync(CurrentPhoto);
            CurrentPhotoRating = CurrentPhoto.Rating;

            System.Diagnostics.Debug.WriteLine($"Rating updated to {CurrentPhoto.Rating} for photo {CurrentPhoto.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating rating: {ex.Message}");
            await ShowAlertAsync("Error", "Could not save rating.");
        }
    }

    #endregion

    #region Private Methods - Labels

    private async Task AddLabelAsync()
    {
        if (CurrentPhoto == null)
        {
            await ShowAlertAsync("No Photo Selected", "Please select a photo first to add a label.");
            return;
        }

        if (string.IsNullOrWhiteSpace(LabelEntryText))
        {
            await ShowAlertAsync("Empty Label", "Please enter a label name.");
            return;
        }

        await AddLabelFromTextAsync(LabelEntryText);
        LabelEntryText = string.Empty; // Clear entry after adding
    }

    /// <summary>
    /// Deletes a label from all photos and the database.
    /// </summary>
    /// <param name="label"></param>
    /// <returns></returns>
    private async Task DeleteLabelAsync(string? label)
    {
        // Makes sure the label gets removed from all photos and the database

        if (string.IsNullOrWhiteSpace(label))
            return;
        try
            {
            bool confirm = await Application.Current?.MainPage?.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete the label '{label}' from all photos and the database?",
                "Yes",
                "No");
            if (!confirm)
                return;
            await _databaseService.DeleteLabelFromAllPhotosAsync(label);
            
            // Update labels in all photos
            foreach (var photo in _allPhotos)
            {
                if (photo.Labels.Contains(label))
                {
                    photo.Labels.Remove(label);
                    await _databaseService.RemoveLabelAsync(photo.Id, label);
                }
            }
            UpdateAvailableLabels();
            ApplyFiltersAndSort();
            await ShowAlertAsync("Label Deleted", $"The label '{label}' has been deleted from all photos.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting label: {ex.Message}");
            await ShowAlertAsync("Error", $"Could not delete label: {ex.Message}");
        }
    }

    public async Task AddLabelFromTextAsync(string? labelText)
    {
        if (CurrentPhoto == null)
            return;

        if (string.IsNullOrWhiteSpace(labelText))
        {
            await ShowAlertAsync("Empty Label", "Please enter a label name.");
            return;
        }

        var newLabel = labelText.Trim();

        if (!IsValidLabelText(newLabel))
        {
            await ShowAlertAsync("Invalid Characters", "Labels may only contain letters, numbers, and spaces. Special characters are not allowed.");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to add label: '{newLabel}' to photo ID: {CurrentPhoto.Id}");

            var result = await _databaseService.AddLabelAsync(CurrentPhoto.Id, newLabel);

            System.Diagnostics.Debug.WriteLine($"AddLabelAsync returned: {result}");

            if (result == 0)
            {
                System.Diagnostics.Debug.WriteLine($"Label '{newLabel}' already exists for photo {CurrentPhoto.Id}");
                await ShowAlertAsync("Label Already Exists", $"The label '{newLabel}' already exists for this photo.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Label '{newLabel}' successfully added with ID: {result}");

            await _databaseService.LoadLabelsForPhotoAsync(CurrentPhoto);
            
            // Force UI refresh on Windows by explicitly notifying CurrentPhoto changed
            // Windows may not detect ObservableCollection changes in bindings, so we force a property change
            if (CurrentPhoto != null)
            {
                var photoRef = CurrentPhoto;
                var wasVisible = IsFullscreenOverlayVisible;
                
                // Temporarily refresh CurrentPhoto property to force binding update on Windows
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CurrentPhoto = null;
                    CurrentPhoto = photoRef;
                    // Ensure overlay stays visible if it was visible
                    if (wasVisible)
                        IsFullscreenOverlayVisible = true;
                });
            }
            
            UpdateAvailableLabels();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding label: {ex.Message}\n{ex.StackTrace}");
            await ShowAlertAsync("Error", $"Failed to add label: {ex.Message}");
        }
    }

    private async Task RemoveLabelAsync(string? label)
    {
        if (CurrentPhoto == null || string.IsNullOrEmpty(label) || !CurrentPhoto.Labels.Contains(label))
            return;

        try
        {
            await _databaseService.RemoveLabelAsync(CurrentPhoto.Id, label);
            await _databaseService.LoadLabelsForPhotoAsync(CurrentPhoto);
            
            // Force UI refresh on Windows by explicitly notifying CurrentPhoto changed
            if (CurrentPhoto != null)
            {
                var photoRef = CurrentPhoto;
                var wasVisible = IsFullscreenOverlayVisible;
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CurrentPhoto = null;
                    CurrentPhoto = photoRef;
                    // Ensure overlay stays visible if it was visible
                    if (wasVisible)
                        IsFullscreenOverlayVisible = true;
                });
            }
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"Failed to remove label: {ex.Message}");
        }
    }

    private async Task LabelDropdownAsync()
    {
        if (CurrentPhoto == null)
        {
            await ShowAlertAsync("No Photo Selected", "Please select a photo first to add a label.");
            return;
        }

        await LoadAvailableLabelsAsync();

        if (AvailableLabels.Count == 0)
        {
            await ShowAlertAsync("No Labels", "No labels are available yet. Please add a label via the text box first.");
            return;
        }

        var options = AvailableLabels.ToList();
        var selectedLabel = await Application.Current?.MainPage?.DisplayActionSheet(
            "Select a label to add",
            "Cancel",
            null,
            options.ToArray());

        if (selectedLabel != null && selectedLabel != "Cancel" && !string.IsNullOrWhiteSpace(selectedLabel))
        {
            await AddLabelFromTextAsync(selectedLabel);
        }
    }

    /// <summary>
    /// Shows an action sheet to select and remove a label from all photos.
    /// </summary>
    /// <returns></returns>
    private async Task RemoveLabelSidebarAsync()
    {

        await LoadAvailableLabelsAsync();

        // Display all labels in an action sheet for selection
        if (AvailableLabels.Count == 0)
        {
            await ShowAlertAsync("No Labels", "No labels are available yet. Please add a label via the text box first.");
            return;
        }
        var options = AvailableLabels.ToList();
        var selectedLabel = await Application.Current?.MainPage?.DisplayActionSheet(
            "All labels (Select to remove from photos and database)",
            "Close",
            null,
            options.ToArray());

        if (selectedLabel != null && selectedLabel != "Close" && !string.IsNullOrWhiteSpace(selectedLabel))
        {
            await DeleteLabelAsync(selectedLabel);
        }

    }

    private async Task LoadAvailableLabelsAsync()
    {
        try
        {
            var labels = await _databaseService.GetAllUniqueLabelsAsync();
            AvailableLabels.Clear();
            foreach (var label in labels)
            {
                AvailableLabels.Add(label);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading available labels: {ex.Message}");
        }
    }

    private bool IsValidLabelText(string labelText)
    {
        if (string.IsNullOrWhiteSpace(labelText))
            return false;

        foreach (char c in labelText)
        {
            if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Private Methods - Selection

    private void ToggleSelectionMode()
    {
        IsSelectionMode = !IsSelectionMode;

        if (IsSelectionMode)
        {
            SelectButtonText = $"Cancel ({SelectedPhotosCount})";
            SelectionActionsButtonVisible = true;
        }
        else
        {
            SelectButtonText = "Select";
            SelectionActionsButtonVisible = false;

            foreach (var photo in _selectedPhotos)
            {
                photo.IsSelected = false;
            }
            _selectedPhotos.Clear();
            SelectedPhotosCount = 0;
        }
    }

    private async Task SelectionActionsAsync()
    {
        if (_selectedPhotos.Count == 0)
        {
            await ShowAlertAsync("No Selection", "Please select photos first to perform actions.");
            return;
        }

        var action = await Application.Current?.MainPage?.DisplayActionSheet(
            $"{_selectedPhotos.Count} photos selected",
            "Cancel",
            "Delete",
            "Export to Photo Book",
            "Add Labels");

        switch (action)
        {
            case "Delete":
                await DeleteSelectedPhotosAsync();
                break;
            case "Export to Photo Book":
                await ShowAlertAsync("Info", "Photo book export coming soon!");
                break;
            case "Add Labels":
                await AddLabelsToSelectedPhotosAsync();
                break;
        }
    }

    private async Task DeleteSelectedPhotosAsync()
    {
        if (_selectedPhotos.Count == 0)
            return;

        if (Application.Current?.MainPage == null)
            return;

        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Confirm Delete",
            $"Are you sure you want to delete {_selectedPhotos.Count} photos?",
            "Yes",
            "No");

        if (!confirm)
            return;

        try
        {
            var photosToDelete = _selectedPhotos.ToList();

            int successCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            foreach (var photo in photosToDelete)
            {
                photo.IsSelected = false;

                // Try to delete file, but continue even if it fails
                if (File.Exists(photo.FilePath))
                {
                    try
                    {
                        File.Delete(photo.FilePath);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cannot delete file (permission denied): {photo.FilePath}. Error: {ex.Message}");
                        errors.Add($"Permission denied: {photo.FileName}");
                    }
                    catch (IOException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cannot delete file (in use): {photo.FilePath}. Error: {ex.Message}");
                        errors.Add($"File in use: {photo.FileName}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error deleting file: {photo.FilePath}. Error: {ex.Message}");
                        errors.Add($"Error deleting {photo.FileName}: {ex.Message}");
                    }
                }

                // Always delete from database, even if file deletion failed
                try
                {
                    await _databaseService.DeletePhotoAsync(photo);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _allPhotos.Remove(photo);
                        Photos.Remove(photo);
                    });

                    successCount++;
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting photo from database: {dbEx.Message}");
                    errorCount++;
                    errors.Add($"Database error for {photo.FileName}: {dbEx.Message}");
                }
            }

            // Show appropriate message
            if (errorCount == 0 && errors.Count == 0)
            {
                await ShowAlertAsync("Done", $"{successCount} photo(s) deleted.");
            }
            else if (successCount > 0)
            {
                string message = $"{successCount} photo(s) removed from gallery.";
                if (errors.Count > 0)
                {
                    message += $"\n\nNote: Some files could not be deleted:\n{string.Join("\n", errors.Take(5))}";
                    if (errors.Count > 5)
                        message += $"\n... and {errors.Count - 5} more.";
                }
                await ShowAlertAsync("Partially Complete", message);
            }
            else
            {
                await ShowAlertAsync("Error", $"Could not delete photos:\n{string.Join("\n", errors.Take(5))}");
            }

            _selectedPhotos.Clear();
            SelectedPhotosCount = 0;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateAvailableLabels();
                ApplyFiltersAndSort();
                HasPhotos = Photos.Count > 0;
            });

            IsSelectionMode = false;
            SelectButtonText = "Select";
            SelectionActionsButtonVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting photos: {ex.Message}");
            await ShowAlertAsync("Error", $"Error deleting: {ex.Message}");
        }
    }

    private async Task AddLabelsToSelectedPhotosAsync()
    {
        if (_selectedPhotos.Count == 0)
            return;

        try
        {
            var allLabels = await _databaseService.GetAllUniqueLabelsAsync();

            if (allLabels.Count == 0)
            {
                await ShowAlertAsync("No Labels Available", "No labels have been created yet. Please add labels to individual photos first.");
                return;
            }

            if (Application.Current?.MainPage == null)
                return;
                
            var selectedLabel = await Application.Current.MainPage.DisplayActionSheet(
                "Select a label to add",
                "Cancel",
                null,
                allLabels.ToArray());

            if (selectedLabel == null || selectedLabel == "Cancel" || string.IsNullOrWhiteSpace(selectedLabel))
                return;

            int successCount = 0;
            int alreadyExistsCount = 0;
            int errorCount = 0;

            foreach (var photo in _selectedPhotos.ToList())
            {
                try
                {
                    if (photo.Labels.Any(l => string.Equals(l, selectedLabel, StringComparison.OrdinalIgnoreCase)))
                    {
                        alreadyExistsCount++;
                        continue;
                    }

                    var result = await _databaseService.AddLabelAsync(photo.Id, selectedLabel);

                    if (result > 0)
                    {
                        await _databaseService.LoadLabelsForPhotoAsync(photo);
                        successCount++;
                    }
                    else
                    {
                        alreadyExistsCount++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error adding label to photo {photo.Id}: {ex.Message}");
                    errorCount++;
                }
            }

            UpdateAvailableLabels();

            string message = $"{successCount} photo(s) updated";
            if (alreadyExistsCount > 0)
            {
                message += $", {alreadyExistsCount} photo(s) already had this label";
            }
            if (errorCount > 0)
            {
                message += $", {errorCount} error(s) occurred";
            }

            await ShowAlertAsync("Labels Added", message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in AddLabelsToSelectedPhotosAsync: {ex.Message}");
            await ShowAlertAsync("Error", "Could not add labels to selected photos.");
        }
    }

    #endregion

    #region Private Methods - Filtering & Sorting

    private void UpdateAvailableLabels()
    {
        AvailableLabels.Clear();
        var labels = _allPhotos
            .SelectMany(p => p.Labels)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l)
            .ToList();

        foreach (var label in labels)
        {
            AvailableLabels.Add(label);
        }
    }

    private void ApplyFiltersAndSort()
    {
        var filteredPhotos = _allPhotos.AsEnumerable();

        if (!string.IsNullOrEmpty(SelectedLabelFilter))
        {
            filteredPhotos = filteredPhotos.Where(p =>
                p.Labels.Any(l => string.Equals(l, SelectedLabelFilter, StringComparison.OrdinalIgnoreCase)));
        }

        switch (RatingSortOrder)
        {
            case RatingSortOrder.Highest:
                filteredPhotos = filteredPhotos.OrderByDescending(p => p.Rating).ThenByDescending(p => p.CreatedDate);
                break;
            case RatingSortOrder.Lowest:
                filteredPhotos = filteredPhotos.OrderBy(p => p.Rating == 0 ? int.MaxValue : p.Rating).ThenByDescending(p => p.CreatedDate);
                break;
            default:
                if (SortNewestFirst)
                {
                    filteredPhotos = filteredPhotos.OrderByDescending(p => p.CreatedDate);
                }
                else
                {
                    filteredPhotos = filteredPhotos.OrderBy(p => p.CreatedDate);
                }
                break;
        }

        Photos.Clear();
        foreach (var photo in filteredPhotos)
        {
            Photos.Add(photo);
        }

        HasPhotos = Photos.Count > 0;
    }

    private async Task FilterLabelAsync()
    {
        var options = new List<string> { "All photos" };
        if (AvailableLabels != null && AvailableLabels.Count > 0)
        {
            options.AddRange(AvailableLabels);
        }

        if (Application.Current?.MainPage == null)
            return;

        var selected = await Application.Current.MainPage.DisplayActionSheet(
            "Filter by label",
            "Cancel",
            null,
            options.ToArray());

        if (selected != null && selected != "Cancel")
        {
            if (selected == "All photos")
            {
                SelectedLabelFilter = null;
                FilterLabelButtonText = "Filter: Label";
            }
            else
            {
                SelectedLabelFilter = selected;
                FilterLabelButtonText = $"Filter: {selected}";
            }

            ApplyFiltersAndSort();
        }
    }

    private void ToggleSortDate()
    {
        SortNewestFirst = !SortNewestFirst;

        if (SortNewestFirst)
        {
            SortDateButtonText = "Newest ↑";
        }
        else
        {
            SortDateButtonText = "Oldest ↑";
        }

        ApplyFiltersAndSort();
    }

    private void ToggleFilterRating()
    {
        switch (RatingSortOrder)
        {
            case RatingSortOrder.None:
                RatingSortOrder = RatingSortOrder.Highest;
                FilterRatingButtonText = "Filter: Rating ↑";
                break;
            case RatingSortOrder.Highest:
                RatingSortOrder = RatingSortOrder.Lowest;
                FilterRatingButtonText = "Filter: Rating ↓";
                break;
            case RatingSortOrder.Lowest:
                RatingSortOrder = RatingSortOrder.None;
                FilterRatingButtonText = "Filter: Rating";
                break;
        }

        ApplyFiltersAndSort();
    }

    #endregion

    #region Private Methods - Navigation & UI

    private void ToggleSidebar()
    {
        // This needs to be handled by the View as it directly manipulates UI elements
        // TODO: Use messaging or event to communicate with View
    }

    private async Task OpenPhotoBookAsync()
    {
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.Navigation.PushAsync(new PhotoBookManagementPage());
                return;
            }

            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.Navigation.PushAsync(new PhotoBookManagementPage());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error navigating to PhotoBookManagementPage: {ex.Message}");
            await ShowAlertAsync("Error", $"Could not open Photo Book: {ex.Message}");
        }
    }

    private async Task ShowAlertAsync(string title, string message)
    {
        if (Application.Current?.MainPage != null)
        {
            await Application.Current.MainPage.DisplayAlert(title, message, "OK");
        }
    }

    #endregion

    #region Property Change Handlers

    partial void OnSelectedLabelFilterChanged(string? value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnSortNewestFirstChanged(bool value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnRatingSortOrderChanged(RatingSortOrder value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnCurrentPhotoChanged(PhotoItem? value)
    {
        // Update rating en labels wanneer CurrentPhoto verandert
        if (value != null)
        {
            CurrentPhotoRating = value.Rating;
            // Zorg dat labels geladen zijn voordat we CurrentPhoto zetten
            // (dit wordt al gedaan in ShowPhotoOverlayAsync, maar als extra check)
            
            // TODO: If photo has location data, trigger map update event
            // MapLocationUpdateRequested?.Invoke(latitude, longitude);
        }
        else
        {
            // Reset rating wanneer geen photo geselecteerd is
            CurrentPhotoRating = 0;
        }
    }

    partial void OnCurrentPhotoRatingChanged(int value)
    {
        // Update the photo's rating if current photo exists
        if (CurrentPhoto != null && CurrentPhoto.Rating != value)
        {
            CurrentPhoto.Rating = value;
        }
    }

    #endregion
}

