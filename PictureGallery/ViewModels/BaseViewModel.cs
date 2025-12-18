using CommunityToolkit.Mvvm.ComponentModel;

namespace PictureGallery.ViewModels;

/// <summary>
/// Basis ViewModel klasse die INotifyPropertyChanged implementeert
/// Biedt gemeenschappelijke functionaliteit voor alle ViewModels
/// </summary>
public partial class BaseViewModel : ObservableObject
{
    /// <summary>
    /// Geeft aan of de ViewModel momenteel bezig is (bijv. data aan het laden)
    /// </summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>
    /// Titel van de pagina/view
    /// </summary>
    [ObservableProperty]
    private string title = string.Empty;

    /// <summary>
    /// Geeft aan of de ViewModel niet bezig is
    /// Handig voor het in-/uitschakelen van UI elementen
    /// </summary>
    public bool IsNotBusy => !IsBusy;
}

