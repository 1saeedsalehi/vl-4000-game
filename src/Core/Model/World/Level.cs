namespace Videoland4000.Model.Simulation;

public enum SpawnKind
{
    Token,
    Coffee,
    CacheBoost,
    CircuitBreaker,
    Rollback,
    Spinner,
    ExpiredToken,
    Bug,
    Terminal,
    Checkpoint,
    Exit,
    RepairIdentity,
    RepairContent,
    RepairStreaming,
    Boss,
}

/// <summary>An entity placement parsed from the ASCII map. Position is the bottom-centre of the tile.</summary>
public sealed record EntitySpawn(SpawnKind Kind, int Col, int Row, Point Position, EntityOverride? Override)
{
    public string Id => $"{Kind}:{Col},{Row}";
}

/// <summary>
/// Parsed, collision-ready level geometry. Produced from <see cref="LevelSpec"/> by <see cref="LevelParser"/>.
/// </summary>
public sealed class Level
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Theme { get; init; } = "dungeon";
    public int TokensRequired { get; init; }
    public int Columns { get; init; }
    public int Rows { get; init; }
    public string[] IntroLines { get; init; } = Array.Empty<string>();

    public int Width => Columns * PhysicsConfig.TileSize;
    public int Height => Math.Max(PhysicsConfig.ViewportHeight, Rows * PhysicsConfig.TileSize);
    public int OffsetY => Height - Rows * PhysicsConfig.TileSize;

    public List<Rect> Solids { get; } = new();
    public List<Rect> OneWayPlatforms { get; } = new();
    public List<Rect> Hazards { get; } = new();
    public List<TileState> Tiles { get; } = new();
    public List<EntitySpawn> Spawns { get; } = new();
    public Point PlayerSpawn { get; set; }

    /// <summary>Alternative geometry (boss arena final phase). Null when the spec has no altMap.</summary>
    public Level? Alternate { get; set; }

    public bool HasBoss => Spawns.Any(s => s.Kind == SpawnKind.Boss);

    public Point TileToWorld(int col, int row) =>
        new(col * PhysicsConfig.TileSize, OffsetY + row * PhysicsConfig.TileSize);

    /// <summary>True when a solid or one-way surface exists at the given point (used for edge detection).</summary>
    public bool HasGroundAt(double x, double y)
    {
        foreach (var solid in Solids)
        {
            if (solid.Contains(new Point(x, y)))
            {
                return true;
            }
        }

        foreach (var platform in OneWayPlatforms)
        {
            if (platform.Contains(new Point(x, y)))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSolidAt(double x, double y)
    {
        foreach (var solid in Solids)
        {
            if (solid.Contains(new Point(x, y)))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Turns the ASCII map of a <see cref="LevelSpec"/> into a <see cref="Level"/>. Legend: docs/ARCHITECTURE.md §6.</summary>
public static class LevelParser
{
    private const int T = PhysicsConfig.TileSize;

    public static Level Parse(LevelSpec spec)
    {
        var level = ParseMap(spec, spec.Map);
        if (!string.IsNullOrWhiteSpace(spec.AltMap))
        {
            level.Alternate = ParseMap(spec, spec.AltMap!);
        }

        return level;
    }

    public static string[] NormalizeRows(string map)
    {
        var rows = map.Replace("\r", string.Empty).Split('\n')
            .Select(r => r.TrimEnd())
            .ToList();

        while (rows.Count > 0 && rows[0].Length == 0)
        {
            rows.RemoveAt(0);
        }

        while (rows.Count > 0 && rows[^1].Length == 0)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        if (rows.Count == 0)
        {
            throw new InvalidDataException("Level map is empty.");
        }

        var width = rows.Max(r => r.Length);
        return rows.Select(r => r.PadRight(width, '.')).ToArray();
    }

    private static Level ParseMap(LevelSpec spec, string map)
    {
        var rows = NormalizeRows(map);
        var level = new Level
        {
            Id = spec.Id,
            Name = spec.Name,
            Theme = spec.Theme,
            TokensRequired = spec.TokensRequired,
            Columns = rows[0].Length,
            Rows = rows.Length,
            IntroLines = spec.IntroLines,
        };

        var hasSpawn = false;

        for (var row = 0; row < rows.Length; row++)
        {
            for (var col = 0; col < level.Columns; col++)
            {
                var ch = rows[row][col];
                var origin = level.TileToWorld(col, row);
                var x = (int)origin.X;
                var y = (int)origin.Y;
                var bottomCentre = new Point(x + T / 2.0, y + T);
                var above = row > 0 ? rows[row - 1][col] : '.';
                var solidAbove = IsSolidChar(above);

                switch (ch)
                {
                    case '.':
                        break;
                    case '#':
                        AddSolid(level, x, y);
                        level.Tiles.Add(Tile(solidAbove ? Frames.Tiles.Floor : Frames.Tiles.FloorTop, x, y));
                        break;
                    case 'W':
                        AddSolid(level, x, y);
                        level.Tiles.Add(Tile(Frames.Tiles.Wall, x, y));
                        break;
                    case 'r':
                        AddSolid(level, x, y);
                        level.Tiles.Add(Tile(solidAbove ? Frames.Tiles.Rack : Frames.Tiles.RackTop, x, y));
                        break;
                    case '=':
                        level.OneWayPlatforms.Add(new Rect(x, y, T, 5));
                        level.Tiles.Add(Tile(Frames.Tiles.Platform, x, y));
                        break;
                    case '^':
                        level.Hazards.Add(new Rect(x + 2, y + 9, T - 4, T - 9));
                        level.Tiles.Add(Tile(Frames.Tiles.Spike, x, y));
                        break;
                    case '|':
                        level.Tiles.Add(Tile(Frames.Tiles.Cable, x, y, RenderDepth.Decor));
                        break;
                    case 'I':
                        level.Tiles.Add(Tile(Frames.Tiles.Pillar, x, y, RenderDepth.Decor));
                        break;
                    case 'g':
                        level.Tiles.Add(Tile(Frames.Tiles.Glass, x, y, RenderDepth.Decor));
                        break;
                    case 'm':
                        level.Tiles.Add(Tile(Frames.Deco.Monitor, x, y, RenderDepth.Decor));
                        break;
                    case 'l':
                        level.Tiles.Add(Tile(Frames.Deco.Lamp, x, y, RenderDepth.Decor));
                        break;
                    case 'p':
                        level.Tiles.Add(Tile(Frames.Deco.Poster, x, y, RenderDepth.Decor));
                        break;
                    case 's':
                        level.Tiles.Add(Tile(Frames.Deco.Server, x, y - T, RenderDepth.Decor));
                        break;
                    case 'c':
                        level.Tiles.Add(Tile(Frames.Deco.CoffeeMachine, x, y - T, RenderDepth.Decor));
                        break;
                    case 'P':
                        level.PlayerSpawn = bottomCentre;
                        hasSpawn = true;
                        break;
                    case 'T': AddSpawn(level, spec, SpawnKind.Token, col, row, bottomCentre); break;
                    case 'C': AddSpawn(level, spec, SpawnKind.Coffee, col, row, bottomCentre); break;
                    case 'B': AddSpawn(level, spec, SpawnKind.CacheBoost, col, row, bottomCentre); break;
                    case 'K': AddSpawn(level, spec, SpawnKind.CircuitBreaker, col, row, bottomCentre); break;
                    case 'R': AddSpawn(level, spec, SpawnKind.Rollback, col, row, bottomCentre); break;
                    case 'S': AddSpawn(level, spec, SpawnKind.Spinner, col, row, bottomCentre); break;
                    case 'X': AddSpawn(level, spec, SpawnKind.ExpiredToken, col, row, bottomCentre); break;
                    case 'G': AddSpawn(level, spec, SpawnKind.Bug, col, row, bottomCentre); break;
                    case 'A': AddSpawn(level, spec, SpawnKind.Terminal, col, row, bottomCentre); break;
                    case 'F': AddSpawn(level, spec, SpawnKind.Checkpoint, col, row, bottomCentre); break;
                    case 'E': AddSpawn(level, spec, SpawnKind.Exit, col, row, new Point(x + T, y + T)); break;
                    case '1': AddSpawn(level, spec, SpawnKind.RepairIdentity, col, row, bottomCentre); break;
                    case '2': AddSpawn(level, spec, SpawnKind.RepairContent, col, row, bottomCentre); break;
                    case '3': AddSpawn(level, spec, SpawnKind.RepairStreaming, col, row, bottomCentre); break;
                    case 'O': AddSpawn(level, spec, SpawnKind.Boss, col, row, new Point(x + T, y + T)); break;
                    default:
                        throw new InvalidDataException($"Unknown map character '{ch}' at column {col}, row {row} in level '{spec.Id}'.");
                }
            }
        }

        if (!hasSpawn)
        {
            throw new InvalidDataException($"Level '{spec.Id}' has no player spawn ('P').");
        }

        MergeHorizontalRuns(level.Solids);
        MergeHorizontalRuns(level.OneWayPlatforms);

        // Invisible boundary walls keep everything inside the level horizontally.
        level.Solids.Add(new Rect(-T, -T * 4, T, level.Height + T * 8));
        level.Solids.Add(new Rect(level.Width, -T * 4, T, level.Height + T * 8));

        return level;
    }

    private static bool IsSolidChar(char ch) => ch is '#' or 'W' or 'r';

    private static void AddSolid(Level level, int x, int y) => level.Solids.Add(new Rect(x, y, T, T));

    private static void AddSpawn(Level level, LevelSpec spec, SpawnKind kind, int col, int row, Point position)
    {
        spec.Entities.TryGetValue($"{col},{row}", out var entityOverride);
        level.Spawns.Add(new EntitySpawn(kind, col, row, position, entityOverride));
    }

    private static TileState Tile(string frame, int x, int y, int depth = RenderDepth.Tiles) =>
        new() { Frame = frame, X = x, Y = y, Depth = depth };

    /// <summary>Merges adjacent same-height rects on the same row to reduce collision checks.</summary>
    private static void MergeHorizontalRuns(List<Rect> rects)
    {
        var merged = new List<Rect>();
        foreach (var rect in rects.OrderBy(r => r.Y).ThenBy(r => r.X))
        {
            if (merged.Count > 0)
            {
                var last = merged[^1];
                if (Math.Abs(last.Y - rect.Y) < 0.001 &&
                    Math.Abs(last.Height - rect.Height) < 0.001 &&
                    Math.Abs(last.Right - rect.X) < 0.001)
                {
                    merged[^1] = new Rect(last.X, last.Y, last.Width + rect.Width, last.Height);
                    continue;
                }
            }

            merged.Add(rect);
        }

        rects.Clear();
        rects.AddRange(merged);
    }
}
