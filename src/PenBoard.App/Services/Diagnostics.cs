using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using PenBoard.Interop;
using Windows.UI.Input.Inking;

namespace PenBoard.Services;

public static class Diagnostics
{
    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("PenBoard diagnostics");
        report.AppendLine("====================");
        report.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine("OS: " + Environment.OSVersion);
        report.AppendLine("OS architecture: " + RuntimeInformation.OSArchitecture);
        report.AppendLine("Process architecture: " + RuntimeInformation.ProcessArchitecture);
        report.AppendLine("Runtime: " + RuntimeInformation.FrameworkDescription);
        report.AppendLine("Executable: " + Environment.ProcessPath);
        report.AppendLine("Primary screen (WPF DIP): " + SystemParameters.PrimaryScreenWidth + " x " + SystemParameters.PrimaryScreenHeight);
        report.AppendLine("Primary screen (px): " + System.Windows.Forms.Screen.PrimaryScreen?.Bounds);

        try
        {
            var foreground = NativeMethods.GetForegroundWindow();
            var context = new CaretProbe().Probe(foreground, 700);
            report.AppendLine($"Caret probe: text={context.IsText}, source={context.Source}, detail={context.Detail}");
        }
        catch (Exception ex)
        {
            report.AppendLine("Caret probe failed: " + ex);
        }

        try
        {
            var recognition = new InkRecognitionService();
            report.AppendLine("Recognition available: " + recognition.IsAvailable);
            report.AppendLine("Recognition: " + recognition.Describe());
        }
        catch (Exception ex)
        {
            report.AppendLine("Recognition failed: " + ex);
        }

        try
        {
            var builder = new InkStrokeBuilder();
            var points = new List<InkPoint>
            {
                new(new Windows.Foundation.Point(50, 50), 0.5f),
                new(new Windows.Foundation.Point(100, 50), 0.5f),
                new(new Windows.Foundation.Point(100, 120), 0.5f)
            };
            var stroke = builder.CreateStrokeFromInkPoints(points, Matrix3x2.Identity);
            var container = new InkStrokeContainer();
            container.AddStroke(stroke);
            report.AppendLine("Ink runtime: OK, strokes=" + container.GetStrokes().Count);
        }
        catch (Exception ex)
        {
            report.AppendLine("Ink runtime failed: " + ex);
        }

        try
        {
            var capture = new ScreenCapture().CapturePrimaryScreen();
            report.AppendLine($"Screen capture: {capture.PixelWidth} x {capture.PixelHeight}, dpi={capture.DpiX}x{capture.DpiY}");
        }
        catch (Exception ex)
        {
            report.AppendLine("Screen capture failed: " + ex);
        }

        report.AppendLine("Log: " + Logger.LogPath);
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PenBoard");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "diagnostics.txt");
        File.WriteAllText(path, report.ToString(), Encoding.UTF8);
        Logger.Log("Diagnostics written: " + path);
    }
}
