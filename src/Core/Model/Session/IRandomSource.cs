namespace Videoland4000.Model.Session;

/// <summary>Random source abstraction so tests can make the simulation deterministic.</summary>
public interface IRandomSource
{
    /// <summary>Uniform double in [0, 1).</summary>
    double NextDouble();

    /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
    int Next(int minInclusive, int maxExclusive);
}

public sealed class SeededRandom : IRandomSource
{
    private readonly Random _random;

    public SeededRandom(int seed) => _random = new Random(seed);

    public SeededRandom() => _random = new Random();

    public double NextDouble() => _random.NextDouble();

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
