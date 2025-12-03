using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using PictureGallery.Models;
using PictureGallery.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PictureGallery.Views;

public partial class Gallery : ContentPage
{
    private static readonly string[] AllowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
    public ObservableCollection<PhotoItem> Photos { get; } = new();
    private ObservableCollection<string> AvailableLabels { get; } = new();

    private PhotoItem? currentPhoto;
    private readonly DatabaseService _databaseService;

    public Gallery()
    {
        InitializeComponent();
        BindingContext = this;
        _databaseService = new DatabaseService();
        
        // Explicitly set ItemsSource to ensure binding works
        PhotosCollection.ItemsSource = Photos;
        
        // Load photos immediately since OnAppearing might not be called when used as ContentView
        _ = LoadPhotosFromDatabaseAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("Gallery.OnAppearing called - loading photos from database");
        await LoadPhotosFromDatabaseAsync();
    }

    /// <summary>
    /// Load all photos from the database
    /// </summary>
    private async Task LoadPhotosFromDatabaseAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Loading photos from database...");
            var photos = await _databaseService.GetAllPhotosAsync();
            
            System.Diagnostics.Debug.WriteLine($"Retrieved {photos.Count} photos from database");
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Photos.Clear();
                foreach (var photo in photos)
                {
                    Photos.Add(photo);
                }

                PhotoCountLabel.IsVisible = Photos.Count > 0;
                PhotosCollection.ItemsSource = Photos;
                
