using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace PenBoard.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly Forms.ToolStripMenuItem _debugItem;
    private readonly Forms.ToolStripMenuItem _autoKeyboardItem;
    private readonly Action _onToggle;
    private readonly Action _onOpenFolder;
    private readonly Action _onExit;
    private readonly Action<bool> _onDebugLoggingChanged;
    private readonly Action _onOpenLogs;
    private readonly Action<bool> _onAutoKeyboardChanged;
    private bool _disposed;

    public TrayService(
        Action onToggle,
        Action onOpenFolder,
        Action onExit,
        bool debugLogging,
        Action<bool> onDebugLoggingChanged,
        Action onOpenLogs,
        bool autoReplaceTouchKeyboard,
        Action<bool> onAutoKeyboardChanged)
    {
        _onToggle = onToggle;
        _onOpenFolder = onOpenFolder;
        _onExit = onExit;
        _onDebugLoggingChanged = onDebugLoggingChanged;
        _onOpenLogs = onOpenLogs;
        _onAutoKeyboardChanged = onAutoKeyboardChanged;

        var icon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "")
                   ?? Drawing.SystemIcons.Application;

        var menu = new Forms.ContextMenuStrip();
        _toggleItem = new Forms.ToolStripMenuItem("打开全屏手写", null, (_, _) => _onToggle());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        _autoKeyboardItem = new Forms.ToolStripMenuItem("自动化：替换系统手写板")
        {
            CheckOnClick = true,
            Checked = autoReplaceTouchKeyboard
        };
        _autoKeyboardItem.CheckedChanged += (_, _) => _onAutoKeyboardChanged(_autoKeyboardItem.Checked);
        menu.Items.Add(_autoKeyboardItem);

        _debugItem = new Forms.ToolStripMenuItem("调试日志（自动保存）")
        {
            CheckOnClick = true,
            Checked = debugLogging
        };
        _debugItem.CheckedChanged += (_, _) => _onDebugLoggingChanged(_debugItem.Checked);
        menu.Items.Add(_debugItem);

        menu.Items.Add(new Forms.ToolStripMenuItem("打开日志目录", null, (_, _) => _onOpenLogs()));
        menu.Items.Add(new Forms.ToolStripMenuItem("打开保存目录", null, (_, _) => _onOpenFolder()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("退出 PenBoard", null, (_, _) => _onExit()));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "PenBoard 触控笔手写",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => _onToggle();
    }

    public void SetOpen(bool open)
    {
        _toggleItem.Text = open ? "关闭全屏手写" : "打开全屏手写";
        _notifyIcon.Text = open ? "PenBoard · 全屏手写中" : "PenBoard 触控笔手写";
    }

    public void ShowBalloon(string title, string text, bool error = false)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                title,
                text,
                error ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info);
        }
        catch
        {
            // ignore
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}
