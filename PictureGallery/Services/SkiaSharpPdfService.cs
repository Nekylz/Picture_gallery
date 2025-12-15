using SkiaSharp;

public static class SkiaSharpPdfService
{
    // Maakt een PDF met SkiaSharp (snelle versie)
    // Geeft per foto progress terug
    public static void ImagesToPdf(
        string[] images,
        string outputPdf,
        Action<int, int>? onProgress = null)
    {
        int total = images.Length;
        int processed = 0;

        // Maak een nieuw PDF document
        using var document = SKDocument.CreatePdf(outputPdf);

        foreach (var imgPath in images)
        {
            if (!File.Exists(imgPath))
            {
                processed++;
                onProgress?.Invoke(processed, total);
                continue; // Sla ontbrekende bestanden over
            }

            // Lees afbeelding in
            using var bitmap = SKBitmap.Decode(imgPath);
            if (bitmap == null)
            {
                processed++;
                onProgress?.Invoke(processed, total);
                continue;
            }

            // Begin een nieuwe PDF pagina met grootte van de afbeelding
            using var canvas = document.BeginPage(bitmap.Width, bitmap.Height);

            // Teken de afbeelding op de PDF pagina
            canvas.DrawBitmap(
                bitmap,
                new SKRect(0, 0, bitmap.Width, bitmap.Height));

            // Sluit de pagina
            document.EndPage();

            // Update progress
            processed++;
            onProgress?.Invoke(processed, total);
        }

        // Sluit en sla het PDF bestand op
        document.Close();
    }
}