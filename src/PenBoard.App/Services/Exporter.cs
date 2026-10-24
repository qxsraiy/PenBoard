using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PenBoard.Services;

public sealed class ExportResult
{
    public string? PngPath { get; init; }
    public string? PdfPath { get; init; }
    public string Directory { get; init; } = "";
}

public sealed class Exporter
{
    public ExportResult Export(
        BitmapSource background,
        StrokeCollection strokes,
        double dipWidth,
        double dipHeight,
        Settings settings)
    {
        if (background.PixelWidth <= 0 || background.PixelHeight <= 0)
        {
            throw new InvalidOperationException("Background image is empty.");
        }

        var pixelWidth = background.PixelWidth;
        var pixelHeight = background.PixelHeight;
        var scaleX = dipWidth > 0 ? pixelWidth / dipWidth : 1.0;
        var scaleY = dipHeight > 0 ? pixelHeight / dipHeight : 1.0;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(background, new Rect(0, 0, pixelWidth, pixelHeight));
            dc.PushTransform(new MatrixTransform(scaleX, 0, 0, scaleY, 0, 0));
            foreach (var stroke in strokes)
            {
                stroke.Draw(dc);
            }
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        var directory = Path.Combine(
            settings.SaveDirectory,
            DateTime.Now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(directory);

        var stem = "PenBoard_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string? pngPath = null;
        string? pdfPath = null;

        if (settings.AutoSavePng)
        {
            pngPath = Path.Combine(directory, stem + ".png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fs = new FileStream(pngPath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(fs);
        }

        if (settings.AutoSavePdf)
        {
            pdfPath = Path.Combine(directory, stem + ".pdf");
            var jpegEncoder = new JpegBitmapEncoder { QualityLevel = 90 };
            jpegEncoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var ms = new MemoryStream();
            jpegEncoder.Save(ms);
            PdfWriter.WriteImagePdf(ms.ToArray(), pixelWidth, pixelHeight, settings.PdfDpi, pdfPath);
        }

        return new ExportResult
        {
            PngPath = pngPath,
            PdfPath = pdfPath,
            Directory = directory
        };
    }
}
