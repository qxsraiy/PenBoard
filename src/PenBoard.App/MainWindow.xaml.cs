using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PenBoard.Interop;
using PenBoard.Services;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;

namespace PenBoard;

public partial class MainWindow : Window, IDisposable
{
    private enum OverlayMode
    {
        Text,
        Annotation
    }

    private const int HotkeyId = 0x5042;

    private readonly Settings _settings;
    private readonly CaretProbe _caretProbe = new();
    private readonly InkRecognitionService _recognizer;
    private readonly TextInjector _injector = new();
    private readonly ScreenCapture _screenCapture = new();
    private readonly Exporter _exporter = new();
    private readonly DispatcherTimer _recognitionTimer;
    private readonly DispatcherTimer _candidateTimer;
    private readonly Stack<Stroke> _undoStack = new();
    private readonly List<Button> _toolButtons = new();
    private readonly List<Button> _colorButtons = new();
    private readonly List<Button> _widthButtons = new();
    private readonly TouchKeyboardInterceptor _touchInterceptor;

    private TrayService? _tray;
    private OverlayMode _mode = OverlayMode.Text;
    private bool _isOpen;
    private bool _opening;
    private bool _savePromptVisible;
    private long _lastToggleTick;
    private long _openedAtTick;
    private bool _isRecognizing;
    private bool _isExiting;
    private bool _disposed;
    private bool _hotkeyRegistered;
    private IntPtr _hwnd;
    private IntPtr _targetHwnd;
    private BitmapSource? _screenshot;
    private StrokeCollection? _pendingStrokes;
    private IReadOnlyList<string> _pendingCandidates = Array.Empty<string>();

