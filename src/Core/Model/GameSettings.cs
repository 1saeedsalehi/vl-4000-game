namespace Videoland4000.Model;

/// <summary>Player preferences. Persisted locally when storage is available.</summary>
public sealed record GameSettings
{
    public bool SoundEnabled { get; init; } = true;
    public bool CrtEnabled { get; init; } = true;
    /// <summary>Set from the OS prefers-reduced-motion media query or by the player.</summary>
    public bool ReducedMotion { get; init; }

    public static readonly GameSettings Default = new();
}
