using System.Diagnostics;
using System.IO;
using System.Windows;
using PenBoard.Services;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace PenBoard;

/// <summary>
/// Owns the tray and the lightweight pill. The heavy WPF overlay is created
/// only when the user actually opens it, so the daemon does not keep the full
/// full-screen visual tree resident.
/// </summary>
public sealed class OverlayController : IDisposable
{
    private readonly Settings _settings;
    private readonly TrayService _tray;
    private readonly PillService _pill;
    private readonly UpdateService _updateService = new();
    private MainWindow? _window;
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
        _pill.OpenRequested += target =>
        {
            _pill.Suspend();
            OpenOverlay(target);
        };
    }

    public void Toggle()
    {
        var window = EnsureWindow();
        _pill.Suspend();
        window.Toggle();
    }

    public void OpenOverlay(IntPtr target)
    {
        var window = EnsureWindow();
        _pill.Suspend();
        window.OpenOverlay(target);
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
        if (_window is { IsOverlayOpen: true })
        {
            MessageBox.Show("请先关闭全屏手写层，再检查更新。", "检查更新");
            return;
        }

        await _updateService.CheckAndInstallAsync();
    }

    private void Exit()
    {
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
        _pill.Dispose();
        _window?.Dispose();
        _window = null;
    }
}
