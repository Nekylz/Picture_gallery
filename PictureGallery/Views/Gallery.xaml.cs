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

    // Selection mode properties
    private bool _isSelectionMode = false;
    private HashSet<PhotoItem> _selectedPhotos = new();
    
    // Filter and sort properties
    private string? _selectedLabelFilter = null; // null = alle labels
    private bool _sortNewestFirst = true; // true = newest first, false = oldest first
    private List<PhotoItem> _allPhotos = new(); // Alle foto's (voor filtering)

    public Gallery()
    {
        InitializeComponent();
        BindingContext = this;
        _databaseService = new DatabaseService();
        
        // Stel expliciet ItemsSource in om ervoor te zorgen dat de binding werkt
        PhotosCollection.ItemsSource = Photos;
        
        // Zet initial state - toon EmptyStateView als er nog geen foto's zijn
        RefreshPhotosView();
        
        // Laad foto's direct omdat OnAppearing mogelijk niet wordt aangeroepen wanneer Gallery als ContentView wordt gebruikt
        _ = LoadPhotosFromDatabaseAsync();
    }

    /// <summary>
    /// Forceert een refresh van de CollectionView zodat EmptyView correct verschijnt
    /// en de foto-telling zichtbaar/onzichtbaar wordt gezet.
    /// </summary>
    private void RefreshPhotosView()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            bool hasPhotos = Photos.Count > 0;
            PhotoCountLabel.IsVisible = hasPhotos;
            
            // Toon/verberg EmptyStateView en PhotosCollection op basis van aantal foto's
            if (EmptyStateView != null)
            {
                EmptyStateView.IsVisible = !hasPhotos;
            }
            if (PhotosCollection != null)
            {
                PhotosCollection.IsVisible = hasPhotos;
                if (hasPhotos)
                {
                    // Refresh ItemsSource alleen als er foto's zijn
                    var current = Photos;
                    PhotosCollection.ItemsSource = null;
                    PhotosCollection.ItemsSource = current;
                }
            }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("Gallery.OnAppearing called - loading photos from database");
        await LoadPhotosFromDatabaseAsync();
    }

    /// <summary>
    /// Laad alle foto's uit de database en toon ze in de UI
    /// </summary>
    private async Task LoadPhotosFromDatabaseAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Loading photos from database...");
            var photos = await _databaseService.GetAllPhotosAsync();
            
            System.Diagnostics.Debug.WriteLine($"Retrieved {photos.Count} photos from database");
            
            // UI updates moeten op de main thread gebeuren
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _allPhotos = photos;
                
                // Update available labels voor filter dropdown
                UpdateAvailableLabels();
                
                // Pas filters en sorting toe
                ApplyFiltersAndSort();
                
                System.Diagnostics.Debug.WriteLine($"Added {Photos.Count} photos to UI collection (filtered from {_allPhotos.Count})");
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

                    PhotoItem? photo = null;
                    try
                    {
                        photo = await CreatePhotoItemAsync(result);
                    }
                    catch (Exception photoEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating photo from {result.FileName}: {photoEx.Message}");
                        await Application.Current.MainPage.DisplayAlert(
                            "Corrupte foto",
                            $"De foto '{result.FileName}' kon niet worden geladen. Het bestand is beschadigd.",
                            "OK");
                        continue;
                    }
                    
                    if (photo != null && photo.IsValid)
                    {
                        try
                        {
                            // Sla foto eerst op in de database (dit zet de Id)
                            await _databaseService.AddPhotoAsync(photo);
                            System.Diagnostics.Debug.WriteLine($"Photo saved to database with ID: {photo.Id}");
                        }
                        catch (Exception dbEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Database error: {dbEx.Message}");
                            await Application.Current.MainPage.DisplayAlert("Database Error", $"Failed to save photo: {dbEx.Message}", "OK");
                            continue;
                        }
                        
                        // Zorg ervoor dat ImageSource is ingesteld (zou al moeten zijn ingesteld in CreatePhotoItemAsync)
                        if (photo.ImageSource == null)
                        {
                            if (photo.FileExists)
                            {
                                try
                                {
                                    // ImageSource moet op de main thread worden ingesteld
                                    await MainThread.InvokeOnMainThreadAsync(() =>
                                    {
                                        try
                                        {
                                            photo.ImageSource = ImageSource.FromFile(photo.FilePath);
                                        }
                                        catch (Exception imgEx)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Error creating ImageSource for existing file: {imgEx.Message}");
                                            throw;
                                        }
                                    });
                                }
                                catch (Exception imgEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to create ImageSource: {imgEx.Message}");
                                    await Application.Current.MainPage.DisplayAlert(
                                        "Corrupte foto",
                                        $"De foto '{photo.FileName}' kon niet worden geladen. Het bestand is beschadigd.",
                                        "OK");
                                    // Verwijder uit database als het daar al in staat
                                    try
                                    {
                                        await _databaseService.DeletePhotoAsync(photo);
                                    }
                                    catch { }
                                    continue;
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"File does not exist: {photo.FilePath}");
                                continue;
                            }
                        }
                        
                        // Verifieer dat ImageSource is ingesteld en bestand bestaat voordat we het toevoegen
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

                // Voeg toe aan collectie en forceer UI refresh
                if (addedPhotos.Any())
                {
                    // Voeg foto's toe aan collectie - doe dit op de main thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Voeg toe aan _allPhotos
                        foreach (var photo in addedPhotos)
                        {
                            _allPhotos.Insert(0, photo); // Insert aan het begin voor nieuwste eerst
                        }
                        
                        // Update available labels
                        UpdateAvailableLabels();
                        
                        // Pas filters en sorting toe
                        ApplyFiltersAndSort();
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

    /// <summary>
    /// Maakt een PhotoItem aan van een geselecteerd bestand
    /// Kopieert het bestand naar permanente opslag en leest de afbeeldingsafmetingen
    /// </summary>
    private async Task<PhotoItem?> CreatePhotoItemAsync(FileResult result)
    {
        // Kopieer altijd naar permanente opslaglocatie (niet naar cache)
        var photosDirectory = Path.Combine(FileSystem.AppDataDirectory, "Photos");
        if (!Directory.Exists(photosDirectory))
        {
            Directory.CreateDirectory(photosDirectory);
        }

        // Genereer unieke bestandsnaam om conflicten te voorkomen
        var extension = Path.GetExtension(result.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var permanentPath = Path.Combine(photosDirectory, uniqueFileName);

        // Kopieer bestand naar permanente locatie
        try
        {
            await using (var pickedStream = await result.OpenReadAsync())
            {
                await using (var permanentFile = File.Create(permanentPath))
                {
                    await pickedStream.CopyToAsync(permanentFile);
                    await permanentFile.FlushAsync(); // Zorg ervoor dat bestand volledig is geschreven
                } // File stream wordt hier gesloten
            } // Picked stream wordt hier gesloten
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying file: {ex.Message}");
            // Probeer het bestand te verwijderen als het is aangemaakt
            try
            {
                if (File.Exists(permanentPath))
                    File.Delete(permanentPath);
            }
            catch { }
            throw new InvalidOperationException($"Kon bestand niet kopiëren: {ex.Message}", ex);
        }

        // Korte vertraging om ervoor te zorgen dat het bestandssysteem het bestand heeft vrijgegeven
        await Task.Delay(50);

        // Verifieer dat bestand bestaat en inhoud heeft
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

        // Lees afbeeldingsafmetingen - probeer opnieuw als bestand is vergrendeld
        // Dit kan gebeuren als het bestand nog niet volledig is vrijgegeven door het OS
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
                        // Bitmap kon niet worden gedecode - waarschijnlijk corrupte afbeelding
                        retries = 0; // Stop retries
                        throw new InvalidOperationException("Het bestand kon niet als afbeelding worden gelezen. Het bestand is mogelijk beschadigd of geen geldige afbeelding.");
                    }
                    
                    try
                    {
                        // Valideer dat de bitmap geldige afmetingen heeft
                        if (bitmap.Width <= 0 || bitmap.Height <= 0)
                        {
                            bitmap.Dispose();
                            retries = 0;
                            throw new InvalidOperationException("De afbeelding heeft geen geldige afmetingen.");
                        }
                        
                        width = bitmap.Width;
                        height = bitmap.Height;
                        success = true;
                    }
                    finally
                    {
                        // Zorg ervoor dat bitmap wordt vrijgegeven
                        bitmap?.Dispose();
                    }
                }
            }
            catch (IOException ex)
            {
                // Bestand is mogelijk nog vergrendeld, probeer opnieuw
                retries--;
                if (retries > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"File locked, retrying... ({retries} attempts left)");
                    await Task.Delay(100);
                }
                else
                {
                    // Verwijder corrupt bestand
                    try
                    {
                        if (File.Exists(permanentPath))
                            File.Delete(permanentPath);
                    }
                    catch { }
                    
                    System.Diagnostics.Debug.WriteLine($"Error reading image dimensions after retries: {ex.Message}");
                    throw new InvalidOperationException($"Kon afbeelding niet lezen na {retries + 3} pogingen: {ex.Message}", ex);
                }
            }
            catch (InvalidOperationException)
            {
                // Re-throw invalid operation exceptions (corrupte afbeelding)
                retries = 0;
                // Verwijder corrupt bestand
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
                // Andere exceptions (mogelijk corrupte afbeelding)
                retries = 0;
                // Verwijder corrupt bestand
                try
                {
                    if (File.Exists(permanentPath))
                        File.Delete(permanentPath);
                }
                catch { }
                
                System.Diagnostics.Debug.WriteLine($"Error reading image dimensions: {ex.Message}");
                throw new InvalidOperationException($"Kon afbeelding niet lezen: {ex.Message}", ex);
            }
        }
        
        // Als we hier zijn zonder success, dan is de afbeelding corrupt
        if (!success)
        {
            try
            {
                if (File.Exists(permanentPath))
                    File.Delete(permanentPath);
            }
            catch { }
            throw new InvalidOperationException("Kon afbeelding niet lezen: ongeldig of beschadigd bestand.");
        }

        var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

        var photo = new PhotoItem
        {
            FileName = result.FileName, // Bewaar originele bestandsnaam voor weergave
            FilePath = permanentPath,   // Bewaar permanente pad in database
            Width = width,
            Height = height,
            FileSizeMb = fileSizeMB
        };

        // Initialiseer ImageSource direct - gebruik FromFile direct
        // Dit moet op de main thread gebeuren voor MAUI
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
                    // Verwijder het bestand als ImageSource niet kan worden gemaakt
                    try
                    {
                        if (File.Exists(permanentPath))
                            File.Delete(permanentPath);
                    }
                    catch { }
                    throw new InvalidOperationException($"Kon afbeelding niet laden voor weergave: {imgEx.Message}", imgEx);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MainThread.InvokeOnMainThreadAsync for ImageSource: {ex.Message}");
            // Bestand is al verwijderd of verwijderen mislukt - throw de exception
            throw;
        }

        // Valideer dat ImageSource succesvol is aangemaakt
        if (photo.ImageSource == null)
        {
            try
            {
                if (File.Exists(permanentPath))
                    File.Delete(permanentPath);
            }
            catch { }
            throw new InvalidOperationException("Kon ImageSource niet aanmaken voor de afbeelding.");
        }

        System.Diagnostics.Debug.WriteLine($"Created photo: {photo.FileName}, Path: {photo.FilePath}, ImageSource: {photo.ImageSource != null}, Size: {width}x{height}");

        return photo;
    }

    private async void OnPhotoTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is PhotoItem tappedPhoto)
        {
            // Als we in selectiemodus zijn, toggle de selectie
            if (_isSelectionMode)
            {
                if (_selectedPhotos.Contains(tappedPhoto))
                {
                    _selectedPhotos.Remove(tappedPhoto);
                    tappedPhoto.IsSelected = false;
                }
                else
                {
                    _selectedPhotos.Add(tappedPhoto);
                    tappedPhoto.IsSelected = true;
                }
                
                // Update de knop text met aantal geselecteerde foto's
                SelectButton.Text = $"Cancel ({_selectedPhotos.Count})";
                
                System.Diagnostics.Debug.WriteLine($"Selected photos: {_selectedPhotos.Count}");
            }
            else
            {
                // Normale modus: toon fullscreen overlay
            currentPhoto = tappedPhoto;
            await ShowPhotoOverlay(tappedPhoto);
            }
        }
    }

    private async Task ShowPhotoOverlay(PhotoItem photo)
    {
        if (photo.ImageSource == null)
            photo.InitializeImageSource();

        if (photo.ImageSource == null)
            return;

        // Zorg ervoor dat labels uit de database zijn geladen
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

        // Valideer dat het label geen speciale tekens bevat
        if (!IsValidLabelText(newLabel))
        {
            await Application.Current.MainPage.DisplayAlert(
                "Ongeldige tekens",
                "Labels mogen alleen letters, cijfers en spaties bevatten. Speciale tekens zijn niet toegestaan.",
                "OK");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to add label: '{newLabel}' to photo ID: {currentPhoto.Id}");
            
            // Voeg label toe aan database (retourneert 0 als label al bestaat, > 0 als toegevoegd)
            // Labels zijn case-insensitive (hoofdletterongevoelig)
            var result = await _databaseService.AddLabelAsync(currentPhoto.Id, newLabel);
            
            System.Diagnostics.Debug.WriteLine($"AddLabelAsync returned: {result}");
            
            if (result == 0)
            {
                // Label bestaat al (case-insensitive)
                System.Diagnostics.Debug.WriteLine($"Label '{newLabel}' already exists for photo {currentPhoto.Id}");
                await Application.Current.MainPage.DisplayAlert("Label bestaat al", $"Het label '{newLabel}' bestaat al voor deze foto.", "OK");
                LabelEntry.Text = string.Empty;
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"Label '{newLabel}' successfully added with ID: {result}");
            
            // Herlaad labels uit database om consistentie te garanderen
            await _databaseService.LoadLabelsForPhotoAsync(currentPhoto);
            
            // Update available labels voor filter dropdown
            UpdateAvailableLabels();
            
            LabelEntry.Text = string.Empty;
            DisplayLabels(currentPhoto);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding label: {ex.Message}\n{ex.StackTrace}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to add label: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handelt klik op de Labels dropdown knop af
    /// Laadt alle beschikbare labels en toont ze in een dropdown menu
    /// </summary>
    private async void LabelDropdownButton_Clicked(object? sender, EventArgs e)
    {
        if (currentPhoto == null)
        {
            await Application.Current.MainPage.DisplayAlert("Geen foto geselecteerd", "Selecteer eerst een foto om een label toe te voegen.", "OK");
            return;
        }

        // Toggle dropdown zichtbaarheid
        if (LabelDropdown.IsVisible)
        {
            LabelDropdown.IsVisible = false;
        }
        else
        {
            // Laad alle beschikbare labels uit de database
            await LoadAvailableLabelsAsync();
            
            if (AvailableLabels.Count == 0)
            {
                await Application.Current.MainPage.DisplayAlert("Geen labels", "Er zijn nog geen labels beschikbaar. Voeg eerst een label toe via het tekstvak.", "OK");
                return;
            }
            
            // Wis bestaande items en row definitions
            LabelDropdownList.Children.Clear();
            LabelDropdownList.RowDefinitions.Clear();
            
            // Maak row definitions voor elk label
            for (int i = 0; i < AvailableLabels.Count; i++)
            {
                LabelDropdownList.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            
            // Maak een klikbare Frame voor elk label
            // Plaats elke frame in zijn eigen row om volledige breedte te garanderen
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
                    InputTransparent = true // Laat klikken door naar parent frame
                };
                
                frame.Content = labelText;
                
                // Bewaar het label voor de click handler (closure)
                string capturedLabel = label;
                
                // Voeg tap gesture toe aan de frame
                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += async (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Frame tapped: {capturedLabel}, sender: {s?.GetType().Name}");
                    await AddLabelFromDropdown(capturedLabel);
                };
                frame.GestureRecognizers.Add(tapGesture);
                
                // Voeg frame toe aan Grid in zijn eigen row
                Grid.SetRow(frame, i);
                LabelDropdownList.Children.Add(frame);
            }
            
            System.Diagnostics.Debug.WriteLine($"Created {AvailableLabels.Count} label items in dropdown");
            
            // Positioneer dropdown direct onder de Labels knop
            // De StackLayout is gecentreerd, en de knop is het eerste element (80px breed)
            // De dropdown is 200px breed, dus om hem uit te lijnen met de linkerrand van de knop,
            // moeten we hem naar links verschuiven met een negatieve margin
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
            
            // Update available labels voor filter dropdown
            UpdateAvailableLabels();
            
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
    /// Verwijdert alle foto's uit de database en het bestandssysteem (voor testing)
    /// Let op: deze methode verwijdert alle data permanent!
    /// </summary>
    private async Task ClearAllPhotosAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Clearing all photos from database...");
            
            // Haal eerst alle foto's op om hun bestanden te verwijderen
            var allPhotos = await _databaseService.GetAllPhotosAsync();
            
            // Verwijder foto bestanden van schijf
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
            
            // Wis database
            await _databaseService.ClearAllDataAsync();
            
            // Wis UI collectie (moet op main thread)
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

    /// <summary>
    /// Toggle de zichtbaarheid van de sidebar en past de kolombreedte aan
    /// Wanneer sidebar open is: breedte 220px, wanneer gesloten: breedte 0px
    /// </summary>
    private void ToggleSidebar_Clicked(object? sender, EventArgs e)
    {
        if (Sidebar != null && MainGrid != null && MainGrid.ColumnDefinitions.Count > 0)
        {
            bool isVisible = Sidebar.IsVisible;
            Sidebar.IsVisible = !isVisible;
            
            // Pas kolombreedte aan: 220px wanneer open, 0px wanneer gesloten
            if (isVisible)
            {
                // Sidebar is open, sluit hem
                MainGrid.ColumnDefinitions[0].Width = 0;
            }
            else
            {
                // Sidebar is gesloten, open hem
                MainGrid.ColumnDefinitions[0].Width = 220;
            }
        }
    }

    private void ImportPhoto_Tapped(object? sender, TappedEventArgs e)
    {
        // Call the existing UploadMedia method
        UploadMedia(sender ?? this, EventArgs.Empty);
    }
    /// <summary>
    /// Navigeert naar de PhotoBook Management pagina wanneer op de Photo Book knop wordt geklikt
    /// Probeert meerdere navigatiemethoden omdat Gallery als ContentView wordt gebruikt
    /// en mogelijk geen directe Navigation heeft
    /// </summary>
    private async void OpenPhotoBook_Tapped(object sender, TappedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("OpenPhotoBook_Tapped called");
            
            // Probeer eerst via Shell.Current (meest betrouwbaar voor Shell-based apps)
            if (Shell.Current != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Attempting navigation via Shell.Current.Navigation");
                    await Shell.Current.Navigation.PushAsync(new PhotoBookManagementPage());
                    System.Diagnostics.Debug.WriteLine("Navigation successful via Shell.Current");
                    return;
                }
                catch (Exception shellEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Shell navigation failed: {shellEx.Message}");
                }
            }

            // Zoek de parent ContentPage (MyMainPage) door de parent chain te doorlopen
            var parent = this.Parent;
            ContentPage? parentPage = null;
            
            while (parent != null)
            {
                if (parent is ContentPage cp)
                {
                    parentPage = cp;
                    break;
                }
                parent = parent.Parent;
            }

            // Probeer via parent ContentPage
            if (parentPage != null && parentPage.Navigation != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Attempting navigation via parent ContentPage");
                    await parentPage.Navigation.PushAsync(new PhotoBookManagementPage());
                    System.Diagnostics.Debug.WriteLine("Navigation successful via parent ContentPage");
                    return;
                }
                catch (Exception parentEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Parent navigation failed: {parentEx.Message}");
                }
            }


            // Fallback: probeer via Application.Current.MainPage
            if (Application.Current?.MainPage != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Attempting navigation via Application.Current.MainPage");
                    await Application.Current.MainPage.Navigation.PushAsync(new PhotoBookManagementPage());
                    System.Diagnostics.Debug.WriteLine("Navigation successful via Application.Current.MainPage");
                    return;
                }
                catch (Exception appEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Application navigation failed: {appEx.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine("All navigation methods failed!");
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Could not navigate to Photo Book page. Please check the debug output.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error navigating to PhotoBookManagementPage: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Could not open Photo Book: {ex.Message}", "OK");
            }
        }
    }

    /// <summary>
    /// Toggle selectiemodus voor het selecteren van meerdere foto's
    /// In selectiemodus kunnen gebruikers meerdere foto's selecteren voor batch operaties
    /// </summary>
    private void SelectButton_Clicked(object? sender, EventArgs e)
    {
        _isSelectionMode = !_isSelectionMode;
        
        if (_isSelectionMode)
        {
            // Activeer selectiemodus
            SelectButton.BackgroundColor = Color.FromArgb("#6750A4");
            SelectButton.TextColor = Colors.White;
            SelectButton.Text = $"Cancel ({_selectedPhotos.Count})";
            
            // Toon de acties knop
            SelectionActionsButton.IsVisible = true;
        }
        else
        {
            // Deactiveer selectiemodus
            SelectButton.BackgroundColor = Color.FromArgb("#EDEDED");
            SelectButton.TextColor = Color.FromArgb("#333333");
            SelectButton.Text = "Select";
            
            // Verberg de acties knop
            SelectionActionsButton.IsVisible = false;
            
            // Wis selecties en reset visuele feedback
            foreach (var photo in _selectedPhotos)
            {
                photo.IsSelected = false;
            }
            _selectedPhotos.Clear();
        }
    }
    
    /// <summary>
    /// Toont het actiemenu voor geselecteerde foto's
    /// </summary>
    private async void SelectionActionsButton_Clicked(object? sender, EventArgs e)
    {
        if (_selectedPhotos.Count == 0)
        {
            await Application.Current.MainPage.DisplayAlert("Geen selectie", "Selecteer eerst foto's om acties uit te voeren.", "OK");
            return;
        }
        
        var action = await Application.Current.MainPage.DisplayActionSheet(
            $"{_selectedPhotos.Count} foto's geselecteerd",
            "Annuleren",
            "Verwijderen",
            "Exporteer naar Fotoboek",
            "Labels toevoegen");
        
        switch (action)
        {
            case "Verwijderen":
                await DeleteSelectedPhotosAsync();
                break;
            case "Exporteer naar Fotoboek":
                await Application.Current.MainPage.DisplayAlert("Info", "Fotoboek export komt binnenkort!", "OK");
                break;
            case "Labels toevoegen":
                await Application.Current.MainPage.DisplayAlert("Info", "Bulk label toevoegen komt binnenkort!", "OK");
                break;
        }
    }
    
    /// <summary>
    /// Verwijdert alle geselecteerde foto's uit de database en van schijf
    /// </summary>
    private async Task DeleteSelectedPhotosAsync()
    {
        if (_selectedPhotos.Count == 0)
            return;
            
        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Bevestig verwijderen",
            $"Weet je zeker dat je {_selectedPhotos.Count} foto's wilt verwijderen?",
            "Ja",
            "Nee");
            
        if (!confirm)
            return;
            
        try
        {
            var photosToDelete = _selectedPhotos.ToList();
            
            foreach (var photo in photosToDelete)
            {
                // Reset selectie staat
                photo.IsSelected = false;
                
                // Verwijder bestand van schijf
                if (File.Exists(photo.FilePath))
                {
                    File.Delete(photo.FilePath);
                }
                
                // Verwijder uit database
                await _databaseService.DeletePhotoAsync(photo);
                
                // Verwijder uit UI collectie en _allPhotos
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _allPhotos.Remove(photo);
                    Photos.Remove(photo);
                });
            }
            
            await Application.Current.MainPage.DisplayAlert(
                "Gereed",
                $"{photosToDelete.Count} foto's verwijderd.",
                "OK");
                
            _selectedPhotos.Clear();
            
            // Update available labels en pas filters opnieuw toe
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateAvailableLabels();
                ApplyFiltersAndSort();
            });
            
            // Schakel selectiemodus uit na verwijderen
            _isSelectionMode = false;
            SelectButton.BackgroundColor = Color.FromArgb("#EDEDED");
            SelectButton.TextColor = Color.FromArgb("#333333");
            SelectButton.Text = "Select";
            SelectionActionsButton.IsVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting photos: {ex.Message}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Fout bij verwijderen: {ex.Message}", "OK");
        }
    }
    
    /// <summary>
    /// Update de lijst met beschikbare labels voor filtering
    /// </summary>
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
    
    /// <summary>
    /// Past filters en sorting toe op de foto's
    /// </summary>
    private void ApplyFiltersAndSort()
    {
        var filteredPhotos = _allPhotos.AsEnumerable();
        
        // Filter op label
        if (!string.IsNullOrEmpty(_selectedLabelFilter))
        {
            filteredPhotos = filteredPhotos.Where(p => 
                p.Labels.Any(l => string.Equals(l, _selectedLabelFilter, StringComparison.OrdinalIgnoreCase)));
        }
        
        // Sort op datum
        if (_sortNewestFirst)
        {
            filteredPhotos = filteredPhotos.OrderByDescending(p => p.CreatedDate);
        }
        else
        {
            filteredPhotos = filteredPhotos.OrderBy(p => p.CreatedDate);
        }
        
        // Update Photos collectie
        Photos.Clear();
        foreach (var photo in filteredPhotos)
        {
            Photos.Add(photo);
        }
        
        RefreshPhotosView();
    }
    
    /// <summary>
    /// Handler voor filter label button - toont label dropdown
    /// </summary>
    private async void FilterLabelButton_Clicked(object? sender, EventArgs e)
    {
        var options = new List<string> { "Alle fotos" };
        options.AddRange(AvailableLabels);
        
        var selected = await Application.Current.MainPage.DisplayActionSheet(
            "Filter op label",
            "Annuleren",
            null,
            options.ToArray());
        
        if (selected != null && selected != "Annuleren")
        {
            if (selected == "Alle fotos")
            {
                _selectedLabelFilter = null;
                FilterLabelButton.Text = "Filter: Label";
            }
            else
            {
                _selectedLabelFilter = selected;
                FilterLabelButton.Text = $"Filter: {selected}";
            }
            
            ApplyFiltersAndSort();
        }
    }
    
    /// <summary>
    /// Handler voor sort date button - toggles tussen newest/oldest
    /// </summary>
    private void SortDateButton_Clicked(object? sender, EventArgs e)
    {
        _sortNewestFirst = !_sortNewestFirst;
        
        if (_sortNewestFirst)
        {
            SortDateButton.Text = "Newest ↑";
        }
        else
        {
            SortDateButton.Text = "Oldest ↑";
        }
        
        ApplyFiltersAndSort();
    }
    
    /// <summary>
    /// Handelt SizeChanged event af voor header om responsieve layout aan te passen
    /// </summary>
    private void HeaderGrid_SizeChanged(object? sender, EventArgs e)
    {
        if (sender is Grid headerGrid && GalleryTitle != null)
        {
            double width = headerGrid.Width;
            
            // Op kleinere schermen: verklein font size en pas button spacing aan
            if (width > 0)
            {
                if (width < 600)
                {
                    // Klein scherm: verklein title en buttons
                    GalleryTitle.FontSize = 24;
                    if (FilterLabelButton != null) FilterLabelButton.FontSize = 12;
                    if (SortDateButton != null) SortDateButton.FontSize = 12;
                    if (SelectButton != null) SelectButton.FontSize = 12;
                    if (HeaderButtonsLayout != null) HeaderButtonsLayout.Spacing = 6;
                }
                else if (width < 900)
                {
                    // Medium scherm
                    GalleryTitle.FontSize = 28;
                    if (FilterLabelButton != null) FilterLabelButton.FontSize = 13;
                    if (SortDateButton != null) SortDateButton.FontSize = 13;
                    if (SelectButton != null) SelectButton.FontSize = 13;
                    if (HeaderButtonsLayout != null) HeaderButtonsLayout.Spacing = 8;
                }
                else
                {
                    // Groot scherm: normale grootte
                    GalleryTitle.FontSize = 32;
                    if (FilterLabelButton != null) FilterLabelButton.FontSize = 14;
                    if (SortDateButton != null) SortDateButton.FontSize = 14;
                    if (SelectButton != null) SelectButton.FontSize = 14;
                    if (HeaderButtonsLayout != null) HeaderButtonsLayout.Spacing = 10;
                }
            }
        }
    }

    /// <summary>
    /// Handelt SizeChanged event af voor PhotosCollection om responsieve Span aan te passen
    /// </summary>
    private void PhotosCollection_SizeChanged(object? sender, EventArgs e)
    {
        if (sender is CollectionView collectionView && collectionView.ItemsLayout is GridItemsLayout gridLayout)
        {
            double width = collectionView.Width;
            
            // Bepaal aantal kolommen op basis van schermbreedte
            // Minimaal 150px per thumbnail, met spacing
            int span = 4; // default
            
            if (width > 0)
            {
                // Account voor margins en spacing (16px spacing + padding)
                double availableWidth = width - 40; // 20px margin each side
                double minThumbnailWidth = 150;
                double spacingPerItem = 16;
                
                // Bereken aantal kolommen: (availableWidth + spacing) / (minThumbnailWidth + spacing)
                int calculatedSpan = (int)Math.Floor((availableWidth + spacingPerItem) / (minThumbnailWidth + spacingPerItem));
                
                // Beperk tot redelijke grenzen - maximaal 4 kolommen per rij
                span = Math.Max(2, Math.Min(4, calculatedSpan));
                
                // Update Span alleen als het anders is
                if (gridLayout.Span != span)
                {
                    gridLayout.Span = span;
                    System.Diagnostics.Debug.WriteLine($"Updated GridItemsLayout Span to {span} for width {width}");
                }
            }
        }
    }

    /// <summary>
    /// Forceert correcte item sizing voor Windows platform en initialiseert responsieve Span
    /// </summary>
    private void PhotosCollection_Loaded(object? sender, EventArgs e)
    {
        // Initialiseer responsieve Span
        PhotosCollection_SizeChanged(sender, e);
        
        // Force refresh van de CollectionView op Windows om correcte sizing te krijgen
        try
        {
            if (Microsoft.Maui.Devices.DeviceInfo.Platform == Microsoft.Maui.Devices.DevicePlatform.WinUI)
            {
                // Forceer een layout update
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var currentSource = PhotosCollection.ItemsSource;
                    PhotosCollection.ItemsSource = null;
                    PhotosCollection.ItemsSource = currentSource;
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in PhotosCollection_Loaded: {ex.Message}");
        }
    }

    /// <summary>
    /// Handelt SizeChanged event af voor Grid items om aspect ratio te behouden
    /// </summary>
    private void Grid_SizeChanged(object? sender, EventArgs e)
    {
        if (sender is Grid grid)
        {
            // Aspect ratio: 380:280 = 1.357:1 (width:height)
            const double aspectRatio = 380.0 / 280.0;
            
            if (grid.Width > 0 && grid.Height > 0)
            {
                double expectedHeight = grid.Width / aspectRatio;
                
                // Als de hoogte niet overeenkomt met de verwachte aspect ratio, pas aan
                if (Math.Abs(grid.Height - expectedHeight) > 1)
                {
                    grid.HeightRequest = expectedHeight;
                }
            }
        }
    }
    
    /// <summary>
    /// Valideert of een label tekst alleen toegestane tekens bevat (letters, cijfers, spaties)
    /// Speciale tekens zijn niet toegestaan
    /// </summary>
    private bool IsValidLabelText(string labelText)
    {
        if (string.IsNullOrWhiteSpace(labelText))
            return false;

        // Controleer of alle karakters letters, cijfers of spaties zijn
        foreach (char c in labelText)
        {
            if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

}
