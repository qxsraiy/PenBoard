using System.Globalization;
using System.IO;
using System.Windows;
using PenBoard.Interop;
using PenBoard.Services;

namespace PenBoard;

public partial class App : System.Windows.Application
{
    private SingleInstance? _singleInstance;
    private OverlayController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        RemoveMarkOfTheWebFromSelf();

        // Capture this before any WPF window is created. The pen shortcut may
        // launch us while the real target (Notepad/Word/browser) still has focus.
        var startupForeground = NativeMethods.GetForegroundWindow();
        var command = (e.Args.FirstOrDefault() ?? "--toggle").Trim().ToLowerInvariant();
        Logger.Log($"App start: command={command}, args={string.Join(' ', e.Args)}");

        if (command is "--diag" or "--diagnose")
        {
            Diagnostics.Run();
            // This is a headless diagnostic run. Exit immediately so WPF/COM
            // finalizers cannot turn a successful report into a crash dialog.
            Environment.Exit(0);
            return;
        }

        _singleInstance = new SingleInstance();
        if (!_singleInstance.TryAcquire())
        {
            if (command == "--daemon")
            {
                // Installer launch should never toggle an already running app.
                Shutdown(0);
                return;
            }

            var message = command switch
            {
                "--quit" => "QUIT",
                "--open" or "--show" => "OPEN",
                _ => "TOGGLE"
            };
            if (message != "QUIT" && startupForeground != IntPtr.Zero)
            {
                message += " " + startupForeground.ToInt64().ToString("X", CultureInfo.InvariantCulture);
            }
            var sent = SingleInstance.SendCommand(message);
            Logger.Log($"Secondary instance: command={command}, sent={sent}, foreground=0x{startupForeground.ToInt64():X}");
            Shutdown(sent ? 0 : 2);
            return;
        }

        OskSuppression.RestoreSystemDefaults();
        var settings = Settings.Load();
        if (settings.AutoReplaceTouchKeyboard)
        {
            settings.AutoReplaceTouchKeyboard = false;
            settings.Save();
        }
        Logger.DebugEnabled = settings.DebugLogging;
        Logger.Debug($"Settings loaded: lang={settings.RecognitionLanguage}, pill={settings.ShowInputPill}");
        _controller = new OverlayController(settings);

        _singleInstance.StartServer(message =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                Logger.Log($"IPC command: {message}");
                var parts = message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var ipcCommand = parts.Length > 0 ? parts[0].Trim().ToUpperInvariant() : "";
                var hint = IntPtr.Zero;
                if (parts.Length > 1 &&
                    long.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw))
                {
                    hint = new IntPtr(raw);
                }

                switch (ipcCommand)
                {
                    case "TOGGLE":
                        _controller?.Toggle();
                        break;
                    case "OPEN":
                        _controller?.OpenOverlay(hint);
                        break;
                    case "QUIT":
                        _controller?.Dispose();
                        Shutdown(0);
                        break;
                }
            });
        });

        if (command is "--toggle" or "--open" or "--show")
        {
            Dispatcher.BeginInvoke(() => _controller.OpenOverlay(startupForeground));
        }
        else if (command == "--quit")
        {
            Dispatcher.BeginInvoke(() =>
            {
                _controller?.Dispose();
                Shutdown(0);
            });
        }
    }

    private static void RemoveMarkOfTheWebFromSelf()
    {
        foreach (var name in new[] { "PenBoard.exe", "PenBoard.dll" })
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, name);
                if (NativeMethods.DeleteFile(path + ":Zone.Identifier"))
                {
                    Logger.Log("Removed Mark-of-the-Web: " + path);
                }
            }
            catch
            {
                // The stream is normally absent; nothing to do.
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _controller?.Dispose();
            _singleInstance?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Log("OnExit: " + ex);
        }
        base.OnExit(e);
    }
}
