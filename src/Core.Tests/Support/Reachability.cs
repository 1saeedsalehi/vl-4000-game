namespace Videoland4000.Core.Tests.Support;

/// <summary>
/// Conservative reachability analysis of a level using the real jump physics:
/// stand points are sampled along every walkable surface; a hop between two
/// stand points is possible when the rise is below the jump height and the
/// horizontal distance is below the distance covered in the time it takes to
/// reach that height on the way down (times a 0.9 safety factor).
/// </summary>
public static class Reachability
{
    private const double Sample = 4;
    private const double BodyWidth = PhysicsConfig.PlayerWidth;
    private const double BodyHeight = PhysicsConfig.PlayerHeight;
    private const double Safety = 0.9;

    public static double MaxRise =>
        PhysicsConfig.JumpVelocity * PhysicsConfig.JumpVelocity / (2 * PhysicsConfig.Gravity) - 2;

    /// <summary>Horizontal distance reachable for a given rise (positive = up). Negative rise = drop.</summary>
    public static double HorizontalReach(double rise)
    {
        var v = PhysicsConfig.JumpVelocity;
        var g = PhysicsConfig.Gravity;
        var discriminant = v * v - 2 * g * rise;
        if (discriminant < 0)
        {
            return -1;
        }

        var tDown = (v + Math.Sqrt(discriminant)) / g;
        return tDown * PhysicsConfig.RunSpeed * Safety;
    }

    public static HashSet<Point> ReachableStandPoints(Level level)
    {
        var standPoints = new List<Point>();
        foreach (var surface in level.Solids.Concat(level.OneWayPlatforms))
        {
            if (surface.Top < -8 || surface.Top > level.Height)
            {
                continue;
            }

            for (var x = surface.Left + BodyWidth / 2; x <= surface.Right - BodyWidth / 2 + 0.01; x += Sample)
            {
                var feet = new Point(x, surface.Top);
                var body = Rect.FromBottomCenter(feet.X, feet.Y, BodyWidth, BodyHeight);
                if (level.Solids.Any(s => s.Overlaps(body)))
                {
                    continue; // no head room
                }

                if (level.Hazards.Any(h => h.Overlaps(body.Inflate(2, 2))))
                {
                    continue; // standing in a hazard is not "safe"
                }

                standPoints.Add(feet);
            }
        }

        var start = standPoints.OrderBy(p => Point.DistanceBetween(p, level.PlayerSpawn)).First();
        var visited = new HashSet<Point> { start };
        var queue = new Queue<Point>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var from = queue.Dequeue();
            foreach (var to in standPoints)
            {
                if (visited.Contains(to))
                {
                    continue;
                }

                var rise = from.Y - to.Y;
                var reach = HorizontalReach(rise);
                if (reach < 0 || Math.Abs(to.X - from.X) > reach)
                {
                    continue;
                }

                if (rise > MaxRise)
                {
                    continue;
                }

                visited.Add(to);
                queue.Enqueue(to);
            }
        }

        return visited;
    }

    /// <summary>A target rect is reachable when the player body can overlap it from a reachable stand point, standing or jumping.</summary>
    public static bool CanTouch(Rect target, HashSet<Point> standPoints)
    {
        foreach (var feet in standPoints)
        {
            var column = new Rect(feet.X - BodyWidth / 2, feet.Y - BodyHeight - MaxRise, BodyWidth, BodyHeight + MaxRise);
            if (column.Overlaps(target))
            {
                return true;
            }
        }

        return false;
    }
}
