using CommunityToolkit.Mvvm.ComponentModel;

namespace PictureGallery.ViewModels;

/// <summary>
/// Base ViewModel class that implements INotifyPropertyChanged
/// Provides common functionality for all ViewModels
/// </summary>
public partial class BaseViewModel : ObservableObject
{
    /// <summary>
    /// Indicates whether the ViewModel is currently busy (e.g., loading data)
    /// </summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>
    /// Title of the page/view
    /// </summary>
    [ObservableProperty]
    private string title = string.Empty;

    /// <summary>
    /// Indicates whether the ViewModel is not busy
    /// Useful for enabling/disabling UI elements
    /// </summary>
    public bool IsNotBusy => !IsBusy;
}

