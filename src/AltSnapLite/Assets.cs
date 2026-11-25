namespace AltSnapLite;

// Programmsymbol aus der eingebetteten ICO-Datei, jeweils in der passenden Grösse.
internal static class Assets
{
    internal static Icon Icon(int size)
    {
        using var stream = typeof(Assets).Assembly.GetManifestResourceStream("app.ico")!;
        return new Icon(stream, size, size);
    }

    internal static Bitmap Logo(int size)
    {
        using var icon = Icon(size);
        return icon.ToBitmap();
    }
}
