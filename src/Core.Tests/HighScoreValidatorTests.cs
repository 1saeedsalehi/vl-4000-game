namespace Videoland4000.Core.Tests;

public class HighScoreValidatorTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("garbage", 0)]
    [InlineData("{\"score\":12}", 0)]
    [InlineData("-50", 0)]
    [InlineData("12345", 12345)]
    [InlineData(" 777 ", 777)]
    [InlineData("99999999999", 0)]
    [InlineData("10000000", 9999999)]
    public void SanitizesPersistedValues(string? raw, int expected)
    {
        Assert.Equal(expected, HighScoreValidator.Sanitize(raw));
    }

    [Fact]
    public void SessionIgnoresNegativeHighScores()
    {
        var session = new GameSession(Support.TestContent.Content(), -5, new SeededRandom(1));
        Assert.Equal(0, session.HighScore);
    }
}
