namespace Videoland4000.Model.Levels;

/// <summary>
/// A level or arena as authored in YAML. The map is an ASCII tile grid
/// (16x16 px per character); the legend is documented in
/// docs/ARCHITECTURE.md and implemented by <see cref="LevelParser"/>.
/// </summary>
public class LevelSpec
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Theme { get; set; } = "dungeon";
    public int TokensRequired { get; set; } = 5;

    /// <summary>ASCII tile map; rows separated by newlines, all rows equal width.</summary>
    public string Map { get; set; } = string.Empty;

    /// <summary>Optional alternative map used by the boss arena in its final phase.</summary>
    public string? AltMap { get; set; }

    /// <summary>Optional per-entity overrides keyed by map coordinate "col,row".</summary>
    public Dictionary<string, EntityOverride> Entities { get; set; } = new();

    public string[] IntroLines { get; set; } = Array.Empty<string>();
}

public class EntityOverride
{
    /// <summary>Patrol half-width in pixels for patrolling enemies.</summary>
    public double? PatrolRange { get; set; }
    public double? Speed { get; set; }
    /// <summary>Bounding box size in tiles (w,h) for erratic enemies.</summary>
    public int? BoxWidth { get; set; }
    public int? BoxHeight { get; set; }
}
