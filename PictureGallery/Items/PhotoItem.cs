using System.Collections.ObjectModel;

public class PhotoItem
{
    public string FileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double FileSizeMb { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public ImageSource? ImageSource { get; set; }

    // Nieuw: Labels toevoegen
    public ObservableCollection<string> Labels { get; } = new ObservableCollection<string>();

    public string DimensionsText => $"Image Dimensions: {Width} x {Height}";
    public string FileSizeText => $"File Size: {FileSizeMb:F1} MB";
}
