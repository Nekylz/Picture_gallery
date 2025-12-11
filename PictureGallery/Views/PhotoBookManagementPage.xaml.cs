using System.Collections.ObjectModel;
using PictureGallery.Models;
using PictureGallery.Services;
using Microsoft.Maui.Controls;

namespace PictureGallery.Views;

public partial class PhotoBookManagementPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    public ObservableCollection<PhotoBook> PhotoBooks { get; } = new();
    private bool _isSelectionMode = false;
    private readonly HashSet<PhotoBook> _selectedPhotoBooks = new();

    public PhotoBookManagementPage()
    {
        InitializeComponent();
        BindingContext = this;
        _databaseService = new DatabaseService();
        
        // Setup modal events
        CreatePhotoBookModal.OnCreate += OnPhotoBookCreated;
        CreatePhotoBookModal.OnCancel += OnPhotoBookCanceled;
        
        LoadPhotoBooksAsync();
        UpdateStatisticsAsync();
    }

    /// <summary>
    /// Laadt alle PhotoBooks uit de database en toont ze in de CollectionView
    /// </summary>
    private async Task LoadPhotoBooksAsync()
    {
        try
        {
            var photoBooks = await _databaseService.GetAllPhotoBooksAsync();
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PhotoBooks.Clear();
                foreach (var photoBook in photoBooks)
                {
                    PhotoBooks.Add(photoBook);
                }
                RefreshEmptyState();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading PhotoBooks: {ex.Message}");
            await DisplayAlert("Fout", "Kon fotoboeken niet laden.", "OK");
        }
    }

    /// <summary>
    /// Werkt de statistieken bij (Total Albums, Total Photos)
    /// </summary>
    private async Task UpdateStatisticsAsync()
    {
        try
        {
            var photoBookCount = await _databaseService.GetPhotoBookCountAsync();
            var photoCount = await _databaseService.GetPhotoCountAsync();
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TotalPhotoBooksLabel.Text = photoBookCount.ToString();
                TotalPhotosLabel.Text = photoCount.ToString();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Handelt klik op "+ New Photobook" knop af
    /// Toont een modal/formulier om een nieuwe PhotoBook aan te maken
    /// </summary>
    private void NewPhotoBookButton_Clicked(object? sender, EventArgs e)
    {
        ShowCreatePhotoBookModal();
    }

    /// <summary>
    /// Toont een modal/formulier voor het maken van een nieuwe PhotoBook
    /// </summary>
    private void ShowCreatePhotoBookModal()
    {
        CreatePhotoBookModal.Reset();
        CreatePhotoBookModal.IsVisible = true;
    }

    /// <summary>
    /// Handelt het aanmaken van een PhotoBook af vanuit de modal
    /// </summary>
    private async void OnPhotoBookCreated(object? sender, string result)
    {
        var parts = result.Split('|');
        if (parts.Length < 2)
            return;

        var name = parts[0].Trim();
        var description = parts[1].Trim();

        if (string.IsNullOrWhiteSpace(name))
            return;

        CreatePhotoBookModal.IsVisible = false;

        // Maak nieuwe PhotoBook aan
        try
        {
            var newPhotoBook = new PhotoBook
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description
            };

            await _databaseService.AddPhotoBookAsync(newPhotoBook);
            
            // Refresh de lijst
            await LoadPhotoBooksAsync();
            await UpdateStatisticsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating PhotoBook: {ex.Message}");
            await DisplayAlert("Fout", "Kon fotoboek niet aanmaken.", "OK");
        }
    }

    /// <summary>
    /// Handelt het annuleren van de modal af
    /// </summary>
    private void OnPhotoBookCanceled(object? sender, EventArgs e)
    {
        CreatePhotoBookModal.IsVisible = false;
        CreatePhotoBookModal.Reset();
    }

    /// <summary>
    /// Handelt klik op Select button af - toggles selection mode
    /// </summary>
    private void SelectButton_Clicked(object? sender, EventArgs e)
    {
        _isSelectionMode = !_isSelectionMode;
        
        if (!_isSelectionMode)
        {
            // Exit selection mode - clear all selections
            foreach (var photoBook in _selectedPhotoBooks.ToList())
            {
                photoBook.IsSelected = false;
            }
            _selectedPhotoBooks.Clear();
        }
        
        UpdateSelectButtonText();
    }

    /// <summary>
    /// Werkt de select button tekst bij met aantal geselecteerde items
    /// </summary>
    private void UpdateSelectButtonText()
    {
        if (_isSelectionMode && _selectedPhotoBooks.Count > 0)
        {
            SelectButton.Text = $"Cancel ({_selectedPhotoBooks.Count})";
            DeleteButton.IsVisible = true;
        }
        else if (_isSelectionMode)
        {
            SelectButton.Text = "Cancel";
            DeleteButton.IsVisible = false;
        }
        else
        {
            SelectButton.Text = "Select";
            DeleteButton.IsVisible = false;
        }
    }

    /// <summary>
    /// Handelt klik op Delete button af
    /// </summary>
    private async void DeleteButton_Clicked(object? sender, EventArgs e)
    {
        await DeleteSelectedPhotoBooksAsync();
    }

    /// <summary>
    /// Handelt klik op een PhotoBook af - navigeert naar PhotoBookPage of toggles selectie
    /// </summary>
    private async void OnPhotoBookTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is PhotoBook photoBook)
        {
            if (_isSelectionMode)
            {
                // Toggle selection
                photoBook.IsSelected = !photoBook.IsSelected;
                
                if (photoBook.IsSelected)
                {
                    _selectedPhotoBooks.Add(photoBook);
                }
                else
                {
                    _selectedPhotoBooks.Remove(photoBook);
                }
                
                UpdateSelectButtonText();
            }
            else
            {
                // Normal mode - navigate to PhotoBookPage
                try
                {
                    var photoBookPage = new PhotoBookPage(photoBook.Id);
                    await Navigation.PushAsync(photoBookPage);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error navigating to PhotoBookPage: {ex.Message}");
                    await DisplayAlert("Fout", "Kon fotoboek niet openen.", "OK");
                }
            }
        }
    }

    /// <summary>
    /// Verwijdert alle geselecteerde PhotoBooks
    /// </summary>
    private async Task DeleteSelectedPhotoBooksAsync()
    {
        if (_selectedPhotoBooks.Count == 0)
            return;

        var confirmed = await DisplayAlert(
            "Fotoboeken verwijderen",
            $"Weet je zeker dat je {_selectedPhotoBooks.Count} fotoboek(en) wilt verwijderen?",
            "Verwijderen",
            "Annuleren");

        if (!confirmed)
            return;

        try
        {
            foreach (var photoBook in _selectedPhotoBooks.ToList())
            {
                await _databaseService.DeletePhotoBookAsync(photoBook);
                PhotoBooks.Remove(photoBook);
            }

            _selectedPhotoBooks.Clear();
            _isSelectionMode = false;
            
            UpdateSelectButtonText();
            await UpdateStatisticsAsync();
            RefreshEmptyState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting PhotoBooks: {ex.Message}");
            await DisplayAlert("Fout", "Kon fotoboeken niet verwijderen.", "OK");
        }
    }

    /// <summary>
    /// Werkt de empty state visibility bij op basis van PhotoBooks count
    /// </summary>
    private void RefreshEmptyState()
    {
        bool hasPhotoBooks = PhotoBooks.Count > 0;
        PhotoBooksCollection.IsVisible = hasPhotoBooks;
        EmptyStateView.IsVisible = !hasPhotoBooks;
    }

    /// <summary>
    /// Handelt SizeChanged event af voor PhotoBooksCollection om responsieve Span aan te passen
    /// Maximaal 3 kolommen per rij
    /// </summary>
    private void PhotoBooksCollection_SizeChanged(object? sender, EventArgs e)
    {
        if (sender is CollectionView collectionView && collectionView.ItemsLayout is GridItemsLayout gridLayout)
        {
            double width = collectionView.Width;
            
            // Bepaal aantal kolommen op basis van schermbreedte
            // Maximaal 3 kolommen per rij
            int span = 3; // default
            
            if (width > 0)
            {
                // Account voor margins en spacing (16px spacing + padding)
                double availableWidth = width - 40; // 20px margin each side
                double minCardWidth = 250; // Minimale breedte per card
                double spacingPerItem = 16;
                
                // Bereken aantal kolommen: (availableWidth + spacing) / (minCardWidth + spacing)
                int calculatedSpan = (int)Math.Floor((availableWidth + spacingPerItem) / (minCardWidth + spacingPerItem));
                
                // Beperk tot 1-3 kolommen (maximaal 3 zoals gevraagd)
                span = Math.Max(1, Math.Min(3, calculatedSpan));
                
                // Update Span alleen als het anders is
                if (gridLayout.Span != span)
                {
                    gridLayout.Span = span;
                    System.Diagnostics.Debug.WriteLine($"Updated PhotoBooksCollection Span to {span} for width {width}");
                }
            }
        }
    }

    /// <summary>
    /// Called when the page appears - refresh data
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadPhotoBooksAsync();
        UpdateStatisticsAsync();
    }
}

