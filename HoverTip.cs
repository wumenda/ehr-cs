using System.Drawing;

namespace EhrOvertimeTray;

/// <summary>
/// 托盘悬停自绘悬浮窗：替代系统 127 字符 tooltip 限制，展示完整加班详情。
/// 鼠标悬停托盘图标时出现，移开（图标与窗体之外）自动隐藏。复用自 trae-usage-tray-cs。
/// </summary>
public sealed class HoverTip : Form
{
    private readonly Label _label = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 350 };
    private Point _anchor;

    /// <summary>悬浮窗从可见变为隐藏时触发（用于恢复托盘系统 tooltip）</summary>
    public event Action? Hidden;

    public HoverTip()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(28, 28, 34);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _label.Dock = DockStyle.Fill;
        _label.ForeColor = Color.FromArgb(235, 235, 240);
        _label.BackColor = Color.Transparent;
        _label.Font = new Font("Consolas", 10.5f);
        _label.Padding = new Padding(16, 12, 16, 12);
        _label.AutoSize = true;
        Controls.Add(_label);

        _timer.Tick += (_, _) =>
        {
            var p = Cursor.Position;
            var inForm = Bounds.Contains(p);
            var iconSize = SystemInformation.IconSize;
            var nearIcon = Math.Abs(p.X - _anchor.X) <= iconSize.Width + 8
                           && Math.Abs(p.Y - _anchor.Y) <= iconSize.Height + 8;
            if (!inForm && !nearIcon) Hide();
        };
    }

    public void ShowAt(Point anchor, string text)
    {
        _anchor = anchor;
        _label.Text = text;
        Relocate();
        if (!Visible) Show();
        _timer.Start();
    }

    public void UpdateText(string text)
    {
        _label.Text = text;
        PerformLayout();
        Relocate();
    }

    private void Relocate()
    {
        var wa = Screen.FromPoint(_anchor).WorkingArea;
        var x = _anchor.X - Width / 2;
        var y = _anchor.Y - Height - 16;
        if (x < wa.Left + 4) x = wa.Left + 4;
        if (x + Width > wa.Right - 4) x = wa.Right - 4 - Width;
        if (y < wa.Top + 4) y = _anchor.Y + iconOffsetDown();
        Location = new Point(x, y);
    }

    private static int iconOffsetDown() => SystemInformation.IconSize.Height + 32;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE：不抢焦点
            return cp;
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible)
        {
            _timer.Stop();
            Hidden?.Invoke();
        }
    }
}