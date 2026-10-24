using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using PenBoard.Interop;

namespace PenBoard.Services;

public sealed class CaretContext
{
    public bool IsText { get; init; }
    public IntPtr TargetHwnd { get; init; }
    public Rect CaretRect { get; init; } = Rect.Empty;
    public string Source { get; init; } = "";
    public string Detail { get; init; } = "";
}

public sealed class CaretProbe
{
    public CaretContext Probe(IntPtr target, int timeoutMs = 400)
    {
        var win32 = ProbeWin32(target);
        try
        {
            var task = Task.Run(() => ProbeUia(target));
            if (task.Wait(timeoutMs) && task.Result is { } uia)
            {
                if (uia.IsText)
                {
                    return uia;
                }
                if (win32.IsText)
                {
                    return win32;
                }
                return uia;
            }
        }
        catch (Exception ex)
        {
            Logger.Log("CaretProbe.Probe: " + ex.Message);
        }

        return win32;
    }

    public async Task<CaretContext> ProbeAsync(IntPtr target, int timeoutMs = 400)
    {
        var win32 = ProbeWin32(target);
        try
        {
            var uiaTask = Task.Run(() => ProbeUia(target));
            var completed = await Task.WhenAny(uiaTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completed == uiaTask && uiaTask.Status == TaskStatus.RanToCompletion && uiaTask.Result is { } uia)
            {
                if (uia.IsText)
                {
                    return uia;
                }
                if (win32.IsText)
                {
                    return win32;
                }
                return uia;
            }
        }
        catch (Exception ex)
        {
            Logger.Log("CaretProbe.ProbeAsync: " + ex.Message);
        }

        return win32;
    }

    private static CaretContext ProbeWin32(IntPtr target)
    {
        try
        {
            var threadId = 0u;
            if (target != IntPtr.Zero)
            {
                threadId = NativeMethods.GetWindowThreadProcessId(target, out _);
            }

            var info = new NativeMethods.GUITHREADINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>()
            };

            if (!NativeMethods.GetGUIThreadInfo(threadId, ref info))
            {
                return new CaretContext
                {
                    IsText = false,
                    TargetHwnd = target,
                    Source = "Win32",
                    Detail = "GetGUIThreadInfo failed"
                };
            }

            var caretHwnd = info.hwndCaret != IntPtr.Zero ? info.hwndCaret : info.hwndFocus;
            var className = GetClassName(caretHwnd);
            var editable = IsEditableClass(className) && !IsReadOnly(caretHwnd);

            var rect = Rect.Empty;
            if (info.hwndCaret != IntPtr.Zero && info.rcCaret.Right > info.rcCaret.Left && info.rcCaret.Bottom > info.rcCaret.Top)
            {
                var pt = new NativeMethods.POINT
                {
                    X = info.rcCaret.Left,
                    Y = info.rcCaret.Top
                };
                if (NativeMethods.ClientToScreen(info.hwndCaret, ref pt))
                {
                    rect = new Rect(
                        pt.X,
                        pt.Y,
                        Math.Max(1, info.rcCaret.Right - info.rcCaret.Left),
                        Math.Max(1, info.rcCaret.Bottom - info.rcCaret.Top));
                }
            }

            return new CaretContext
            {
                IsText = editable,
                TargetHwnd = target,
                CaretRect = rect,
                Source = "Win32",
                Detail = $"caret=0x{caretHwnd.ToInt64():X} class={className} editable={editable}"
            };
        }
        catch (Exception ex)
        {
            Logger.Log("ProbeWin32: " + ex.Message);
            return new CaretContext { TargetHwnd = target, Source = "Win32", Detail = "exception" };
        }
    }

    private static CaretContext ProbeUia(IntPtr target)
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused.Current.ProcessId == Environment.ProcessId)
            {
                return new CaretContext
                {
                    TargetHwnd = target,
                    Source = "UIA",
                    Detail = "focused element belongs to PenBoard"
                };
            }

            var controlType = focused.Current.ControlType;
            var isEnabled = focused.Current.IsEnabled;
            var isKeyboardFocusable = focused.Current.IsKeyboardFocusable;
            var processId = focused.Current.ProcessId;
            var name = focused.Current.Name ?? "";
            var className = focused.Current.ClassName ?? "";

            var hasValuePattern = focused.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj);
            var readOnly = false;
            if (hasValuePattern && valueObj is ValuePattern valuePattern)
            {
                readOnly = valuePattern.Current.IsReadOnly;
            }

            var hasTextPattern = focused.TryGetCurrentPattern(TextPattern.Pattern, out var textObj);
            var caretRect = Rect.Empty;
            if (hasTextPattern && textObj is TextPattern textPattern)
            {
                try
                {
                    var selection = textPattern.GetSelection();
                    if (selection.Length > 0)
                    {
                        var bounds = selection[0].GetBoundingRectangles();
                        if (bounds.Length > 0)
                        {
                            var rect = bounds[0];
                            caretRect = new Rect(rect.X, rect.Y, Math.Max(1.0, rect.Width), Math.Max(1.0, rect.Height));
                        }
                    }
                }
                catch
                {
                    // Some providers throw for a collapsed selection.
                }
            }

            var processName = "";
            try
            {
                processName = Process.GetProcessById(processId).ProcessName;
            }
            catch
            {
                // ignore
            }

            var isWord = processName.Equals("WINWORD", StringComparison.OrdinalIgnoreCase);
            var isEdit = controlType == ControlType.Edit;
            var isDocument = controlType == ControlType.Document;
            var isComboBox = controlType == ControlType.ComboBox;

            var editable = isEnabled && isKeyboardFocusable && !readOnly &&
                           (isEdit || isComboBox || (hasValuePattern && !readOnly) || (isDocument && isWord));

            return new CaretContext
            {
                IsText = editable,
                TargetHwnd = target,
                CaretRect = caretRect,
                Source = "UIA",
                Detail = $"type={controlType.ProgrammaticName} name={Truncate(name, 48)} class={className} pid={processId} value={hasValuePattern} readOnly={readOnly} text={hasTextPattern} word={isWord}"
            };
        }
        catch (Exception ex)
        {
            return new CaretContext
            {
                TargetHwnd = target,
                Source = "UIA",
                Detail = "exception: " + ex.GetType().Name
            };
        }
    }

    private static bool IsEditableClass(string className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return false;
        }

        return className.Contains("Edit", StringComparison.OrdinalIgnoreCase)
               || className.Contains("RichEdit", StringComparison.OrdinalIgnoreCase)
               || className.Contains("Scintilla", StringComparison.OrdinalIgnoreCase)
               || className.Equals("TextArea", StringComparison.OrdinalIgnoreCase)
               || className.Contains("TextBox", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadOnly(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64();
            return (style & NativeMethods.ES_READONLY) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "";
        }

        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
