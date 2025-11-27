using System.Collections.ObjectModel;

namespace PictureGallery.Models
{
    public class PhotoBookPageItem
    {
        public PhotoItem Photo { get; set; }
        public string? Title { get; set; } // Tekst/titel per pagina
    }

    public class PhotoBook
    {
        public string Name { get; set; } = "NewPhotoBook";
        public ObservableCollection<PhotoBookPageItem> Pages { get; set; } = new();
        public string Template { get; set; } = "Default"; // Template selectie
    }
}
