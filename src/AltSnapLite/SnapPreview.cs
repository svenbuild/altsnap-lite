namespace AltSnapLite;

internal sealed class SnapPreview : Form
{
    internal SnapPreview()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(82, 105, 236);
        Opacity = 0.22;
        StartPosition = FormStartPosition.Manual;
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x20 | 0x80; return p; }
    }
    internal void ShowBounds(Rectangle bounds)
    {
        bounds.Inflate(-6, -6);
        if (Bounds != bounds) Bounds = bounds;
        if (!Visible) Show();
    }
}
