namespace AltSnapLite;

internal enum SnapKind { None, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight, Maximize }
internal readonly record struct SnapTarget(SnapKind Kind, Rectangle Bounds);

internal static class Geometry
{
    internal static SnapTarget Snap(Point cursor, Rectangle area, int distance)
    {
        bool left = cursor.X < area.Left + distance;
        bool right = cursor.X >= area.Right - distance;
        bool top = cursor.Y < area.Top + distance;
        bool bottom = cursor.Y >= area.Bottom - distance;
        int middleX = area.Left + area.Width / 2, middleY = area.Top + area.Height / 2;
        if (left && top) return new(SnapKind.TopLeft, Rectangle.FromLTRB(area.Left, area.Top, middleX, middleY));
        if (right && top) return new(SnapKind.TopRight, Rectangle.FromLTRB(middleX, area.Top, area.Right, middleY));
        if (left && bottom) return new(SnapKind.BottomLeft, Rectangle.FromLTRB(area.Left, middleY, middleX, area.Bottom));
        if (right && bottom) return new(SnapKind.BottomRight, Rectangle.FromLTRB(middleX, middleY, area.Right, area.Bottom));
        if (left) return new(SnapKind.Left, Rectangle.FromLTRB(area.Left, area.Top, middleX, area.Bottom));
        if (right) return new(SnapKind.Right, Rectangle.FromLTRB(middleX, area.Top, area.Right, area.Bottom));
        if (top) return new(SnapKind.Maximize, area);
        return default;
    }

    internal static (int X, int Y) ResizeEdges(Point point, Rectangle bounds)
    {
        double x = (point.X - bounds.Left) / (double)bounds.Width;
        double y = (point.Y - bounds.Top) / (double)bounds.Height;
        int edgeX = x < 1.0 / 3 ? -1 : x > 2.0 / 3 ? 1 : 0;
        int edgeY = y < 1.0 / 3 ? -1 : y > 2.0 / 3 ? 1 : 0;
        return edgeX == 0 && edgeY == 0 ? (1, 1) : (edgeX, edgeY);
    }

    internal static Rectangle Resize(Rectangle start, Point delta, int edgeX, int edgeY, Size minimum)
    {
        int left = start.Left, top = start.Top, right = start.Right, bottom = start.Bottom;
        if (edgeX < 0) left = Math.Min(left + delta.X, right - minimum.Width);
        if (edgeX > 0) right = Math.Max(right + delta.X, left + minimum.Width);
        if (edgeY < 0) top = Math.Min(top + delta.Y, bottom - minimum.Height);
        if (edgeY > 0) bottom = Math.Max(bottom + delta.Y, top + minimum.Height);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
