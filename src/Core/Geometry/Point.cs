namespace Videoland4000.Geometry;

/// <summary>A 2D point/vector in logical pixel units (y grows downwards).</summary>
public readonly record struct Point(double X, double Y)
{
    public static readonly Point Zero = new(0, 0);

    public Point Offset(double dX, double dY) => new(X + dX, Y + dY);

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
    public static Point operator *(Point a, double s) => new(a.X * s, a.Y * s);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public Point Normalized()
    {
        var length = Length;
        return length < 1e-9 ? Zero : new Point(X / length, Y / length);
    }

    public static double DistanceBetween(Point p1, Point p2) => (p2 - p1).Length;
}
