using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Eventide.App.Tray;

/// <summary>Eventide palette for the tray menu (Color Hunt "#202734 · #36415A · #B7D894 · #E0F8C7").</summary>
internal static class Palette
{
    public static readonly Color Background = ColorTranslator.FromHtml("#202734");
    public static readonly Color Surface = ColorTranslator.FromHtml("#36415A");
    public static readonly Color Hover = ColorTranslator.FromHtml("#2D3649");
    public static readonly Color Border = ColorTranslator.FromHtml("#36415A");
    public static readonly Color Accent = ColorTranslator.FromHtml("#B7D894");
    public static readonly Color AccentSoft = ColorTranslator.FromHtml("#34423A");
    public static readonly Color Highlight = ColorTranslator.FromHtml("#E0F8C7");
    public static readonly Color Text = ColorTranslator.FromHtml("#EEF2F7");
    public static readonly Color TextMuted = ColorTranslator.FromHtml("#A9B4C6");
    public static readonly Color TextDisabled = ColorTranslator.FromHtml("#7A8497");
}

/// <summary>
/// Dark tray menu: rounded hover, icons on the left, check state as an accent frame around the icon
/// (never system blue), muted status line.
/// </summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer()
        : base(new DarkColorTable())
    {
        RoundedEdges = true;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Palette.Background);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Palette.Border);
        var r = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Palette.Background);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        bounds.Inflate(-4, -1);
        using var path = RoundedRect(bounds, 6);
        using var brush = new SolidBrush(Palette.Hover);
        g.FillPath(brush, path);
    }

    /// <summary>
    /// Text is drawn by Eventide itself, vertically centred on the whole item height – exactly like the icon
    /// (Windows Forms aligns text and image separately, which left the text slightly above the icon).
    /// </summary>
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var color = e.Item.Tag is MenuTag { Muted: true }
            ? Palette.TextMuted
            : e.Item.Enabled ? Palette.Text : Palette.TextDisabled;
        var bounds = new Rectangle(e.TextRectangle.X, 0, e.TextRectangle.Width, e.Item.Height);
        const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                                      | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, color, flags);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var y = e.Item.Height / 2;
        using var pen = new Pen(Palette.Border);
        e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item is { Enabled: true } ? Palette.TextMuted : Palette.TextDisabled;
        base.OnRenderArrow(e);
    }

    /// <summary>Checked item: soft accent tile with accent outline behind the (accent) icon.</summary>
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = CenteredImageRect(e.Item, e.ImageRectangle);
        r.Inflate(3, 3);
        using var path = RoundedRect(r, 5);
        using var fill = new SolidBrush(Palette.AccentSoft);
        using var pen = new Pen(Palette.Accent, 1f);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);

        // The icon itself is drawn afterwards by OnRenderItemImage, on top of this tile.
    }

    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image is null)
        {
            return;
        }

        DrawImage(e.Graphics, e.Image, CenteredImageRect(e.Item, e.ImageRectangle));
    }

    /// <summary>Icon square of the layout size, centred vertically on the item (same centre line as the text).</summary>
    private static Rectangle CenteredImageRect(ToolStripItem item, Rectangle layout)
    {
        var size = Math.Min(layout.Width, layout.Height);
        if (item.Owner is { } owner)
        {
            size = Math.Max(size, owner.ImageScalingSize.Height);
        }

        size = Math.Min(size, item.Height - 4);
        var x = layout.X + ((layout.Width - size) / 2);
        var y = (item.Height - size) / 2;
        return new Rectangle(x, y, size, size);
    }

    private static void DrawImage(Graphics g, Image image, Rectangle rect)
    {
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(image, rect);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Palette.Background;
        public override Color ImageMarginGradientBegin => Palette.Background;
        public override Color ImageMarginGradientMiddle => Palette.Background;
        public override Color ImageMarginGradientEnd => Palette.Background;
        public override Color MenuBorder => Palette.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Palette.Hover;
        public override Color MenuItemSelectedGradientBegin => Palette.Hover;
        public override Color MenuItemSelectedGradientEnd => Palette.Hover;
        public override Color SeparatorDark => Palette.Border;
        public override Color SeparatorLight => Palette.Border;
        public override Color CheckBackground => Palette.AccentSoft;
        public override Color CheckSelectedBackground => Palette.AccentSoft;
        public override Color CheckPressedBackground => Palette.AccentSoft;
    }
}

/// <summary>Extra rendering hints for a menu item.</summary>
internal sealed record MenuTag(bool Muted = false);
