using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using PenBoard.Interop;

namespace PenBoard.Services;

/// <summary>
/// Experimental helper for the "replace the Windows touch keyboard" switch.
/// It detects touch/pen-generated mouse input and hides TabTip/TextInputHost
/// when it appears. Text field probing and opening the overlay stay in
/// MainWindow, so this class only decides when to ask.
/// </summary>
public sealed class TouchKeyboardInterceptor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private NativeMethods.LowLevelMouseProc? _hookProc;
    private IntPtr _hook;
    private Func<IntPtr>? _overlayHandle;
    private long _lastTouchTick;
    private long _lastTriggerTick;
    private long _lastHideTick;
    private IntPtr _lastNonKeyboardForeground;
    private bool _keyboardWasVisible;
    private bool _disposed;

    public TouchKeyboardInterceptor()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += Timer_Tick;
    }

    public event Action<IntPtr>? TouchPenInput;

    public bool Enabled { get; set; }

    public long ClickSequence { get; private set; }

    public void Start(Func<IntPtr> overlayHandle)
    {
        _overlayHandle = overlayHandle;
        _hookProc = HookCallback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);
        Logger.Log("TouchKeyboardInterceptor hook=" + (_hook != IntPtr.Zero));
        _timer.Start();
    }

    public bool IsTouchKeyboardVisible()
    {
        var visible = false;
        try
        {
            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hwnd))
                {
                    return true;
                }

                if (IsKeyboardWindow(hwnd))
                {
                    visible = true;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Logger.Log("IsTouchKeyboardVisible: " + ex.Message);
        }

        return visible;
    }

    public void ResetTrigger()
    {
        _lastTriggerTick = 0;
        _lastTouchTick = 0;
        _keyboardWasVisible = false;
    }

    public bool HideTouchKeyboard(bool force = false)
    {
        var now = Environment.TickCount64;
        if (!force && now - _lastHideTick < 1500)
        {
            return false;
        }
        _lastHideTick = now;

        var hidden = false;
        try
        {
            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hwnd))
                {
                    return true;
                }

                if (IsKeyboardWindow(hwnd))
                {
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
                    hidden = true;
                    Logger.Debug($"HideTouchKeyboard: hwnd=0x{hwnd.ToInt64():X} process={ProcessName(hwnd)} class={GetClassName(hwnd)}");
                }

                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Logger.Log("HideTouchKeyboard: " + ex.Message);
        }

        return hidden;
    }

    private static bool IsKeyboardWindow(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        var processName = "";
        try
        {
            processName = Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            // ignore
        }

        var className = GetClassName(hwnd);
        var title = GetWindowText(hwnd);
        var known =
            processName.Equals("TabTip", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("IPTip", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("TextInput", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("触摸键盘", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Touch keyboard", StringComparison.OrdinalIgnoreCase);
        if (!known)
        {
            return false;
        }

        // TextInputHost also owns tiny/helper windows. The real keyboard is a
        // reasonably sized window, so ignore 1x1 or zero-sized hosts.
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        return width >= 200 && height >= 100;
    }

    private static string ProcessName(IntPtr hwnd)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            return "";
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!Enabled || _disposed)
        {
            return;
        }

        try
        {
            var now = Environment.TickCount64;
            var foreground = NativeMethods.GetForegroundWindow();
            var overlay = _overlayHandle?.Invoke() ?? IntPtr.Zero;
            if (foreground == IntPtr.Zero || foreground == overlay)
            {
                return;
            }

            if (!IsKeyboardWindow(foreground))
            {
                _lastNonKeyboardForeground = foreground;
            }

            var keyboardVisible = IsTouchKeyboardVisible();
            var newlyVisible = keyboardVisible && !_keyboardWasVisible;
            if (keyboardVisible)
            {
                HideTouchKeyboard(newlyVisible);
            }

            if (newlyVisible)
            {
                Logger.Debug("Touch keyboard became visible; requesting overlay.");
            }
            _keyboardWasVisible = keyboardVisible;

            var touchRecent = now - _lastTouchTick < 900;
            if (!touchRecent && !keyboardVisible)
            {
                return;
            }

            if (now - _lastTriggerTick < 900)
            {
                return;
            }

            _lastTriggerTick = now;
            _lastTouchTick = 0;

            var target = _lastNonKeyboardForeground != IntPtr.Zero &&
                         NativeMethods.IsWindow(_lastNonKeyboardForeground)
                ? _lastNonKeyboardForeground
                : foreground;
            if (target == overlay)
            {
                return;
            }

            Logger.Debug($"TouchKeyboardInterceptor trigger: target=0x{target.ToInt64():X} foreground=0x{foreground.ToInt64():X} touch={touchRecent} keyboard={keyboardVisible}");
            TouchPenInput?.Invoke(target);
        }
        catch (Exception ex)
        {
            Logger.Log("TouchKeyboardInterceptor.Timer: " + ex.Message);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WM_LBUTTONDOWN && Enabled)
            {
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

                // Clicks on PenBoard's own overlay (toolbar, save prompt,
                // language button) must not trigger the automation again.
                var hit = NativeMethods.WindowFromPoint(data.pt);
                if (hit != IntPtr.Zero)
                {
                    NativeMethods.GetWindowThreadProcessId(hit, out var pid);
                    if (pid == (uint)Environment.ProcessId)
                    {
                        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
                    }
                }

                ClickSequence++;
                var foreground = NativeMethods.GetForegroundWindow();
                if (foreground != IntPtr.Zero && !IsKeyboardWindow(foreground))
                {
                    _lastNonKeyboardForeground = foreground;
                }

                _lastTouchTick = Environment.TickCount64;
                Logger.Debug($"Mouse/pen click detected: x={data.pt.X}, y={data.pt.Y}, extra=0x{data.dwExtraInfo.ToUInt64():X}");
            }
        }
        catch
        {
            // Hooks must never throw into user32.
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetWindowText(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _hookProc = null;
    }
}
