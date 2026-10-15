namespace Videoland4000.Model;

public enum MessageKind
{
    /// <summary>Small non-blocking toast near the bottom of the play area.</summary>
    Toast,
    /// <summary>Centered multi-line banner (terminal output style).</summary>
    Banner,
    /// <summary>Red warning banner with a brief flash.</summary>
    Alert,
}

/// <summary>A short humorous in-game message. Never blocks gameplay.</summary>
public sealed record GameMessage(
    IReadOnlyList<string> Lines,
    double DurationSeconds,
    MessageKind Kind = MessageKind.Toast)
{
    public static GameMessage Toast(double seconds, params string[] lines) =>
        new(lines, seconds, MessageKind.Toast);

    public static GameMessage Banner(double seconds, params string[] lines) =>
        new(lines, seconds, MessageKind.Banner);

    public static GameMessage Alert(double seconds, params string[] lines) =>
        new(lines, seconds, MessageKind.Alert);
}
