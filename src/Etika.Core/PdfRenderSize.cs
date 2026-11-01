namespace Etika.Core;

public sealed record PdfRenderSize(uint Width, uint Height)
{
    public static PdfRenderSize Calculate(double pageWidth, double pageHeight, double zoom)
    {
        if (!double.IsFinite(pageWidth) || !double.IsFinite(pageHeight) || pageWidth <= 0 || pageHeight <= 0
            || !double.IsFinite(zoom) || zoom is < .5 or > 2.5)
            throw new ArgumentException("Ukuran halaman PDF tidak valid.");
        // Bound both dimensions, including unusually tall/wide PDF pages.
        double ratio = pageWidth / pageHeight;
        double width = Math.Clamp(1200 * zoom, 600, 3000);
        double height = width / ratio;
        if (height > 4096) { height = 4096; width = height * ratio; }
        if (width > 4096) { width = 4096; height = width / ratio; }
        return new((uint)Math.Clamp(width, 1, 4096), (uint)Math.Clamp(height, 1, 4096));
    }
}
