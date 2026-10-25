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

            var direct = EvaluateElement(focused, target, "focus");
            if (direct.IsText)
            {
                return direct;
            }

            // UWP input can report a Windows.UI.Input.InputSite.WindowClass
            // host as the focused element instead of the actual text box.
            // Search the nearest app window for a focused editable control.
            var fallback = FindEditableFromTarget(target);
            if (fallback is not null)
            {
                var result = EvaluateElement(fallback, target, "fallback");
                if (result.IsText)
                {
                    return result;
                }
            }

            return direct;
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

    private static AutomationElement? FindEditableFromTarget(IntPtr target)
    {
        if (target == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var element = AutomationElement.FromHandle(target);
            if (element is null)
            {
                return null;
            }

            var root = element;
            for (var i = 0; i < 8; i++)
            {
                var parent = TreeWalker.ControlViewWalker.GetParent(root);
                if (parent is null || parent == AutomationElement.RootElement)
                {
                    break;
                }
                root = parent;
            }

            var found = FindEditable(root) ?? FindEditable(element);
            if (found is not null)
            {
                return found;
            }

            // Some UWP hosts are top-level InputSite windows whose text control
            // lives in another top-level window of the same process. Search the
            // other windows for the element that actually has keyboard focus.
            NativeMethods.GetWindowThreadProcessId(target, out var processId);
            if (processId != 0)
            {
                var windows = AutomationElement.RootElement.FindAll(
                    TreeScope.Children,
                    System.Windows.Automation.Condition.TrueCondition);
                foreach (AutomationElement window in windows)
                {
                    if (window.Current.ProcessId == (int)processId)
                    {
                        var focusedEdit = FindFocusedEditable(window);
                        if (focusedEdit is not null)
                        {
                            return focusedEdit;
                        }
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static AutomationElement? FindFocusedEditable(AutomationElement root)
    {
        try
        {
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox));

            var elements = root.FindAll(TreeScope.Subtree, condition);
            foreach (AutomationElement element in elements)
            {
                if (element.Current.HasKeyboardFocus)
                {
                    return element;
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static AutomationElement? FindEditable(AutomationElement root)
    {
        try
        {
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox));

            var elements = root.FindAll(TreeScope.Subtree, condition);
            AutomationElement? first = null;
            foreach (AutomationElement element in elements)
            {
                if (element.Current.HasKeyboardFocus)
                {
                    return element;
                }

                if (first is null && element.Current.IsKeyboardFocusable && element.Current.IsEnabled)
                {
                    first = element;
                }
            }

            // For an InputSite host, the actual edit is often the only editable
            // descendant. Do not use this fallback for normal windows, where it
            // could pick an unrelated search box.
            var className = root.Current.ClassName ?? "";
            if (className.Contains("InputSite", StringComparison.OrdinalIgnoreCase))
            {
                return first;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static CaretContext EvaluateElement(AutomationElement element, IntPtr target, string source)
    {
        try
        {
            var controlType = element.Current.ControlType;
            var isEnabled = element.Current.IsEnabled;
            var isKeyboardFocusable = element.Current.IsKeyboardFocusable;
            var hasFocus = element.Current.HasKeyboardFocus;
            var processId = element.Current.ProcessId;
            var name = element.Current.Name ?? "";
            var className = element.Current.ClassName ?? "";

            var hasValuePattern = element.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj);
            var readOnly = false;
            if (hasValuePattern && valueObj is ValuePattern valuePattern)
            {
                readOnly = valuePattern.Current.IsReadOnly;
            }

            var hasTextPattern = element.TryGetCurrentPattern(TextPattern.Pattern, out var textObj);
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
            var isNotepad = processName.Equals("notepad", StringComparison.OrdinalIgnoreCase);
            var isEdit = controlType == ControlType.Edit;
            var isDocument = controlType == ControlType.Document;
            var isComboBox = controlType == ControlType.ComboBox;

            var editEditable = isEdit && (!hasValuePattern || !readOnly);
            var comboEditable = isComboBox && hasValuePattern && !readOnly;
            var documentEditable = isDocument &&
                                   (isWord || isNotepad || (hasValuePattern && !readOnly));

            // Chromium/Electron contenteditable controls (QQ, Teams, Discord,
            // CodeMirror, ProseMirror, Monaco...) often expose ControlType.Group
            // or Pane plus a TextPattern instead of an Edit control. Accept them
            // only when the class looks like an editor; otherwise a whole web
            // page's text container would be mistaken for an input box.
            var editorClass =
                className.Contains("ProseMirror", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("CodeMirror", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("Editor", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("contenteditable", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("ql-editor", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("ace_editor", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("monaco", StringComparison.OrdinalIgnoreCase);
            var containerEditable =
                (controlType == ControlType.Group ||
                 controlType == ControlType.Pane ||
                 controlType == ControlType.Custom) &&
                hasTextPattern &&
                editorClass &&
                !readOnly &&
                (hasFocus || isKeyboardFocusable);

            var editable = isEnabled &&
                           (isKeyboardFocusable || hasFocus) &&
                           (editEditable || comboEditable || documentEditable || containerEditable);

            return new CaretContext
            {
                IsText = editable,
                TargetHwnd = target,
                CaretRect = caretRect,
                Source = "UIA",
                Detail = $"{source} type={controlType.ProgrammaticName} name={Truncate(name, 48)} class={className} pid={processId} value={hasValuePattern} readOnly={readOnly} text={hasTextPattern} focus={hasFocus} word={isWord}"
            };
        }
        catch (Exception ex)
        {
            return new CaretContext
            {
                TargetHwnd = target,
                Source = "UIA",
                Detail = source + " exception: " + ex.GetType().Name
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
