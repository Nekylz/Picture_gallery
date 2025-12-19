using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

public static class ImageToPdfConverter
{
    /// <summary>
    /// Converts an array of image file paths to a single PDF file.
    /// </summary>
    /// <param name="images"></param>
    /// <param name="outputPdf"></param>
    public static void ImagesToPdf(string[] images, string outputPdf)
    {
        // Create a new empty PDF document
        PdfDocument doc = new PdfDocument();

        foreach (var imgPath in images)
        {
            if (!File.Exists(imgPath))
                continue; // Skip missing files instead of crashing
            
            // Load the image from the file path
            using (XImage img = XImage.FromFile(imgPath))
            {
                // Add a new blank page to the PDF
                PdfPage page = doc.AddPage();

                // Set the page size so it matches the image real world size in pixels to PDF points 72 points per inch
                page.Width = img.PixelWidth * 72 / img.HorizontalResolution;
                page.Height = img.PixelHeight * 72 / img.VerticalResolution;

                // Create a drawing surface for the page
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    // Draw the image so it fills the whole page
                    gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                }
            }
        }

        // Save the completed PDF to the chosen path
        doc.Save(outputPdf);
        // Close the document 
        doc.Close();
    }
}