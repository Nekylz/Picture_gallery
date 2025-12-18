using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using PictureGallery.Models;
using PictureGallery.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PictureGallery.ViewModels;

public partial class SelectPhotosFromGalleryViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly int? _photoBookId;
    private readonly HashSet<PhotoItem> _selectedPhotos = new();

    [ObservableProperty]
    private ObservableCollection<PhotoItem> photos = new();

    [ObservableProperty]
    private bool hasPhotos;

    [ObservableProperty]
    private bool hasNoPhotos;

    [ObservableProperty]
    private int selectedCount;

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private bool hasSelection;

    public SelectPhotosFromGalleryViewModel(int? photoBookId)
    {
        _databaseService = new DatabaseService();
        _photoBookId = photoBookId;
        Title = "Select Photos";
        
        CancelCommand = new RelayCommand(async () => await CancelAsync());
        AddSelectedCommand = new AsyncRelayCommand(AddSelectedAsync);
        PhotoTappedCommand = new RelayCommand<PhotoItem>(OnPhotoTapped);
    }

    public ICommand CancelCommand { get; }
    public ICommand AddSelectedCommand { get; }
    public ICommand PhotoTappedCommand { get; }

    public event Action<List<PhotoItem>>? PhotosSelected;

    public async Task LoadPhotosAsync()
    {
        try
        {
            IsBusy = true;
            
            // Get all photos from main gallery (not in any PhotoBook)
            var galleryPhotos = await _databaseService.GetAllPhotosAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Photos.Clear();
                foreach (var photo in galleryPhotos)
                {
                    Photos.Add(photo);
                }
                HasPhotos = Photos.Count > 0;
                HasNoPhotos = Photos.Count == 0;
                TotalCount = Photos.Count;
                UpdateSelectionState();
            });
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"Could not load photos: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnPhotoTapped(PhotoItem? photo)
    {
        if (photo == null) return;

        photo.IsSelected = !photo.IsSelected;

        if (photo.IsSelected)
        {
            _selectedPhotos.Add(photo);
        }
        else
        {
            _selectedPhotos.Remove(photo);
        }

        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectedCount = _selectedPhotos.Count;
        HasSelection = SelectedCount > 0;
    }

    private async Task CancelAsync()
    {
        if (Application.Current?.MainPage != null)
        {
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }
    }

    private async Task AddSelectedAsync()
    {
        if (_selectedPhotos.Count == 0)
            return;

        try
        {
            IsBusy = true;
            
            var selectedList = _selectedPhotos.ToList();
            
            // Notify that photos were selected
            PhotosSelected?.Invoke(selectedList);

            // Close the page
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.Navigation.PopModalAsync();
            }
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"Could not add photos: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowAlertAsync(string title, string message)
    {
        if (Application.Current?.MainPage != null)
        {
            await Application.Current.MainPage.DisplayAlert(title, message, "OK");
        }
    }
}

