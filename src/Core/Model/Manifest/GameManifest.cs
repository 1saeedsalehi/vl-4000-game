namespace Videoland4000.Model.Manifest;

/// <summary>Root of games/diskNNN/game.yaml.</summary>
public class GameManifest
{
    public GameMetadata Metadata { get; set; } = new();
    public GameSpec Spec { get; set; } = new();
    public int DiskNumber { get; set; }
    public string BasePath { get; set; } = string.Empty;
}
