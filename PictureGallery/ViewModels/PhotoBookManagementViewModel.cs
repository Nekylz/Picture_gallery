using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using PictureGallery.Models;
using PictureGallery.Services;
using PictureGallery.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PictureGallery.ViewModels;

public partial class PhotoBookManagementViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private HashSet<PhotoBook> _selectedPhotoBooks = new();

    [ObservableProperty]
    private ObservableCollection<PhotoBook> photoBooks = new();

    [ObservableProperty]
    private bool isSelectionMode;

    [ObservableProperty]
    private int totalPhotoBooks;

    [ObservableProperty]
    private int totalPhotos;

    [ObservableProperty]
    private string selectButtonText = "Select";

    [ObservableProperty]
    private bool deleteButtonVisible;

    [ObservableProperty]
    private bool hasPhotoBooks;

    [ObservableProperty]
    private bool isCreatePhotoBookModalVisible;

    public PhotoBookManagementViewModel()
    {
        _databaseService = new DatabaseService();
        Title = "Photobook Management";

        // Initialize commands
        NewPhotoBookCommand = new RelayCommand(ShowCreatePhotoBookModal);
        SelectButtonCommand = new RelayCommand(ToggleSelectionMode);
        DeleteButtonCommand = new AsyncRelayCommand(DeleteSelectedPhotoBooksAsync);
        PhotoBookTappedCommand = new AsyncRelayCommand<PhotoBook>(OnPhotoBookTappedAsync);

        // Load data on initialization
        _ = LoadPhotoBooksAsync();
        _ = UpdateStatisticsAsync();
    }

    #region Commands

    public ICommand NewPhotoBookCommand { get; }
    public ICommand SelectButtonCommand { get; }
    public ICommand DeleteButtonCommand { get; }
    public ICommand PhotoBookTappedCommand { get; }

    #endregion

    #region Public Methods

    public async Task LoadPhotoBooksAsync()
    {
        try
        {
            IsBusy = true;
            var loadedPhotoBooks = await _databaseService.GetAllPhotoBooksAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PhotoBooks.Clear();
                foreach (var photoBook in loadedPhotoBooks)
                {
                    PhotoBooks.Add(photoBook);
                }
                HasPhotoBooks = PhotoBooks.Count > 0;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading PhotoBooks: {ex.Message}");
            await ShowAlertAsync("Error", "Could not load photo books.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UpdateStatisticsAsync()
    {
        try
        {
            var photoBookCount = await _databaseService.GetPhotoBookCountAsync();
            var photoCount = await _databaseService.GetPhotoCountAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                TotalPhotoBooks = photoBookCount;
                TotalPhotos = photoCount;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating statistics: {ex.Message}");
        }
    }

    public void OnPhotoBookCreated(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        IsCreatePhotoBookModalVisible = false;

        _ = CreatePhotoBookAsync(name, description);
    }

    public void OnPhotoBookCanceled()
    {
        IsCreatePhotoBookModalVisible = false;
    }

    #endregion

    #region Private Methods

    private void ShowCreatePhotoBookModal()
    {
        IsCreatePhotoBookModalVisible = true;
    }

    private async Task CreatePhotoBookAsync(string name, string description)
    {
        try
        {
            var newPhotoBook = new PhotoBook
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description
            };

            await _databaseService.AddPhotoBookAsync(newPhotoBook);

            // Refresh the list
            await LoadPhotoBooksAsync();
            await UpdateStatisticsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating PhotoBook: {ex.Message}");
            await ShowAlertAsync("Error", "Could not create photo book.");
        }
    }

    private void ToggleSelectionMode()
    {
        IsSelectionMode = !IsSelectionMode;

        if (!IsSelectionMode)
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

    private void UpdateSelectButtonText()
    {
        if (IsSelectionMode && _selectedPhotoBooks.Count > 0)
        {
            SelectButtonText = $"Cancel ({_selectedPhotoBooks.Count})";
            DeleteButtonVisible = true;
        }
        else if (IsSelectionMode)
        {
            SelectButtonText = "Cancel";
            DeleteButtonVisible = false;
        }
        else
        {
            SelectButtonText = "Select";
            DeleteButtonVisible = false;
        }
    }

    private async Task OnPhotoBookTappedAsync(PhotoBook? photoBook)
    {
        if (photoBook == null)
            return;

        if (IsSelectionMode)
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
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.Navigation.PushAsync(photoBookPage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating to PhotoBookPage: {ex.Message}");
                await ShowAlertAsync("Error", "Could not open photo book.");
            }
        }
    }

    private async Task DeleteSelectedPhotoBooksAsync()
    {
        if (_selectedPhotoBooks.Count == 0)
            return;

        var confirmed = await Application.Current?.MainPage?.DisplayAlert(
            "Delete Photo Books",
            $"Are you sure you want to delete {_selectedPhotoBooks.Count} photo book(s)?",
            "Delete",
            "Cancel");

        if (confirmed != true)
            return;

        try
        {
            foreach (var photoBook in _selectedPhotoBooks.ToList())
            {
                await _databaseService.DeletePhotoBookAsync(photoBook);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    PhotoBooks.Remove(photoBook);
                });
            }

            _selectedPhotoBooks.Clear();
            IsSelectionMode = false;

            UpdateSelectButtonText();
            await UpdateStatisticsAsync();
            HasPhotoBooks = PhotoBooks.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting PhotoBooks: {ex.Message}");
            await ShowAlertAsync("Error", "Could not delete photo books.");
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
}

