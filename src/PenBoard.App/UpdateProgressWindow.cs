using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProgressBar = System.Windows.Controls.ProgressBar;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace PenBoard;

public sealed class UpdateProgressWindow : Window
{
    private readonly ProgressBar _bar;
    private readonly TextBlock _status;

    public UpdateProgressWindow()
    {
        Title = "PenBoard 更新";
        Width = 430;
        Height = 150;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));

        var panel = new StackPanel { Margin = new Thickness(22, 18, 22, 14) };
        panel.Children.Add(new TextBlock
        {
            Text = "正在下载 PenBoard 更新…",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        _status = new TextBlock
        {
            Text = "连接 GitHub…",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 8, 0, 10),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        panel.Children.Add(_status);
        _bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)),
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
        };
        panel.Children.Add(_bar);
        Content = panel;
    }

    public void SetProgress(double percent, string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetProgress(percent, text));
            return;
        }

        _bar.IsIndeterminate = percent < 0;
        if (percent >= 0)
        {
            _bar.Value = Math.Clamp(percent, 0, 100);
        }
        _status.Text = text;
    }
}
