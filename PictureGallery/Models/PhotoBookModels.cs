using System.Collections.ObjectModel;

public class PhotoBookPageModel
{
    public ObservableCollection<PhotoItem> Photos { get; } = new();
    public string? Title { get; set; }
}

public class PhotoBook
{
    public ObservableCollection<PhotoBookPageModel> Pages { get; } = new();
}
