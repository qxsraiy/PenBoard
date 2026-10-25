using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using PenBoard.Interop;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Border = System.Windows.Controls.Border;

namespace PenBoard;

public sealed class InputPillWindow : Window
{
    private readonly Button _button;

    public InputPillWindow()
    {
        Width = 142;
        Height = 42;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;

        _button = new Button
        {
            Content = "PenBoard 手写",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(12, 6, 12, 6),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        _button.Click += (_, _) => Clicked?.Invoke();

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x14, 0x14, 0x14)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(4),
            Child = _button
        };

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
            style |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
        };
    }

    public event Action? Clicked;

    public void ShowAt(double screenX, double screenY)
    {
        if (!IsVisible)
        {
            Show();
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var left = screenX / dpi.DpiScaleX;
        var top = screenY / dpi.DpiScaleY;
        var work = SystemParameters.WorkArea;
        left = Math.Clamp(left, work.Left + 4, Math.Max(work.Left + 4, work.Right - Width - 4));
        top = Math.Clamp(top, work.Top + 4, Math.Max(work.Top + 4, work.Bottom - Height - 4));
        Left = left;
        Top = top;
    }

    public bool ContainsScreenPoint(int screenX, int screenY)
    {
        if (!IsVisible)
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var x = screenX / dpi.DpiScaleX;
        var y = screenY / dpi.DpiScaleY;
        return x >= Left && x <= Left + Width && y >= Top && y <= Top + Height;
    }

    public void HidePill()
    {
        if (IsVisible)
        {
            Hide();
        }
    }
}
