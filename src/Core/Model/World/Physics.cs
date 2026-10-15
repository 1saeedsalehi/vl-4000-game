namespace Videoland4000.Model.Simulation;

public readonly record struct MoveResult(Rect Bounds, bool Grounded, bool HitWall, bool HitCeiling);

/// <summary>
/// Minimal, deterministic AABB collision resolution against level geometry.
/// Movement is resolved per axis (X then Y). With a 1/120 s step and speeds
/// ≤ 300 px/s a body moves at most 2.5 px per step, so no tunnelling is possible
/// through 16 px tiles.
/// </summary>
public static class Physics
{
    private const double Epsilon = 0.001;

    public static MoveResult Move(Rect bounds, double dx, double dy, Level level, bool dropThrough = false)
    {
        var hitWall = false;
        var hitCeiling = false;
        var grounded = false;

        // ----- Horizontal -----
        var moved = bounds.Translate(dx, 0);
        foreach (var solid in level.Solids)
        {
            if (!moved.Overlaps(solid))
            {
                continue;
            }

            if (dx > 0)
            {
                moved = moved.WithPosition(solid.Left - moved.Width - Epsilon, moved.Y);
            }
            else if (dx < 0)
            {
                moved = moved.WithPosition(solid.Right + Epsilon, moved.Y);
            }

            hitWall = true;
        }

        // ----- Vertical -----
        var previousBottom = moved.Bottom;
        var vertical = moved.Translate(0, dy);

        foreach (var solid in level.Solids)
        {
            if (!vertical.Overlaps(solid))
            {
                continue;
            }

            if (dy > 0)
            {
                vertical = vertical.WithPosition(vertical.X, solid.Top - vertical.Height - Epsilon);
                grounded = true;
            }
            else if (dy < 0)
            {
                vertical = vertical.WithPosition(vertical.X, solid.Bottom + Epsilon);
                hitCeiling = true;
            }
        }

        if (dy >= 0 && !dropThrough)
        {
            foreach (var platform in level.OneWayPlatforms)
            {
                // Only land when we were above the platform surface before this step.
                if (previousBottom <= platform.Top + Epsilon &&
                    vertical.Bottom >= platform.Top &&
                    vertical.Right > platform.Left &&
                    vertical.Left < platform.Right)
                {
                    vertical = vertical.WithPosition(vertical.X, platform.Top - vertical.Height);
                    grounded = true;
                }
            }
        }

        return new MoveResult(vertical, grounded, hitWall, hitCeiling);
    }

    /// <summary>True when the body is standing on something (probe 1 px below).</summary>
    public static bool IsStanding(Rect bounds, Level level)
    {
        var probe = new Rect(bounds.X + 0.5, bounds.Bottom, bounds.Width - 1, 1);
        foreach (var solid in level.Solids)
        {
            if (probe.Overlaps(solid))
            {
                return true;
            }
        }

        foreach (var platform in level.OneWayPlatforms)
        {
            if (probe.Overlaps(platform) && bounds.Bottom <= platform.Top + 1)
            {
                return true;
            }
        }

        return false;
    }

    public static double MoveToward(double current, double target, double maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(target - current) * maxDelta;
    }
}
