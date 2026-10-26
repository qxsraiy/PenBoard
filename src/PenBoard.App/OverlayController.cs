using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using PenBoard.Interop;
using PenBoard.Services;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace PenBoard;

/// <summary>
/// Owns the tray and the lightweight pill and launches the native
/// DirectComposition overlay on demand. The WPF overlay is only a fallback.
/// </summary>
public sealed class OverlayController : IDisposable
{
    private readonly Settings _settings;
    private readonly TrayService _tray;
    private readonly PillService _pill;
    private readonly UpdateService _updateService = new();
    private readonly CaretProbe _caretProbe = new();
    private MainWindow? _window;
    private Process? _native;
    private bool _disposed;

    public OverlayController(Settings settings)
    {
        _settings = settings;
        _tray = new TrayService(
            Toggle,
            OpenSaveFolder,
            Exit,
            _settings.DebugLogging,
            OnDebugLoggingChanged,
            OpenLogFolder,
            _settings.ShowInputPill,
            OnShowInputPillChanged,
            CheckForUpdates);

        _pill = new PillService { Enabled = _settings.ShowInputPill };
        _pill.OpenRequested += _ =>
        {
            _pill.Suspend();
            OpenNative(textMode: true);
        };
    }

    private static string NativePath =>
        Path.Combine(AppContext.BaseDirectory, "PenBoard.DcompOverlay.exe");

    public async void Toggle()
    {
        if (TryCloseNative())
        {
            return;
        }

        var target = NativeMethods.GetForegroundWindow();
        var context = await _caretProbe.ProbeAsync(target, 350);
        OpenNative(context.IsText);
    }

    public void OpenOverlay(IntPtr target)
    {
        OpenNative(textMode: true);
    }

    private void OpenNative(bool textMode)
    {
        if (!File.Exists(NativePath))
        {
            var window = EnsureWindow();
            _pill.Suspend();
            window.OpenOverlay(NativeMethods.GetForegroundWindow());
            return;
        }

        if (TryCloseNative())
        {
            return;
        }

        try
        {
            _pill.Suspend();
            if (!textMode)
            {
                Thread.Sleep(220);
            }
            var process = Process.Start(new ProcessStartInfo(
                NativePath, textMode ? "" : "--annotation")
            {
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false
            });
            if (process is null)
            {
                _pill.Resume();
                return;
            }

            _native = process;
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                _native = null;
                _pill.Resume();
            };
            Logger.Log($"Native overlay started: pid={process.Id}, text={textMode}");
        }
        catch (Exception ex)
        {
            Logger.Log("Native overlay start: " + ex);
            _pill.Resume();
        }
    }

    private bool TryCloseNative()
    {
        var hwnd = NativeMethods.FindWindow("PenBoardDcompOverlay", null);
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            Logger.Log("Native overlay close requested.");
            return true;
        }
        return false;
    }

    private MainWindow EnsureWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow(_settings, _tray);
            _window.OverlayHidden += () => _pill.Resume();
        }
        return _window;
    }

    private void OnShowInputPillChanged(bool enabled)
    {
        _settings.ShowInputPill = enabled;
        _settings.Save();
        _pill.Enabled = enabled;
    }

    private void OnDebugLoggingChanged(bool enabled)
    {
        Logger.DebugEnabled = enabled;
        _settings.DebugLogging = enabled;
        _settings.Save();
        Logger.Log("Debug logging " + (enabled ? "enabled" : "disabled"));
        _tray.ShowBalloon(
            "调试日志",
            enabled ? "已开启。复现问题后打开日志目录，把 penboard.log 发回。" : "已关闭。");
    }

    private void OpenSaveFolder()
    {
        try
        {
            Directory.CreateDirectory(_settings.SaveDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", _settings.SaveDirectory)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Log("OpenSaveFolder: " + ex);
        }
    }

    private static void OpenLogFolder()
    {
        try
        {
            var directory = Path.GetDirectoryName(Logger.LogPath)!;
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo("explorer.exe", directory)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Log("OpenLogFolder: " + ex);
        }
    }

    private async void CheckForUpdates()
    {
        if (_native is { HasExited: false } || _window is { IsOverlayOpen: true })
        {
            MessageBox.Show("请先关闭全屏手写层，再检查更新。", "检查更新");
            return;
        }

        await _updateService.CheckAndInstallAsync();
    }

    private void Exit()
    {
        TryCloseNative();
        if (_window is not null)
        {
            _window.ExitApp();
            return;
        }

        _pill.Dispose();
        _tray.Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        TryCloseNative();
        _pill.Dispose();
        _window?.Dispose();
        _window = null;
    }
}
