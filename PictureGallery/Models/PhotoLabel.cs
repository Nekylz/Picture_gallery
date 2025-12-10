using SQLite;

namespace PictureGallery.Models;

[Table("Labels")]
public class PhotoLabel
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Indexed]
    public int PhotoId { get; set; }

    [NotNull, MaxLength(100)]
    public string LabelText { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // Helper methods

    /// <summary>
    /// Check if the label has valid data
    /// </summary>
    [Ignore]
    public bool IsValid => PhotoId > 0 && 
                          !string.IsNullOrWhiteSpace(LabelText) && 
                          LabelText.Length <= 100;

    /// <summary>
    /// Get a trimmed version of the label text
    /// </summary>
    [Ignore]
    public string TrimmedLabelText => LabelText?.Trim() ?? string.Empty;
}

