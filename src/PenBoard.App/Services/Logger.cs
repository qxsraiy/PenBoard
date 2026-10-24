using System.IO;
using System.Text;

namespace PenBoard.Services;

internal static class Logger
{
    private static readonly object Sync = new();
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PenBoard", "logs");

    public static string LogPath => Path.Combine(LogDir, "penboard.log");

    public static bool DebugEnabled { get; set; }

    public static void Debug(string message)
    {
        if (DebugEnabled)
        {
            Log("DBG " + message);
        }
    }

    public static void Log(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDir);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.ProcessId}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never take down the app.
        }
    }
}
