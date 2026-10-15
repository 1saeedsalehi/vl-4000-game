namespace Videoland4000.Model.Manifest;

public class GameSpec
{
    public Dictionary<string, AtlasSpec> Atlasses { get; set; } = new();

    /// <summary>Relative paths (from the disk folder) of level YAML files, in play order.</summary>
    public string[] Levels { get; set; } = Array.Empty<string>();

    /// <summary>Relative path of the boss arena YAML file.</summary>
    public string BossArena { get; set; } = string.Empty;

    public ScoringSpec Scoring { get; set; } = new();
    public PlayerSpec Player { get; set; } = new();
}
