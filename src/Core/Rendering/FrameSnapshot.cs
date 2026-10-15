namespace Videoland4000.Rendering;

/// <summary>
/// The complete render state for one frame. Produced by the simulation,
/// serialised to JSON once per frame and reconciled by the Phaser renderer
/// (see docs/ARCHITECTURE.md for the wire format).
/// Property names are deliberately short to keep the per-frame payload small.
/// </summary>
public sealed class FrameSnapshot
{
    [JsonPropertyName("cam")]
    public CameraState Camera { get; set; }

    [JsonPropertyName("sprites")]
    public List<SpriteState> Sprites { get; } = new(96);

    [JsonPropertyName("labels")]
    public List<LabelState> Labels { get; } = new(16);

    /// <summary>Sound cue names (see <see cref="SoundCue"/>) to play this frame.</summary>
    [JsonPropertyName("fx")]
    public List<string> Sounds { get; } = new(8);

    /// <summary>Full-screen flash intensity 0..1 (damage feedback).</summary>
    [JsonPropertyName("flash")]
    public double Flash { get; set; }

    /// <summary>
    /// Static level geometry. Only non-null on the frame in which a new static
    /// layer must be built (level start, boss arena start, arena layout shift).
    /// </summary>
    [JsonPropertyName("level")]
    public LevelSnapshot? Level { get; set; }

    /// <summary>Monotonic id of the static layer currently expected on screen.</summary>
    [JsonPropertyName("levelId")]
    public int LevelId { get; set; }

    /// <summary>True when gameplay is advancing (false while paused / in menus).</summary>
    [JsonPropertyName("running")]
    public bool Running { get; set; }

    /// <summary>Background music track that should be playing: "title", "level", "boss", "victory" or null for silence.</summary>
    [JsonPropertyName("music")]
    public string? Music { get; set; }

    private readonly Stack<SpriteState> _spritePool = new(96);
    private readonly Stack<LabelState> _labelPool = new(16);

    /// <summary>Takes a sprite slot for this frame (pooled; the caller sets every field).</summary>
    public SpriteState AddSprite()
    {
        var sprite = _spritePool.Count > 0 ? _spritePool.Pop() : new SpriteState();
        Sprites.Add(sprite);
        return sprite;
    }

    /// <summary>Takes a label slot for this frame (pooled; the caller sets every field).</summary>
    public LabelState AddLabel()
    {
        var label = _labelPool.Count > 0 ? _labelPool.Pop() : new LabelState();
        Labels.Add(label);
        return label;
    }

    public void Clear()
    {
        foreach (var sprite in Sprites)
        {
            _spritePool.Push(sprite);
        }

        foreach (var label in Labels)
        {
            _labelPool.Push(label);
        }

        Sprites.Clear();
        Labels.Clear();
        Sounds.Clear();
        Flash = 0;
        Level = null;
    }
}

public readonly record struct CameraState(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("shake")] double Shake);

/// <summary>A sprite instance. Default origin is bottom-centre (0.5, 1).</summary>
public sealed class SpriteState
{
    [JsonPropertyName("k")] public string Key { get; set; } = string.Empty;
    [JsonPropertyName("f")] public string Frame { get; set; } = string.Empty;
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("fx")] public bool FlipX { get; set; }
    [JsonPropertyName("a")] public double Alpha { get; set; } = 1;
    [JsonPropertyName("s")] public double Scale { get; set; } = 1;
    [JsonPropertyName("d")] public int Depth { get; set; }
    /// <summary>Optional tint as 0xRRGGBB, or null for no tint.</summary>
    [JsonPropertyName("t")] public int? Tint { get; set; }
    [JsonPropertyName("ox")] public double OriginX { get; set; } = 0.5;
    [JsonPropertyName("oy")] public double OriginY { get; set; } = 1;
    /// <summary>Rotation in radians (used by projectiles/spinners).</summary>
    [JsonPropertyName("r")] public double Rotation { get; set; }
}

/// <summary>World-space pixel-font text (floating score popups, terminal labels).</summary>
public sealed class LabelState
{
    [JsonPropertyName("k")] public string Key { get; set; } = string.Empty;
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    /// <summary>CSS colour, e.g. "#ffd43b".</summary>
    [JsonPropertyName("c")] public string Color { get; set; } = "#f6f2ff";
    [JsonPropertyName("a")] public double Alpha { get; set; } = 1;
    [JsonPropertyName("d")] public int Depth { get; set; } = RenderDepth.Labels;
    /// <summary>Scroll factor: 1 = world space (default), 0 = fixed to camera.</summary>
    [JsonPropertyName("sf")] public double ScrollFactor { get; set; } = 1;
}

/// <summary>Static tile layer plus parallax theme for a level or arena.</summary>
public sealed class LevelSnapshot
{
    [JsonPropertyName("w")] public int Width { get; set; }
    [JsonPropertyName("h")] public int Height { get; set; }
    /// <summary>Background theme name, see docs/ARCHITECTURE.md ("dungeon", "arena", "title").</summary>
    [JsonPropertyName("theme")] public string Theme { get; set; } = "dungeon";
    [JsonPropertyName("tiles")] public List<TileState> Tiles { get; } = new(512);
}

/// <summary>A static tile. Origin is top-left.</summary>
public sealed class TileState
{
    [JsonPropertyName("f")] public string Frame { get; set; } = string.Empty;
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("d")] public int Depth { get; set; } = RenderDepth.Tiles;
}

/// <summary>Depth (z-order) constants shared by the simulation and renderer.</summary>
public static class RenderDepth
{
    public const int Background = -10;
    public const int Decor = -1;
    public const int Tiles = 0;
    public const int Interactables = 3;
    public const int Collectibles = 5;
    public const int Enemies = 6;
    public const int Boss = 7;
    public const int Projectiles = 8;
    public const int Player = 10;
    public const int Effects = 12;
    public const int Labels = 20;
}
