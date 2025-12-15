using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using PictureGallery.Models;
using PictureGallery.Services;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PictureGallery.ViewModels;

public partial class PhotoBookPageViewModel : BaseViewModel
{
    private static readonly string[] AllowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
    private static readonly int MaxPhotosPerPage = 4;
    private static readonly int MaxPages = 2;

    private readonly DatabaseService _databaseService;
    private readonly int? _photoBookId;

    [ObservableProperty]
    private PhotoBook photoBook = new();

    public PhotoBookPageViewModel(int? photoBookId = null)
    {
        _databaseService = new DatabaseService();
        _photoBookId = photoBookId;
        Title = "Photobook";

        // Initialize commands
        AddPhotoCommand = new AsyncRelayCommand(AddPhotoAsync);

        // Load photo book
        _ = LoadPhotoBookAsync();
    }

    #region Commands

    public ICommand AddPhotoCommand { get; }

    #endregion

    #region Public Methods

    public async Task LoadPhotoBookAsync()
    {
        try
        {
            IsBusy = true;

            if (_photoBookId.HasValue)
            {
                var loadedPhotoBook = await _databaseService.GetPhotoBookByIdAsync(_photoBookId.Value);
                if (loadedPhotoBook != null)
                {
                    PhotoBook = loadedPhotoBook;

                    // Initialize pages if empty
                    if (PhotoBook.Pages.Count == 0)
                    {
                        for (int i = 0; i < MaxPages; i++)
                            PhotoBook.Pages.Add(new PhotoBookPageModel());
                    }
                }
            }
            else
            {
                // No ID - create new PhotoBook
                // Initialize pages
                for (int i = 0; i < MaxPages; i++)
                    PhotoBook.Pages.Add(new PhotoBookPageModel());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading PhotoBook: {ex.Message}");
            await ShowAlertAsync("Error", "Could not load photo book.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Private Methods

    private async Task AddPhotoAsync()
    {
        try
        {
            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Select images"
            });

            if (results == null || !results.Any())
                return;

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
                {
                    await ShowAlertAsync("Invalid Image", $"The image '{result.FileName}' could not be loaded.");
                    continue;
                }

                // Find first page with less than 4 photos
                var page = PhotoBook.Pages.FirstOrDefault(p => p.Photos.Count < MaxPhotosPerPage);
                if (page == null)
                {
                    await ShowAlertAsync("Full", "All pages are full (max 4 photos per page, 2 pages).");
                    break;
                }

                page.Photos.Add(photo);
            }

            IsBusy = false;
        }
        catch (Exception ex)
        {
            IsBusy = false;
            await ShowAlertAsync("Error", ex.Message);
        }
    }

    private bool IsSupportedImage(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<PhotoItem> CreatePhotoItemAsync(FileResult result)
    {
        var filePath = result.FullPath;

        // If file is not directly available, copy it to cache directory
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            try
            {
                await using var pickedStream = await result.OpenReadAsync();
                filePath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid()}_{result.FileName}");
                await using var tempFile = File.Create(filePath);
                await pickedStream.CopyToAsync(tempFile);
                await tempFile.FlushAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying file: {ex.Message}");
                throw new InvalidOperationException($"Could not copy file: {ex.Message}", ex);
            }
        }

        // Verify file exists and is not empty
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("File could not be created.");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            try
            {
                File.Delete(filePath);
            }
            catch { }
            throw new InvalidOperationException("File is empty.");
        }

        // Read image dimensions using SkiaSharp
        int width = 0, height = 0;
        try
        {
            using (var stream = File.OpenRead(filePath))
            {
                var bitmap = SKBitmap.Decode(stream);

                if (bitmap == null)
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch { }
                    throw new InvalidOperationException("The file could not be read as an image. The file may be corrupted or not a valid image.");
                }

                try
                {
                    if (bitmap.Width <= 0 || bitmap.Height <= 0)
                    {
                        bitmap.Dispose();
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch { }
                        throw new InvalidOperationException("The image has no valid dimensions.");
                    }

                    width = bitmap.Width;
                    height = bitmap.Height;
                }
                finally
                {
                    bitmap?.Dispose();
                }
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                File.Delete(filePath);
            }
            catch { }
            System.Diagnostics.Debug.WriteLine($"Error reading image dimensions: {ex.Message}");
            throw new InvalidOperationException($"Could not read image: {ex.Message}", ex);
        }

        // Create ImageSource with error handling
        ImageSource? imageSource = null;
        try
        {
            imageSource = ImageSource.FromFile(filePath);
        }
        catch (Exception imgEx)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch { }
            throw new InvalidOperationException($"Could not load image for display: {imgEx.Message}", imgEx);
        }

        if (imageSource == null)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch { }
            throw new InvalidOperationException("Could not create ImageSource for the image.");
        }

        return new PhotoItem
        {
            FileName = result.FileName,
            FilePath = filePath,
            Width = width,
            Height = height,
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

    #endregion
}

