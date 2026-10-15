namespace Videoland4000.Geometry;

/// <summary>Axis-aligned bounding box. X/Y is the top-left corner.</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public Point Center => new(CenterX, CenterY);
    public Point BottomCenter => new(CenterX, Bottom);

    public static Rect FromCenter(double cx, double cy, double width, double height) =>
        new(cx - width / 2, cy - height / 2, width, height);

    public static Rect FromBottomCenter(double cx, double bottom, double width, double height) =>
        new(cx - width / 2, bottom - height, width, height);

    public bool Overlaps(in Rect other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

    public bool Contains(Point p) =>
        p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public Rect Translate(double dX, double dY) => new(X + dX, Y + dY, Width, Height);

    public Rect Inflate(double dX, double dY) =>
        new(X - dX, Y - dY, Width + dX * 2, Height + dY * 2);

    public Rect WithPosition(double x, double y) => new(x, y, Width, Height);
}