    public MainWindow(Settings settings)
    {
        _settings = settings;
        InitializeComponent();

        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        Left = 0;
        Top = 0;

        _recognizer = new InkRecognitionService();
        _recognizer.SetLanguage(ParseLanguage(_settings.RecognitionLanguage));
        _recognitionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_settings.RecognitionDelayMs)
        };
        _recognitionTimer.Tick += RecognitionTimer_Tick;
        _candidateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_settings.CandidateDelayMs)
        };
        _candidateTimer.Tick += CandidateTimer_Tick;

        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        InkCanvas.PreviewTouchDown += (_, args) => args.Handled = true;
        InkCanvas.PreviewStylusDown += InkCanvas_PreviewStylusDown;

        _toolButtons.AddRange(new[] { PenButton, EraserButton, EraseStrokeButton });
        _colorButtons.AddRange(new[] { BlackButton, RedButton, BlueButton });
        _widthButtons.AddRange(new[] { ThinButton, MediumButton, ThickButton });

        _tray = new TrayService(
            () => Toggle(),
            OpenSaveFolder,
            ExitApp,
            _settings.DebugLogging,
            OnDebugLoggingChanged,
            OpenLogFolder,
            _settings.AutoReplaceTouchKeyboard,
            OnAutoKeyboardChanged);

        _touchInterceptor = new TouchKeyboardInterceptor
        {
            Enabled = _settings.AutoReplaceTouchKeyboard
        };
        _touchInterceptor.TouchPenInput += OnTouchPenInput;

        ApplyPenAttributes();
        UpdateToolHighlights();
        UpdateLanguageUi();
        UpdateSafeInsets();
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        new WindowInteropHelper(this).EnsureHandle();
        _touchInterceptor.Start(() => _hwnd);
    }

    public void Toggle(IntPtr targetHint = default)
    {
        if (_opening)
        {
            Logger.Debug("Toggle ignored: overlay is still opening.");
            return;
        }

        var now = Environment.TickCount64;
        if (now - _lastToggleTick < 700)
        {
            Logger.Debug("Toggle ignored: duplicate toggle within 700ms.");
            return;
        }
        _lastToggleTick = now;

        if (_isOpen)
        {
            CloseOverlay();
        }
        else
        {
            OpenOverlay(targetHint);
        }
    }

    public async void OpenOverlay(IntPtr targetHint = default)
    {
        if (_isOpen || _opening)
        {
            Logger.Debug("OpenOverlay ignored: already open/opening.");
            return;
        }

        _opening = true;
        _isOpen = true;
        Logger.Debug($"OpenOverlay begin: hint=0x{targetHint.ToInt64():X}");
        try
        {
            _targetHwnd = ResolveTarget(targetHint);
            var context = await _caretProbe.ProbeAsync(_targetHwnd, 400);
            if (!_isOpen)
            {
                return;
            }

            // Focus can still be settling immediately after the shortcut launch.
            // Give it one short second chance before falling back to annotation.
            if (!context.IsText)
            {
                await Task.Delay(120);
                if (!_isOpen)
                {
                    return;
                }

                var retryTarget = ResolveTarget(targetHint);
                if (retryTarget != IntPtr.Zero)
                {
                    _targetHwnd = retryTarget;
                }

                var retry = await _caretProbe.ProbeAsync(_targetHwnd, 350);
                if (retry.IsText)
                {
                    context = retry;
                }
            }

            _mode = context.IsText ? OverlayMode.Text : OverlayMode.Annotation;
            UpdateSafeInsets();
            _screenshot = null;
            if (_mode == OverlayMode.Annotation)
            {
                try
                {
                    _screenshot = _screenCapture.CapturePrimaryScreen();
                }
                catch (Exception ex)
                {
                    Logger.Log("CapturePrimaryScreen: " + ex);
                }
            }

            ResetForOpen();
            UpdateModeUi();
            if (_mode == OverlayMode.Text)
            {
                _touchInterceptor.HideTouchKeyboard();
            }
            ShowOverlay();
            Logger.Log($"OpenOverlay: mode={_mode}, target=0x{_targetHwnd.ToInt64():X}, detail={context.Detail}");
            Logger.Debug($"OpenOverlay shown: visible={IsVisible}, hwnd=0x{_hwnd.ToInt64():X}");
        }
        catch (Exception ex)
        {
            _isOpen = false;
            Logger.Log("OpenOverlay: " + ex);
            _tray?.ShowBalloon("打开失败", ex.Message, true);
        }
        finally
        {
            _opening = false;
            Logger.Debug("OpenOverlay end.");
        }
    }

    public void CloseOverlay()
    {
        if (!_isOpen || _opening)
        {
            Logger.Debug($"CloseOverlay ignored: open={_isOpen}, opening={_opening}");
            return;
        }

        if (_savePromptVisible)
        {
            return;
        }

        if (Environment.TickCount64 - _openedAtTick < 500)
        {
            Logger.Debug("CloseOverlay ignored: overlay was just opened.");
            return;
        }

        Logger.Debug($"CloseOverlay begin: mode={_mode}, strokes={InkCanvas.Strokes.Count}");
        _recognitionTimer.Stop();
        _candidateTimer.Stop();
        _pendingStrokes = null;
        _pendingCandidates = Array.Empty<string>();

        Logger.Log($"CloseOverlay: mode={_mode}, strokes={InkCanvas.Strokes.Count}");
        if (_mode == OverlayMode.Annotation && InkCanvas.Strokes.Count > 0)
        {
            ShowSavePrompt();
            return;
        }

        HideOverlay();
    }

    private void ShowSavePrompt()
    {
        if (_savePromptVisible)
        {
            return;
        }

        _savePromptVisible = true;
        SavePromptDetail.Text =
            $"有 {InkCanvas.Strokes.Count} 个笔迹。保存 PNG + PDF 后再关闭？\n保存目录：{_settings.SaveDirectory}";
        SavePromptBackdrop.Visibility = Visibility.Visible;
        InkCanvas.IsHitTestVisible = false;
        Toolbar.IsEnabled = false;
        CandidateBar.Visibility = Visibility.Collapsed;
        _recognitionTimer.Stop();
        _candidateTimer.Stop();
        Logger.Debug("Save prompt shown.");
    }

    private void HideSavePrompt()
    {
        if (!_savePromptVisible)
        {
            return;
        }

        _savePromptVisible = false;
        SavePromptBackdrop.Visibility = Visibility.Collapsed;
        InkCanvas.IsHitTestVisible = true;
        Toolbar.IsEnabled = true;
        Logger.Debug("Save prompt hidden.");
    }

    private void SavePrompt_Save_Click(object sender, RoutedEventArgs e)
    {
        Logger.Log("SavePrompt_Save_Click");
        try
        {
            SaveCurrent();
            HideSavePrompt();
            HideOverlay();
        }
        catch (Exception ex)
        {
            Logger.Log("SavePrompt_Save: " + ex);
            StatusText.Text = "保存失败：" + ex.Message;
            _tray?.ShowBalloon("保存失败", ex.Message, true);
        }
    }

    private void SavePrompt_Discard_Click(object sender, RoutedEventArgs e)
    {
        Logger.Log("SavePrompt_Discard_Click");
        Logger.Debug("Save prompt: discard.");
        HideSavePrompt();
        HideOverlay();
    }

    private void SavePrompt_Cancel_Click(object sender, RoutedEventArgs e)
    {
        HideSavePrompt();
    }

    private IntPtr ResolveTarget(IntPtr hint)
    {
        var target = hint;
        if (target == IntPtr.Zero || !NativeMethods.IsWindow(target))
        {
            target = NativeMethods.GetForegroundWindow();
        }

        if (target == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(target, out var pid);
            if (pid == (uint)Environment.ProcessId)
            {
                var foreground = NativeMethods.GetForegroundWindow();
                if (foreground != _hwnd)
                {
                    target = foreground;
                }
            }
        }
        catch
        {
            // Keep the resolved handle.
        }

        return target;
    }

    private void ShowOverlay()
    {
        if (!IsVisible)
        {
            Show();
        }

        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        if (_targetHwnd != IntPtr.Zero && NativeMethods.GetForegroundWindow() == _hwnd)
        {
            NativeMethods.SetForegroundWindow(_targetHwnd);
        }

        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNOACTIVATE);
        _openedAtTick = Environment.TickCount64;
        _tray?.SetOpen(true);
    }

    private void HideOverlay()
    {
        Logger.Log("HideOverlay");
        Hide();
        _isOpen = false;
        _isRecognizing = false;
        _savePromptVisible = false;
        _screenshot = null;
        BackgroundImage.Source = null;
        Root.Background = null;
        InkCanvas.Strokes.Clear();
        _undoStack.Clear();
        CandidatePanel.Children.Clear();
        CandidateBar.Visibility = Visibility.Collapsed;
        SavePromptBackdrop.Visibility = Visibility.Collapsed;
        InkCanvas.IsHitTestVisible = true;
        Toolbar.IsEnabled = true;
        StatusText.Text = "";
        _tray?.SetOpen(false);
    }

    private void ResetForOpen()
    {
        _recognitionTimer.Stop();
        _candidateTimer.Stop();
        _isRecognizing = false;
        _savePromptVisible = false;
        _pendingStrokes = null;
        _pendingCandidates = Array.Empty<string>();
        _undoStack.Clear();
        InkCanvas.Strokes.Clear();
        CandidatePanel.Children.Clear();
        CandidateBar.Visibility = Visibility.Collapsed;
        SavePromptBackdrop.Visibility = Visibility.Collapsed;
        InkCanvas.IsHitTestVisible = true;
        Toolbar.IsEnabled = true;
        StatusText.Text = "";
        InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        ApplyPenAttributes();
        UpdateToolHighlights();
    }

    private void UpdateSafeInsets()
    {
        try
        {
            var work = SystemParameters.WorkArea;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var bottomInset = Math.Max(0, screenHeight - work.Bottom);
            var topInset = Math.Max(0, work.Top);

            // Keep the bar above the taskbar. Auto-hide taskbars report no inset,
            // so a small minimum gap is kept as well.
            var bottom = Math.Max(72, bottomInset + 24);
            Toolbar.Margin = new Thickness(0, 0, 0, bottom);
            CandidateBar.Margin = new Thickness(0, 0, 0, bottom + 68);
            StatusText.Margin = new Thickness(22, 0, 0, bottom + 6);
            TopBanner.Margin = new Thickness(0, Math.Max(16, topInset + 16), 0, 0);

            Logger.Log($"SafeInsets: screen={screenHeight:0.#}, work={work}, toolbarBottom={bottom:0.#}");
        }
        catch (Exception ex)
        {
            Logger.Log("UpdateSafeInsets: " + ex.Message);
        }
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.WorkArea)
            or nameof(SystemParameters.PrimaryScreenWidth)
            or nameof(SystemParameters.PrimaryScreenHeight))
        {
            Dispatcher.BeginInvoke(UpdateSafeInsets);
        }
    }

    private void UpdateModeUi()
    {
        if (_mode == OverlayMode.Text)
        {
            ModeText.Text = "输入模式";
            HintText.Text = "写完后停笔自动识别；双击笔键关闭";
            ModeButton.Content = "批注";
            SaveButton.Visibility = Visibility.Collapsed;
            BackspaceButton.Visibility = Visibility.Visible;
            LanguageButton.Visibility = Visibility.Visible;
            BackgroundImage.Source = null;
            // A fully transparent layered window can be click-through. Keep one
            // alpha step so the ink canvas still receives the pen/mouse.
            Root.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
        }
        else
        {
            ModeText.Text = "批注模式";
            HintText.Text = "再次双击笔键；关闭时选择是否保存";
            ModeButton.Content = "输入";
            SaveButton.Visibility = Visibility.Visible;
            BackspaceButton.Visibility = Visibility.Collapsed;
            LanguageButton.Visibility = Visibility.Collapsed;
            BackgroundImage.Source = _screenshot;
            Root.Background = Brushes.Black;
        }
    }

    private void SaveCurrent()
    {
        if (_screenshot is null)
        {
            throw new InvalidOperationException("没有可保存的屏幕截图。");
        }

        var dipWidth = InkCanvas.ActualWidth > 0 ? InkCanvas.ActualWidth : Width;
        var dipHeight = InkCanvas.ActualHeight > 0 ? InkCanvas.ActualHeight : Height;
        var result = _exporter.Export(_screenshot, InkCanvas.Strokes, dipWidth, dipHeight, _settings);

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(result.PngPath))
        {
            parts.Add("PNG");
        }
        if (!string.IsNullOrEmpty(result.PdfPath))
        {
            parts.Add("PDF");
        }

        var message = parts.Count > 0
            ? $"{string.Join(" + ", parts)} 已保存到 {result.Directory}"
            : "没有启用保存格式。";
        StatusText.Text = message;
        _tray?.ShowBalloon("批注已保存", message);
        Logger.Log("Saved: " + message);
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

    private void OpenLogFolder()
    {
        try
        {
            var directory = Path.GetDirectoryName(Logger.LogPath) ?? "";
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

    private void OnDebugLoggingChanged(bool enabled)
    {
        Logger.DebugEnabled = enabled;
        _settings.DebugLogging = enabled;
        _settings.Save();
        Logger.Log("Debug logging " + (enabled ? "enabled" : "disabled"));
        _tray?.ShowBalloon(
            "调试日志",
            enabled
                ? "已开启。复现问题后，右键托盘图标 → 打开日志目录，把 penboard.log 发回。"
                : "已关闭。",
            false);
    }

    private void OnAutoKeyboardChanged(bool enabled)
    {
        _settings.AutoReplaceTouchKeyboard = enabled;
        _settings.Save();
        _touchInterceptor.Enabled = enabled;
        Logger.Log("Auto replace touch keyboard: " + enabled);
        if (enabled)
        {
            _touchInterceptor.HideTouchKeyboard();
            _tray?.ShowBalloon(
                "自动化已开启",
                "触控笔点击输入框或系统手写板出现时，会尝试用 PenBoard 全屏手写替换。",
                false);
        }
    }

    private async void OnTouchPenInput(IntPtr target)
    {
        if (!_settings.AutoReplaceTouchKeyboard || _isOpen || _opening)
        {
            return;
        }

        if (target == IntPtr.Zero || target == _hwnd)
        {
            return;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(target, out var pid);
            if (pid == (uint)Environment.ProcessId)
            {
                return;
            }

            Logger.Debug($"OnTouchPenInput: probing 0x{target.ToInt64():X}");
            var context = await _caretProbe.ProbeAsync(target, 300);
            if (!context.IsText)
            {
                Logger.Debug("OnTouchPenInput: target has no caret; leaving system keyboard alone.");
                return;
            }

            _touchInterceptor.HideTouchKeyboard();
            Logger.Debug("OnTouchPenInput: opening full-screen input.");
            OpenOverlay(target);
        }
        catch (Exception ex)
        {
            Logger.Log("OnTouchPenInput: " + ex.Message);
        }
    }

    public void ExitApp()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        try
        {
            _recognitionTimer.Stop();
            _candidateTimer.Stop();
            _touchInterceptor.Dispose();
            if (_hotkeyRegistered && _hwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
                _hotkeyRegistered = false;
            }
            _tray?.Dispose();
            _tray = null;
        }
        catch (Exception ex)
        {
            Logger.Log("ExitApp: " + ex);
        }

        System.Windows.Application.Current.Shutdown();
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
            SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
            _recognitionTimer.Stop();
            _candidateTimer.Stop();
            _touchInterceptor.Dispose();
            if (_hotkeyRegistered && _hwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
                _hotkeyRegistered = false;
            }
            _tray?.Dispose();
            _tray = null;
        }
        catch (Exception ex)
        {
            Logger.Log("Dispose: " + ex);
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
        NativeMethods.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_FRAMECHANGED);

        if (HwndSource.FromHwnd(_hwnd) is { } source)
        {
            source.AddHook(WndProc);
        }

        if (_settings.DebugHotkey)
        {
            _hotkeyRegistered = NativeMethods.RegisterHotKey(
                _hwnd,
                HotkeyId,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT,
                NativeMethods.VK_P);
            Logger.Log("Debug hotkey Ctrl+Alt+P registered: " + _hotkeyRegistered);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(NativeMethods.MA_NOACTIVATE);
        }

        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Dispatcher.BeginInvoke(() => Toggle());
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            CloseOverlay();
        }
    }

    private void InkCanvas_PreviewStylusDown(object sender, StylusDownEventArgs e)
    {
        if (e.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Touch)
        {
            e.Handled = true;
        }
    }

    private void InkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        _undoStack.Push(e.Stroke);
        if (_mode != OverlayMode.Text)
        {
            return;
        }

        _candidateTimer.Stop();
        CandidateBar.Visibility = Visibility.Collapsed;
        _pendingStrokes = null;
        _pendingCandidates = Array.Empty<string>();
        StatusText.Text = "";

        _recognitionTimer.Stop();
        _recognitionTimer.Start();
    }

    private async void RecognitionTimer_Tick(object? sender, EventArgs e)
    {
        _recognitionTimer.Stop();
        if (!_isOpen || _mode != OverlayMode.Text || InkCanvas.Strokes.Count == 0)
        {
            return;
        }

        if (_isRecognizing)
        {
            _recognitionTimer.Start();
            return;
        }

        if (_pendingStrokes is not null)
        {
            return;
        }

        _isRecognizing = true;
        try
        {
            var snapshot = new StrokeCollection(InkCanvas.Strokes);
            StatusText.Text = "识别中...";
            var candidates = await _recognizer.RecognizeAsync(snapshot);
            if (!_isOpen || _mode != OverlayMode.Text)
            {
                return;
            }

            if (candidates.Count == 0)
            {
                StatusText.Text = "未识别，请重写";
                return;
            }

            ShowCandidates(snapshot, candidates);
        }
        catch (Exception ex)
        {
            Logger.Log("RecognitionTimer_Tick: " + ex);
            StatusText.Text = "识别失败";
        }
        finally
        {
            _isRecognizing = false;
        }
    }

    private void ShowCandidates(StrokeCollection snapshot, IReadOnlyList<string> candidates)
    {
        var list = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Take(3)
            .ToList();

        if (list.Count == 0)
        {
            StatusText.Text = "未识别，请重写";
            return;
        }

        _pendingStrokes = snapshot;
        _pendingCandidates = list;
        CandidatePanel.Children.Clear();

        for (var i = 0; i < list.Count; i++)
        {
            var index = i;
            var text = list[i];
            var button = new Button
            {
                Content = TrimForButton(text, 14),
                ToolTip = text,
                Style = (Style)FindResource("ToolButton"),
                Margin = new Thickness(3, 0, 3, 0),
                Focusable = false
            };
            button.Click += (_, _) => CommitCandidate(index);
            CandidatePanel.Children.Add(button);
        }

        CandidateBar.Visibility = Visibility.Visible;
        StatusText.Text = "选择候选，或等待自动输入";
        _candidateTimer.Stop();
        _candidateTimer.Start();
    }

    private void CandidateTimer_Tick(object? sender, EventArgs e)
    {
        CommitCandidate(0);
    }

    private void CommitCandidate(int index)
    {
        _candidateTimer.Stop();
        CandidateBar.Visibility = Visibility.Collapsed;

        var strokes = _pendingStrokes;
        var list = _pendingCandidates;
        _pendingStrokes = null;
        _pendingCandidates = Array.Empty<string>();
        CandidatePanel.Children.Clear();

        if (strokes is null || index < 0 || index >= list.Count)
        {
            return;
        }

        var text = list[index];
        Logger.Log($"CommitCandidate: index={index}, text={text}, target=0x{_targetHwnd.ToInt64():X}");
        var ok = false;
        try
        {
            ok = _injector.TryInsert(text, _targetHwnd);
        }
        catch (Exception ex)
        {
            Logger.Log("TryInsert: " + ex);
        }

        foreach (var stroke in strokes.ToList())
        {
            InkCanvas.Strokes.Remove(stroke);
        }
        _undoStack.Clear();

        if (ok)
        {
            StatusText.Text = "已输入：" + TrimForButton(text, 30);
        }
        else
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                StatusText.Text = "目标窗口不可注入，已复制到剪贴板";
            }
            catch (Exception ex)
            {
                Logger.Log("Clipboard.SetText: " + ex);
                StatusText.Text = "目标窗口不可注入";
            }

            _tray?.ShowBalloon("未能直接输入", "文字已复制到剪贴板，请手动粘贴。", true);
        }

        if (InkCanvas.Strokes.Count > 0)
        {
            _recognitionTimer.Start();
        }
    }

    private async void Mode_Click(object sender, RoutedEventArgs e)
    {
        if (!_isOpen)
        {
            return;
        }

        if (_mode == OverlayMode.Text)
        {
            Hide();
            await Task.Delay(140);
            if (!_isOpen)
            {
                return;
            }

            try
            {
                _screenshot = _screenCapture.CapturePrimaryScreen();
            }
            catch (Exception ex)
            {
                Logger.Log("Switch to annotation capture: " + ex);
                _screenshot = null;
            }

            _mode = OverlayMode.Annotation;
            UpdateModeUi();
            ShowOverlay();
        }
        else
        {
            var probeTarget = _targetHwnd;
            if (probeTarget == IntPtr.Zero || !NativeMethods.IsWindow(probeTarget))
            {
                probeTarget = NativeMethods.GetForegroundWindow();
            }

            Logger.Log($"Mode_Click: annotation->text, probeTarget=0x{probeTarget.ToInt64():X}");
            var context = await _caretProbe.ProbeAsync(probeTarget, 350);
            Logger.Log($"Mode_Click: text={context.IsText}, source={context.Source}, detail={context.Detail}");
            if (!context.IsText)
            {
                StatusText.Text = "当前没有输入光标，保持批注模式";
                return;
            }

            _targetHwnd = probeTarget;
            _mode = OverlayMode.Text;
            _screenshot = null;
            UpdateModeUi();
            _touchInterceptor.HideTouchKeyboard();
        }
    }

    private void Pen_Click(object sender, RoutedEventArgs e)
    {
        InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        ApplyPenAttributes();
        UpdateToolHighlights();
    }

    private void Eraser_Click(object sender, RoutedEventArgs e)
    {
        InkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
        InkCanvas.EraserShape = new EllipseStylusShape(26, 26);
        StatusText.Text = "涂抹擦除";
        UpdateToolHighlights();
    }

    private void EraseStroke_Click(object sender, RoutedEventArgs e)
    {
        InkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
        StatusText.Text = "整笔擦除";
        UpdateToolHighlights();
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            _settings.InkColorHex = hex;
            _settings.Save();
            InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            ApplyPenAttributes();
            UpdateToolHighlights();
        }
    }

    private void Width_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string text } && double.TryParse(text, out var width))
        {
            _settings.InkWidth = width;
            _settings.Save();
            InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            ApplyPenAttributes();
            UpdateToolHighlights();
        }
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        var target = string.Equals(_settings.RecognitionLanguage, "en", StringComparison.OrdinalIgnoreCase)
            ? RecognitionLanguage.Chinese
            : RecognitionLanguage.English;

        if (!_recognizer.SetLanguage(target))
        {
            StatusText.Text = target == RecognitionLanguage.English
                ? "没有可用的英文手写识别器"
                : "没有可用的中文手写识别器";
            _tray?.ShowBalloon("识别语言", StatusText.Text, true);
            return;
        }

        _settings.RecognitionLanguage = target == RecognitionLanguage.English ? "en" : "zh";
        _settings.Save();
        UpdateLanguageUi();
        StatusText.Text = target == RecognitionLanguage.English
            ? "English recognition / half-width punctuation"
            : "中文识别 / 全角标点";
        Logger.Log("Language switched: " + target);
        Logger.Debug("Recognizer: " + _recognizer.CurrentRecognizerName);
    }

    private void UpdateLanguageUi()
    {
        var english = string.Equals(_settings.RecognitionLanguage, "en", StringComparison.OrdinalIgnoreCase);
        LanguageButton.Content = english ? "English" : "中文";
        LanguageButton.ToolTip = english
            ? "当前：English 识别（半角标点）"
            : "当前：中文识别（全角标点）";
        LanguageButton.Background = english
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x7D, 0xFF))
            : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        LanguageButton.BorderBrush = english
            ? Brushes.White
            : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        LanguageButton.BorderThickness = new Thickness(english ? 2 : 1);
    }

    private void Backspace_Click(object sender, RoutedEventArgs e)
    {
        if (!_isOpen || _mode != OverlayMode.Text)
        {
            return;
        }

        _recognitionTimer.Stop();
        _candidateTimer.Stop();
        _pendingStrokes = null;
        _pendingCandidates = Array.Empty<string>();
        CandidatePanel.Children.Clear();
        CandidateBar.Visibility = Visibility.Collapsed;

        var ok = _injector.TrySendBackspace(_targetHwnd);
        StatusText.Text = ok ? "已退格" : "目标窗口不可编辑";
        Logger.Debug("Backspace: ok=" + ok);
    }

    private static RecognitionLanguage ParseLanguage(string value)
        => string.Equals(value, "en", StringComparison.OrdinalIgnoreCase)
            ? RecognitionLanguage.English
            : RecognitionLanguage.Chinese;

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count > 0)
        {
            var stroke = _undoStack.Pop();
            InkCanvas.Strokes.Remove(stroke);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InkCanvas.Strokes.Clear();
        _undoStack.Clear();
        _pendingStrokes = null;
        _pendingCandidates = Array.Empty<string>();
        _candidateTimer.Stop();
        CandidateBar.Visibility = Visibility.Collapsed;
        StatusText.Text = "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_isOpen || _mode != OverlayMode.Annotation)
        {
            return;
        }

        if (InkCanvas.Strokes.Count == 0)
        {
            HideOverlay();
            return;
        }

        try
        {
            SaveCurrent();
            HideOverlay();
        }
        catch (Exception ex)
        {
            Logger.Log("Save_Click: " + ex);
            StatusText.Text = "保存失败：" + ex.Message;
            _tray?.ShowBalloon("保存失败", ex.Message, true);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    private void UpdateToolHighlights()
    {
        var accent = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x7D, 0xFF));
        var normalBackground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        var normalBorder = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        var selectedBorder = Brushes.White;

        var activeTool = InkCanvas.EditingMode switch
        {
            InkCanvasEditingMode.EraseByPoint => EraserButton,
            InkCanvasEditingMode.EraseByStroke => EraseStrokeButton,
            _ => PenButton
        };

        foreach (var button in _toolButtons)
        {
            var selected = ReferenceEquals(button, activeTool);
            button.Background = selected ? accent : normalBackground;
            button.BorderBrush = selected ? selectedBorder : normalBorder;
            button.BorderThickness = new Thickness(selected ? 2 : 1);
        }

        foreach (var button in _colorButtons)
        {
            var selected = button.Tag is string hex &&
                           string.Equals(hex, _settings.InkColorHex, StringComparison.OrdinalIgnoreCase);
            button.BorderBrush = selected ? selectedBorder : normalBorder;
            button.BorderThickness = new Thickness(selected ? 3 : 2);
            button.Opacity = selected ? 1.0 : 0.82;
        }

        foreach (var button in _widthButtons)
        {
            var selected = button.Tag is string text &&
                           double.TryParse(text, out var width) &&
                           Math.Abs(width - _settings.InkWidth) < 0.1;
            button.Background = selected ? accent : normalBackground;
            button.BorderBrush = selected ? selectedBorder : normalBorder;
            button.BorderThickness = new Thickness(selected ? 2 : 1);
        }

        Logger.Log($"Highlights: tool={activeTool.Name}, color={_settings.InkColorHex}, width={_settings.InkWidth}");
    }

    private void ApplyPenAttributes()
    {
        var attributes = InkCanvas.DefaultDrawingAttributes.Clone();
        attributes.Color = ParseColor(_settings.InkColorHex);
        attributes.Width = _settings.InkWidth;
        attributes.Height = _settings.InkWidth;
        attributes.FitToCurve = true;
        attributes.IgnorePressure = false;
        attributes.StylusTip = StylusTip.Ellipse;
        InkCanvas.DefaultDrawingAttributes = attributes;
    }

    private static System.Windows.Media.Color ParseColor(string hex)
    {
        try
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
        }
        catch
        {
            return Colors.Black;
        }
    }

    private static string TrimForButton(string text, int max)
    {
        var singleLine = text.Replace('\r', ' ').Replace('\n', ' ');
        return singleLine.Length <= max ? singleLine : singleLine[..max] + "…";
    }
}
