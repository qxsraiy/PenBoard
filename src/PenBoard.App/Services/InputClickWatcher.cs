using System.Runtime.InteropServices;
using PenBoard.Interop;

namespace PenBoard.Services;

/// <summary>
/// Minimal low-level click watcher. The hook callback only reads the click
/// point and raises an event; no UIA/COM calls are made on the hook thread.
/// </summary>
public sealed class InputClickWatcher : IDisposable
{
    private NativeMethods.LowLevelMouseProc? _proc;
    private IntPtr _hook;
    private bool _disposed;

    public event Action<int, int>? Clicked;

    public void Start()
    {
        if (_hook != IntPtr.Zero || _disposed)
        {
            return;
        }

        _proc = Callback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _proc,
            NativeMethods.GetModuleHandle(null),
            0);
        Logger.Log("InputClickWatcher hook=" + (_hook != IntPtr.Zero));
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _proc = null;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WM_LBUTTONDOWN)
        {
            try
            {
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                Clicked?.Invoke(data.pt.X, data.pt.Y);
            }
            catch
            {
                // Hooks must never throw into user32.
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