                System.Diagnostics.Debug.WriteLine($"Added {Photos.Count} photos to UI collection");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading photos: {ex.Message}\n{ex.StackTrace}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load photos: {ex.Message}", "OK");
        }
    }

    private async void UploadMedia(object sender, EventArgs e)
    {
        try
        {
            IEnumerable<FileResult>? results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Select images (PNG / JPG)"
            });

            if (results != null && results.Any())
            {
                var addedPhotos = new List<PhotoItem>();

                foreach (var result in results)
                {
                    if (!IsSupportedImage(result.FileName))
                        continue;

                    var photo = await CreatePhotoItemAsync(result);
                    if (photo != null && photo.IsValid)
                    {
                        try
                        {
                            // Save photo to database first (this sets the Id)
                            await _databaseService.AddPhotoAsync(photo);
                            System.Diagnostics.Debug.WriteLine($"Photo saved to database with ID: {photo.Id}");
                        }
                        catch (Exception dbEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Database error: {dbEx.Message}");
                            await Application.Current.MainPage.DisplayAlert("Database Error", $"Failed to save photo: {dbEx.Message}", "OK");
                            continue;
                        }
                        
                        // Ensure ImageSource is set (should already be set in CreatePhotoItemAsync)
                        if (photo.ImageSource == null)
                        {
                            if (photo.FileExists)
                            {
                                await MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    photo.ImageSource = ImageSource.FromFile(photo.FilePath);
                                });
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"File does not exist: {photo.FilePath}");
                                continue;
                            }
                        }
                        
                        // Verify ImageSource is set and file exists before adding
                        if (photo.ImageSource != null && photo.FileExists)
                        {
                            System.Diagnostics.Debug.WriteLine($"Adding photo: {photo.FileName}, Path: {photo.FilePath}, ImageSource: {photo.ImageSource != null}, Id: {photo.Id}");
                            addedPhotos.Add(photo);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipping photo: ImageSource={photo.ImageSource != null}, FileExists={photo.FileExists}");
                        }
                    }
                }

                // Add to collection and force UI refresh
                if (addedPhotos.Any())
                {
                    // Add photos to collection (newest first) - do this on main thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var photo in addedPhotos)
                        {
                            Photos.Insert(0, photo);
                        }
                        
                        PhotoCountLabel.IsVisible = Photos.Count > 0;
                        
                        // Explicitly set ItemsSource to ensure UI updates
                        PhotosCollection.ItemsSource = Photos;
                    });

                    // Debug: Check if photos are actually in collection
                    System.Diagnostics.Debug.WriteLine($"Added {addedPhotos.Count} photos. Total in collection: {Photos.Count}");
                    foreach (var photo in addedPhotos)
                    {
                        System.Diagnostics.Debug.WriteLine($"Photo: {photo.FileName}, ImageSource: {photo.ImageSource != null}, FilePath: {photo.FilePath}, Id: {photo.Id}");
                    }

                    currentPhoto = addedPhotos.FirstOrDefault();
                }
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task<PhotoItem?> CreatePhotoItemAsync(FileResult result)
    {
        // Always copy to permanent storage location
        var photosDirectory = Path.Combine(FileSystem.AppDataDirectory, "Photos");
        if (!Directory.Exists(photosDirectory))
        {
            Directory.CreateDirectory(photosDirectory);
        }

        // Generate unique filename to avoid conflicts
        var extension = Path.GetExtension(result.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var permanentPath = Path.Combine(photosDirectory, uniqueFileName);

        // Copy file to permanent location
        await using (var pickedStream = await result.OpenReadAsync())
        {
            await using (var permanentFile = File.Create(permanentPath))
            {
                await pickedStream.CopyToAsync(permanentFile);
                await permanentFile.FlushAsync(); // Ensure file is fully written
            } // File stream is closed here
        } // Picked stream is closed here

        // Small delay to ensure file system has released the file
        await Task.Delay(50);

        // Verify file exists and has content
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

        // Read image dimensions - retry if file is locked
        int width = 0, height = 0;
        int retries = 3;
        bool success = false;
        
        while (retries > 0 && !success)
        {
            try
            {
                using (var stream = File.OpenRead(permanentPath))
                using (var bitmap = SKBitmap.Decode(stream))
                {
                    if (bitmap != null)
                    {
                        width = bitmap.Width;
                        height = bitmap.Height;
                        success = true;
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
                    System.Diagnostics.Debug.WriteLine($"Error reading image dimensions after retries: {ex.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading image dimensions: {ex.Message}");
                return null;
            }
        }

        var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

        var photo = new PhotoItem
        {
            FileName = result.FileName, // Keep original filename for display
            FilePath = permanentPath,   // Store permanent path in database
            Width = width,
            Height = height,
            FileSizeMb = fileSizeMB
        };

        // Initialize ImageSource immediately - use FromFile directly
        // This must be done on main thread for MAUI
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            photo.ImageSource = ImageSource.FromFile(permanentPath);
        });

        System.Diagnostics.Debug.WriteLine($"Created photo: {photo.FileName}, Path: {photo.FilePath}, ImageSource: {photo.ImageSource != null}, Size: {width}x{height}");

        return photo;
    }

    private async void OnPhotoTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is PhotoItem tappedPhoto)
        {
            currentPhoto = tappedPhoto;
            await ShowPhotoOverlay(tappedPhoto);
        }
    }

    private async Task ShowPhotoOverlay(PhotoItem photo)
    {
        if (photo.ImageSource == null)
            photo.InitializeImageSource();

        if (photo.ImageSource == null)
            return;

        // Ensure labels are loaded from database
        if (photo.Id > 0)
        {
            await _databaseService.LoadLabelsForPhotoAsync(photo);
        }

        FullscreenImage.Source = photo.ImageSource;
        OverlayFileName.Text = photo.FileName;
        OverlayDimensions.Text = photo.DimensionsText;
        OverlayFileSize.Text = photo.FileSizeText;
        FullscreenOverlay.IsVisible = true;

        DisplayLabels(photo);
    }

    private void CloseFullscreen(object sender, EventArgs e)
    {
        FullscreenOverlay.IsVisible = false;
    }

    private async void AddLabelButton_Clicked(object sender, EventArgs e)
    {
        if (currentPhoto == null)
        {
            await Application.Current.MainPage.DisplayAlert("Geen foto geselecteerd", "Selecteer eerst een foto om een label toe te voegen.", "OK");
            return;
        }

        var newLabel = LabelEntry.Text?.Trim();
        if (string.IsNullOrEmpty(newLabel))
        {
            await Application.Current.MainPage.DisplayAlert("Leeg label", "Voer een label naam in.", "OK");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to add label: '{newLabel}' to photo ID: {currentPhoto.Id}");
            
            // Add label to database (returns 0 if label already exists, > 0 if added)
            var result = await _databaseService.AddLabelAsync(currentPhoto.Id, newLabel);
            
            System.Diagnostics.Debug.WriteLine($"AddLabelAsync returned: {result}");
            
            if (result == 0)
            {
                // Label already exists (case-insensitive)
                System.Diagnostics.Debug.WriteLine($"Label '{newLabel}' already exists for photo {currentPhoto.Id}");
                await Application.Current.MainPage.DisplayAlert("Label bestaat al", $"Het label '{newLabel}' bestaat al voor deze foto.", "OK");
                LabelEntry.Text = string.Empty;
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"Label '{newLabel}' successfully added with ID: {result}");
            
            // Reload labels from database to ensure consistency
            await _databaseService.LoadLabelsForPhotoAsync(currentPhoto);
            
            LabelEntry.Text = string.Empty;
            DisplayLabels(currentPhoto);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding label: {ex.Message}\n{ex.StackTrace}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to add label: {ex.Message}", "OK");
        }
    }

    private async void LabelDropdownButton_Clicked(object? sender, EventArgs e)
    {
        if (currentPhoto == null)
        {
            await Application.Current.MainPage.DisplayAlert("Geen foto geselecteerd", "Selecteer eerst een foto om een label toe te voegen.", "OK");
            return;
        }

        // Toggle dropdown visibility
        if (LabelDropdown.IsVisible)
        {
            LabelDropdown.IsVisible = false;
        }
        else
        {
            // Load all available labels
            await LoadAvailableLabelsAsync();
            
            if (AvailableLabels.Count == 0)
            {
                await Application.Current.MainPage.DisplayAlert("Geen labels", "Er zijn nog geen labels beschikbaar. Voeg eerst een label toe via het tekstvak.", "OK");
                return;
            }
            
            // Clear existing items and row definitions
            LabelDropdownList.Children.Clear();
            LabelDropdownList.RowDefinitions.Clear();
            
            // Create row definitions for each label
            for (int i = 0; i < AvailableLabels.Count; i++)
            {
                LabelDropdownList.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            
            // Create a clickable Frame for each label
            // Place each frame in its own row to ensure full width
            for (int i = 0; i < AvailableLabels.Count; i++)
            {
                var label = AvailableLabels[i];
                
                var frame = new Frame
                {
                    BackgroundColor = Colors.White,
                    BorderColor = Colors.Transparent,
                    HasShadow = false,
                    Padding = new Thickness(12, 8),
                    MinimumHeightRequest = 40,
                    HeightRequest = 40,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    CornerRadius = 0
                };
                
                var labelText = new Label
                {
                    Text = label,
                    FontSize = 14,
                    TextColor = Colors.Black,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Start,
                    InputTransparent = true
                };
                
                frame.Content = labelText;
                
                // Store the label for the click handler
                string capturedLabel = label;
                
                // Add tap gesture to the frame
                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += async (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Frame tapped: {capturedLabel}, sender: {s?.GetType().Name}");
                    await AddLabelFromDropdown(capturedLabel);
                };
                frame.GestureRecognizers.Add(tapGesture);
                
                // Add frame to Grid in its own row
                Grid.SetRow(frame, i);
                LabelDropdownList.Children.Add(frame);
            }
            
            System.Diagnostics.Debug.WriteLine($"Created {AvailableLabels.Count} label items in dropdown");
            
            // Position dropdown directly under the Labels button
            // The StackLayout is centered, and the button is the first element (80px wide)
            // The dropdown is 200px wide, so to align it with the button's left edge,
            // we need to shift it left. Since the Grid is centered and the dropdown has HorizontalOptions="Start",
            // it aligns to the Grid's left edge. We need to shift it left by approximately
            // half the StackLayout width minus half the dropdown width to align with the button.
            // StackLayout width ≈ 80 + 5 + 160 + 5 + 80 = 330px
            // To align dropdown (200px) with button (80px) that's at position (330-80)/2 = 125px from Grid left,
            // we shift left by: 125 - (200-80)/2 = 125 - 60 = 65px
            // Actually simpler: shift left by the button's position relative to Grid center
            // For now, let's use a negative margin to shift it left
            // The button starts at approximately 125px from Grid left (half of 330 - half of 80)
            // So we shift the dropdown left by 125px to align with button's left edge
            LabelDropdown.Margin = new Thickness(-125, 5, 0, 0);
            LabelDropdown.IsVisible = true;
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

    private async void LabelDropdown_ItemSelected(object? sender, SelectedItemChangedEventArgs e)
    {
        if (currentPhoto == null || e.SelectedItem == null)
            return;

        var selectedLabel = e.SelectedItem as string;
        if (string.IsNullOrEmpty(selectedLabel))
            return;

        System.Diagnostics.Debug.WriteLine($"ItemSelected: {selectedLabel}");
        
        // Clear selection immediately to allow re-selection
        if (sender is ListView listView)
        {
            listView.SelectedItem = null;
        }
        
        await AddLabelFromDropdown(selectedLabel);
    }

    private async void OnLabelItemTapped(object? sender, TappedEventArgs e)
    {
        if (currentPhoto == null)
            return;

        // Get the label text from the binding context
        string? selectedLabel = null;
        
        // Try to get from Grid (in ViewCell)
        if (sender is Grid grid)
        {
            if (grid.BindingContext is string gridLabel)
            {
                selectedLabel = gridLabel;
            }
            else if (grid.Parent is ViewCell viewCell && viewCell.BindingContext is string cellLabel)
            {
                selectedLabel = cellLabel;
            }
        }
        else if (sender is ViewCell viewCell2 && viewCell2.BindingContext is string cellLabel2)
        {
            selectedLabel = cellLabel2;
        }
        else if (sender is VisualElement element)
        {
            // Walk up the visual tree to find the ViewCell
            var parent = element.Parent;
            while (parent != null)
            {
                if (parent is ViewCell vc && vc.BindingContext is string vcLabel)
                {
                    selectedLabel = vcLabel;
                    break;
                }
                parent = parent.Parent;
            }
        }

        if (!string.IsNullOrEmpty(selectedLabel))
        {
            System.Diagnostics.Debug.WriteLine($"Label tapped: {selectedLabel}");
            await AddLabelFromDropdown(selectedLabel);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Could not get label from tapped element");
        }
    }

    private async Task AddLabelFromDropdown(string selectedLabel)
    {
        // Hide dropdown
        LabelDropdown.IsVisible = false;

        // Add the selected label to the photo
        try
        {
            System.Diagnostics.Debug.WriteLine($"Adding label from dropdown: '{selectedLabel}' to photo ID: {currentPhoto.Id}");
            
            var result = await _databaseService.AddLabelAsync(currentPhoto.Id, selectedLabel);
            
            if (result == 0)
            {
                // Label already exists (case-insensitive)
                await Application.Current.MainPage.DisplayAlert("Label bestaat al", $"Het label '{selectedLabel}' bestaat al voor deze foto.", "OK");
                return;
            }
            
            // Reload labels from database to ensure consistency
            await _databaseService.LoadLabelsForPhotoAsync(currentPhoto);
            
            DisplayLabels(currentPhoto);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding label from dropdown: {ex.Message}\n{ex.StackTrace}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to add label: {ex.Message}", "OK");
        }
    }

    private void DisplayLabels(PhotoItem photo)
    {
        LabelContainer.Children.Clear();
        foreach (var label in photo.Labels)
        {
            LabelContainer.Children.Add(CreateLabelChip(label));
        }
    }

    private View CreateLabelChip(string label)
    {
        var textLabel = new Label
        {
            Text = label,
            FontSize = 16,
            TextColor = Colors.Black,
            VerticalOptions = LayoutOptions.Center
        };

        var removeButton = new Button
        {
            Text = "×",
            WidthRequest = 22,
            HeightRequest = 22,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            CornerRadius = 11,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Colors.Black,
            CommandParameter = label,
            Margin = new Thickness(4, 0, 0, 0)
        };
        removeButton.Clicked += RemoveLabelButton_Clicked;

        var horizontal = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center
        };
        horizontal.Children.Add(textLabel);
        horizontal.Children.Add(removeButton);

        return new Frame
        {
            Padding = new Thickness(14, 8),
            Margin = new Thickness(0, 0, 8, 0),
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            CornerRadius = 20,
            HasShadow = false,
            Content = horizontal
        };
    }

    private async void RemoveLabelButton_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button button &&
            button.CommandParameter is string label &&
            currentPhoto != null &&
            currentPhoto.Labels.Contains(label))
        {
            try
            {
                // Remove label from database
                await _databaseService.RemoveLabelAsync(currentPhoto.Id, label);
                
                // Reload labels from database to ensure consistency
                await _databaseService.LoadLabelsForPhotoAsync(currentPhoto);
                
                DisplayLabels(currentPhoto);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to remove label: {ex.Message}", "OK");
            }
        }
    }

    /// <summary>
    /// Clear all photos from database and file system (for testing)
    /// </summary>
    private async Task ClearAllPhotosAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Clearing all photos from database...");
            
            // Get all photos first to delete their files
            var allPhotos = await _databaseService.GetAllPhotosAsync();
            
            // Delete photo files from disk
            foreach (var photo in allPhotos)
            {
                try
                {
                    if (File.Exists(photo.FilePath))
                    {
                        File.Delete(photo.FilePath);
                        System.Diagnostics.Debug.WriteLine($"Deleted file: {photo.FilePath}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting file {photo.FilePath}: {ex.Message}");
                }
            }
            
            // Clear database
            await _databaseService.ClearAllDataAsync();
            
            // Clear UI collection
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Photos.Clear();
                PhotosCollection.ItemsSource = Photos;
                PhotoCountLabel.IsVisible = false;
            });
            
            System.Diagnostics.Debug.WriteLine("All photos cleared from database and file system");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing photos: {ex.Message}");
        }
    }

    private bool IsSupportedImage(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private void ToggleSidebar_Clicked(object? sender, EventArgs e)
    {
        if (Sidebar != null && MainGrid != null && MainGrid.ColumnDefinitions.Count > 0)
        {
            bool isVisible = Sidebar.IsVisible;
            Sidebar.IsVisible = !isVisible;
            
            // Adjust column width: 220 when open, 0 when closed
            if (isVisible)
            {
                // Sidebar is open, close it
                MainGrid.ColumnDefinitions[0].Width = 0;
            }
            else
            {
                // Sidebar is closed, open it
                MainGrid.ColumnDefinitions[0].Width = 220;
            }
        }
    }

    private void ImportPhoto_Tapped(object? sender, TappedEventArgs e)
    {
        // Call the existing UploadMedia method
        UploadMedia(sender ?? this, EventArgs.Empty);
    }
}
