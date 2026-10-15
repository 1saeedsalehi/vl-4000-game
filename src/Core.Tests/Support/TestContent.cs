using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Videoland4000.Core.Tests.Support;

/// <summary>Loads the real game disk (copied to the test output) and builds sessions/worlds for tests.</summary>
public static class TestContent
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static string DiskPath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, "disk001", relative.Replace('/', Path.DirectorySeparatorChar));

    public static GameManifest Manifest()
    {
        var manifest = Yaml.Deserialize<GameManifest>(File.ReadAllText(DiskPath("game.yaml")));
        manifest.BasePath = "games/disk001/";
        manifest.DiskNumber = 1;
        return manifest;
    }

    public static LevelSpec Level() => Yaml.Deserialize<LevelSpec>(File.ReadAllText(DiskPath("levels/login-dungeon.yaml")));

    public static LevelSpec Arena() => Yaml.Deserialize<LevelSpec>(File.ReadAllText(DiskPath("levels/boss-arena.yaml")));

    public static GameContent Content() => new(Manifest(), Level(), Arena());

    public static GameSession NewSession(int highScore = 0, int seed = 1) =>
        new(Content(), highScore, new SeededRandom(seed));

    /// <summary>Boot → menu → new game → skip intro: the session is in the Playing phase.</summary>
    public static GameSession StartPlaying(int highScore = 0, int seed = 1)
    {
        var session = NewSession(highScore, seed);
        Assert.True(session.CompleteBoot());
        Assert.True(session.StartNewGame());
        Assert.True(session.SkipIntro());
        Assert.Equal(GamePhase.Playing, session.Phase);
        return session;
    }

    public static LevelSpec Synthetic(string map, int tokensRequired = 1, string? altMap = null) => new()
    {
        Id = "test",
        Name = "TEST LEVEL",
        Map = map,
        AltMap = altMap,
        TokensRequired = tokensRequired,
    };

    public static World NewWorld(LevelSpec spec, int seed = 1, int maxHealth = 3) =>
        new(LevelParser.Parse(spec), new SeededRandom(seed), maxHealth, new ScoringSpec());

    public static World NewWorld(string map, int tokensRequired = 1, int seed = 1) =>
        NewWorld(Synthetic(map, tokensRequired), seed);

    /// <summary>A flat 20-tile floor with the player at the left. Floor top is y = 164.</summary>
    public const string FlatFloor = """
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        .P..................
        ####################
        """;
}

/// <summary>Steps a world with held-input edge detection, like the session does.</summary>
public sealed class Driver
{
    private InputButtons _previous;

    public void Step(World world, InputButtons held = InputButtons.None, int steps = 1)
    {
        for (var i = 0; i < steps; i++)
        {
            world.Step(PhysicsConfig.FixedStep, InputState.FromHeld(held, _previous));
            _previous = held;
        }
    }

    public void Seconds(World world, double seconds, InputButtons held = InputButtons.None) =>
        Step(world, held, (int)Math.Round(seconds / PhysicsConfig.FixedStep));

    /// <summary>Press (edge) then keep holding for the given number of steps.</summary>
    public void Press(World world, InputButtons button, int holdSteps = 1, InputButtons alsoHeld = InputButtons.None)
    {
        _previous &= ~button;
        Step(world, button | alsoHeld, holdSteps);
    }

    public void Release(World world) => Step(world, InputButtons.None, 1);

    public static List<WorldEvent> Drain(World world)
    {
        var events = new List<WorldEvent>();
        while (world.Events.TryDequeue(out var e))
        {
            events.Add(e);
        }

        return events;
    }
}

/// <summary>Drives a session frame by frame (60 Hz) with held input.</summary>
public static class SessionDriver
{
    public const double FrameSeconds = 1.0 / 60.0;

    public static void Frames(GameSession session, int frames, InputButtons held = InputButtons.None)
    {
        for (var i = 0; i < frames; i++)
        {
            session.Tick(FrameSeconds, held);
        }
    }

    public static void Seconds(GameSession session, double seconds, InputButtons held = InputButtons.None) =>
        Frames(session, (int)Math.Round(seconds / FrameSeconds), held);

    /// <summary>One frame with the button newly pressed (previous frame without it).</summary>
    public static void Press(GameSession session, InputButtons button)
    {
        session.Tick(FrameSeconds, InputButtons.None);
        session.Tick(FrameSeconds, button);
        session.Tick(FrameSeconds, InputButtons.None);
    }
}
