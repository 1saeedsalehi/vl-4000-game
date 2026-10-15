namespace Videoland4000.Model.Manifest;

/// <summary>Configurable score values. Defaults follow the game design brief.</summary>
public class ScoringSpec
{
    public int Token { get; set; } = 100;
    public int Coffee { get; set; } = 50;
    public int Enemy { get; set; } = 150;
    public int Checkpoint { get; set; } = 250;
    public int Objective { get; set; } = 500;
    public int Boss { get; set; } = 2000;
}
