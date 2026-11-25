#:package System.Drawing.Common@9.0.0
#:property TargetFramework=net10.0-windows
// Erzeugt assets/app.ico, assets/logo.png und docs/logo.png.
// Aufruf im Projektordner: dotnet run tools/icon.cs
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var graphite = Color.FromArgb(46, 50, 60);

GraphicsPath Rounded(RectangleF r, float radius)
{
    var path = new GraphicsPath();
    float d = radius * 2;
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

// Klassischer Pfeil-Mauszeiger, Spitze bei (x, y), Höhe h.
GraphicsPath Cursor(float x, float y, float h)
{
    float u = h / 16f;
    var points = new[] { (0f, 0f), (0f, 13.2f), (3.3f, 10.2f), (5.7f, 15.6f), (8.2f, 14.5f), (5.9f, 9.3f), (10.4f, 9.3f) }
        .Select(p => new PointF(x + p.Item1 * u, y + p.Item2 * u)).ToArray();
    var path = new GraphicsPath();
    path.AddPolygon(points);
    return path;
}

// Gezeichnet auf 256 x 256; kleine Grössen (unter 40 px) ohne "ALT", weil Schrift dort unlesbar ist.
Bitmap Render(int size)
{
    var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bitmap);
    g.Clear(Color.Transparent);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.ScaleTransform(size / 256f, size / 256f);
    using var tile = Rounded(new RectangleF(16, 16, 224, 224), 52);
    using var fill = new SolidBrush(graphite);
    g.FillPath(fill, tile);
    bool text = size >= 40;
    var window = text ? new RectangleF(50, 48, 156, 100) : new RectangleF(50, 58, 132, 108);
    using (var outline = Rounded(window, text ? 14 : 16))
    using (var pen = new Pen(Color.White, text ? 13 : 16) { LineJoin = LineJoin.Round })
        g.DrawPath(pen, outline);
    g.FillRectangle(Brushes.White, window.X, window.Y, window.Width, text ? 24 : 30);
    if (text)
    {
        using var family = new FontFamily("Segoe UI");
        using var letters = new GraphicsPath();
        letters.AddString("ALT", family, (int)FontStyle.Bold, 50, new PointF(40, 160), StringFormat.GenericTypographic);
        g.FillPath(Brushes.White, letters);
    }
    using var cursor = text ? Cursor(150, 104, 120) : Cursor(124, 112, 118);
    using (var edge = new Pen(graphite, text ? 20 : 22) { LineJoin = LineJoin.Round }) g.DrawPath(edge, cursor);
    g.FillPath(Brushes.White, cursor);
    return bitmap;
}

byte[] Png(int size)
{
    using var bitmap = Render(size);
    using var stream = new MemoryStream();
    bitmap.Save(stream, ImageFormat.Png);
    return stream.ToArray();
}

int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
var images = sizes.Select(Png).ToArray();
using (var writer = new BinaryWriter(File.Create("assets/app.ico")))
{
    writer.Write((short)0); writer.Write((short)1); writer.Write((short)sizes.Length);
    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        byte side = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
        writer.Write(side); writer.Write(side); writer.Write((byte)0); writer.Write((byte)0);
        writer.Write((short)1); writer.Write((short)32); writer.Write(images[i].Length); writer.Write(offset);
        offset += images[i].Length;
    }
    foreach (var image in images) writer.Write(image);
}
File.WriteAllBytes("assets/logo.png", Png(1024));
File.WriteAllBytes("docs/logo.png", Png(256));
Console.WriteLine("assets/app.ico, assets/logo.png, docs/logo.png");
