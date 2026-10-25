using System.Drawing;
using System.Windows.Forms;

namespace PenBoard;

public sealed class PillForm : Form
{
    private readonly Button _button;

    public PillForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Width = 142;
        Height = 42;
        BackColor = Color.FromArgb(20, 20, 20);
        Padding = new Padding(1);

        _button = new Button
        {
            Text = "PenBoard 手写",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 136, 229),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _button.FlatAppearance.BorderColor = Color.White;
        _button.Click += (_, _) => Clicked?.Invoke();
        Controls.Add(_button);
    }

    public event Action? Clicked;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return parameters;
        }
    }

    public void ShowAt(int screenX, int screenY)
    {
        var area = Screen.FromPoint(new Point(screenX, screenY)).WorkingArea;
        var left = Math.Clamp(screenX, area.Left + 4, area.Right - Width - 4);
        var top = Math.Clamp(screenY, area.Top + 4, area.Bottom - Height - 4);
        SetBounds(left, top, Width, Height);
        if (!Visible)
        {
            Show();
        }
        BringToFront();
    }

    public bool ContainsScreenPoint(int screenX, int screenY)
        => Visible && Bounds.Contains(screenX, screenY);

    public void HidePill()
    {
        if (Visible)
        {
            Hide();
        }
    }
}
