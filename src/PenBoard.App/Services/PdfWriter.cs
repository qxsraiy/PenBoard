using System.Globalization;
using System.IO;
using System.Text;

namespace PenBoard.Services;

public static class PdfWriter
{
    /// <summary>
    /// Writes a one-page PDF that contains one JPEG image. No fonts are embedded,
    /// so CJK text in the image is safe.
    /// </summary>
    public static void WriteImagePdf(
        byte[] jpeg,
        int pixelWidth,
        int pixelHeight,
        double dpi,
        string path)
    {
        if (jpeg.Length == 0)
        {
            throw new ArgumentException("JPEG is empty.", nameof(jpeg));
        }

        dpi = Math.Clamp(dpi, 72, 600);
        var pageWidth = pixelWidth * 72.0 / dpi;
        var pageHeight = pixelHeight * 72.0 / dpi;
        var content = $"q\n{Fmt(pageWidth)} 0 0 {Fmt(pageHeight)} 0 0 cm\n/Im0 Do\nQ\n";
        var contentBytes = Encoding.ASCII.GetBytes(content);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var offsets = new long[6];

        WriteAscii(fs, "%PDF-1.4\n");

        offsets[1] = fs.Position;
        WriteAscii(fs, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets[2] = fs.Position;
        WriteAscii(fs, "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        offsets[3] = fs.Position;
        WriteAscii(fs,
            $"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Fmt(pageWidth)} {Fmt(pageHeight)}] " +
            "/Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");

        offsets[4] = fs.Position;
        WriteAscii(fs,
            $"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {pixelWidth} /Height {pixelHeight} " +
            $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
        fs.Write(jpeg, 0, jpeg.Length);
        WriteAscii(fs, "\nendstream\nendobj\n");

        offsets[5] = fs.Position;
        WriteAscii(fs, $"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        fs.Write(contentBytes, 0, contentBytes.Length);
        WriteAscii(fs, "\nendstream\nendobj\n");

        var xref = fs.Position;
        WriteAscii(fs, "xref\n0 6\n");
        WriteAscii(fs, "0000000000 65535 f \n");
        for (var i = 1; i <= 5; i++)
        {
            WriteAscii(fs, $"{offsets[i]:D10} 00000 n \n");
        }

        WriteAscii(fs,
            $"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    private static string Fmt(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
