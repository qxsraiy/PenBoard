using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PenBoard.Interop;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace PenBoard.Services;

public sealed class ScreenCapture
{
    public BitmapSource CapturePrimaryScreen()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen
                     ?? throw new InvalidOperationException("No primary screen.");
        var bounds = screen.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("Primary screen has no area.");
        }

        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new Win32Exception("GetDC failed.");
        }

        try
        {
            using var bitmap = new Bitmap(bounds.Width, bounds.Height, DrawingPixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var targetDc = graphics.GetHdc();
                try
                {
                    var ok = NativeMethods.BitBlt(
                        targetDc,
                        0,
                        0,
                        bounds.Width,
                        bounds.Height,
                        screenDc,
                        bounds.Left,
                        bounds.Top,
                        NativeMethods.SRCCOPY);
                    if (!ok)
                    {
                        throw new Win32Exception("BitBlt failed.");
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(targetDc);
                }
            }

            var data = bitmap.LockBits(
                new Rectangle(0, 0, bounds.Width, bounds.Height),
                ImageLockMode.ReadOnly,
                DrawingPixelFormat.Format32bppArgb);
            try
            {
                var stride = data.Stride;
                var pixels = new byte[stride * bounds.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                var source = BitmapSource.Create(
                    bounds.Width,
                    bounds.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    pixels,
                    stride);
                source.Freeze();
                return source;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
