using System.Diagnostics;
using System.Windows.Forms;
using PenBoard.Interop;

namespace PenBoard.Services;

/// <summary>
/// Lightweight WinForms pill shown next to an editable field. It deliberately
/// does not create a WPF window, so it can stay available while the WPF overlay
/// is not loaded.
/// </summary>
public sealed class PillService : IDisposable
{
    private CaretProbe? _probe;
    private readonly InputClickWatcher _clickWatcher = new();
    private PillForm? _form;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _enabled = true;
    private bool _suspended;
    private bool _busy;

    public PillService()
    {
        _clickWatcher.Clicked += OnClick;
        _timer = new System.Windows.Forms.Timer
        {
            Interval = 900
        };
        _timer.Tick += Timer_Tick;
    }

    public event Action<IntPtr>? OpenRequested;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                Hide();
                _clickWatcher.Stop();
                _timer.Stop();
            }
            else if (!_suspended)
            {
                _clickWatcher.Start();
            }
        }
    }

    public void Suspend()
    {
        _suspended = true;
        Hide();
    }

    public void Resume()
    {
        _suspended = false;
        if (_enabled)
        {
            _clickWatcher.Start();
        }
    }

    public void Hide() => _form?.HidePill();

    private async void OnClick(int x, int y)
    {
        _probe ??= new CaretProbe();
        var form = EnsureForm();
        _timer.Start();
        if (!_enabled || _suspended || form.ContainsScreenPoint(x, y))
        {
            return;
        }

        form.HidePill();
        var hit = NativeMethods.WindowFromPoint(new NativeMethods.POINT { X = x, Y = y });
        if (hit == IntPtr.Zero)
        {
            hit = NativeMethods.GetForegroundWindow();
        }
        await ProbeAndShow(hit, x, y);
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_enabled || _suspended || _busy || _probe is null)
        {
            return;
        }

        _busy = true;
        try
        {
            var focused = await Task.Run(() =>
            {
                try
                {
                    var element = System.Windows.Automation.AutomationElement.FocusedElement;
                    return (element.Current.ProcessId, new IntPtr(element.Current.NativeWindowHandle));
                }
                catch
                {
                    return (0, IntPtr.Zero);
                }
            });

            if (focused.Item1 == Environment.ProcessId)
            {
                return;
            }

            var target = focused.Item2 != IntPtr.Zero && NativeMethods.IsWindow(focused.Item2)
                ? focused.Item2
                : NativeMethods.GetForegroundWindow();
            if (target == IntPtr.Zero)
            {
                return;
            }

            var probe = _probe;
            if (probe is null)
            {
                return;
            }
            var context = await probe.ProbeAsync(target, 300);
            if (!context.IsText)
            {
                _form?.HidePill();
                return;
            }

            _lastTarget = target;
            var cursor = Cursor.Position;
            var x = context.CaretRect.IsEmpty ? cursor.X + 12 : context.CaretRect.Right + 8;
            var y = context.CaretRect.IsEmpty ? cursor.Y + 18 : context.CaretRect.Top;
            EnsureForm().ShowAt((int)x, (int)y);
        }
        catch (Exception ex)
        {
            Logger.Debug("PillService timer: " + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private IntPtr _lastTarget;

    private async Task ProbeAndShow(IntPtr target, int x, int y)
    {
        if (target == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(target, out var pid);
        if (pid == (uint)Environment.ProcessId)
        {
            return;
        }

        var probe = _probe;
        if (probe is null)
        {
            return;
        }
        var context = await probe.ProbeAsync(target, 300, x, y);
        if (!context.IsText)
        {
            return;
        }

        _lastTarget = target;
        var left = context.CaretRect.IsEmpty ? x + 12 : context.CaretRect.Right + 8;
        var top = context.CaretRect.IsEmpty ? y + 18 : context.CaretRect.Top;
        EnsureForm().ShowAt((int)left, (int)top);
    }

    private PillForm EnsureForm()
    {
        if (_form is null)
        {
            _form = new PillForm();
            _form.Clicked += OnPillClicked;
        }
        return _form;
    }

    private void OnPillClicked()
    {
        _form?.HidePill();
        OpenRequested?.Invoke(_lastTarget);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _clickWatcher.Dispose();
        _form?.Dispose();
    }
}
