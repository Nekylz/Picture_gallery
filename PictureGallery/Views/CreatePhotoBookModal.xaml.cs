using System;

namespace PictureGallery.Views;

public partial class CreatePhotoBookModal : ContentView
{
    public event EventHandler<string>? OnCreate;
    public event EventHandler? OnCancel;

    public string TitleText => TitleEntry?.Text ?? "";
    public string DescriptionText => DescriptionEditor?.Text ?? "";

    public CreatePhotoBookModal()
    {
        InitializeComponent();
    }

    private void CloseButton_Clicked(object? sender, EventArgs e)
    {
        OnCancel?.Invoke(this, EventArgs.Empty);
    }

    private void CancelButton_Clicked(object? sender, EventArgs e)
    {
        OnCancel?.Invoke(this, EventArgs.Empty);
    }

    private void CreateButton_Clicked(object? sender, EventArgs e)
    {
        var title = TitleEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title))
        {
            // Show error - title is required
            TitleEntry.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#FF3B30");
            return;
        }

        var description = DescriptionEditor.Text?.Trim() ?? "";
        var result = $"{title}|{description}";
        OnCreate?.Invoke(this, result);
    }

    public void Reset()
    {
        TitleEntry.Text = "";
        DescriptionEditor.Text = "";
        TitleEntry.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#3A3A3C");
    }
}


