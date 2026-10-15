namespace Videoland4000.Persistence;

/// <summary>Validates persisted high score values so corrupted storage can never crash the game.</summary>
public static class HighScoreValidator
{
    public const int MaxScore = 9_999_999;

    public static int Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        if (!int.TryParse(raw.Trim(), out var value))
        {
            return 0;
        }

        return Math.Clamp(value, 0, MaxScore);
    }

    public static int Sanitize(int value) => Math.Clamp(value, 0, MaxScore);
}
