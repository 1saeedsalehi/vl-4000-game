using Videoland4000.Core.Tests.Support;

namespace Videoland4000.Core.Tests;

public class ObjectiveTests
{
    private const string ObjectiveLevel = """
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        P.T.TF...A...F....E.
        ####################
        """;

    private static void CollectAllTokens(World world, Driver driver)
    {
        foreach (var token in world.Collectibles.Where(c => c.Kind == CollectibleKind.Token))
        {
            world.Player.Teleport(new Point(token.Origin.X, token.Origin.Y + 6));
            driver.Step(world, steps: 2);
        }
    }

    private static void Interact(World world, Driver driver, Rect zone)
    {
        world.Player.Teleport(new Point(zone.CenterX, 164));
        driver.Step(world, steps: 2);
        driver.Press(world, InputButtons.Interact, holdSteps: 2);
        driver.Release(world);
    }

    [Fact]
    public void TerminalDeniesWhenTokensAreMissing()
    {
        var world = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        var driver = new Driver();

        Interact(world, driver, world.Terminal!.InteractZone);

        Assert.False(world.TerminalActivated);
        Assert.False(world.ExitUnlocked);
        var events = Driver.Drain(world);
        Assert.Single(events, e => e.Kind == WorldEventKind.TerminalDenied && e.Value == 2);
    }

    [Fact]
    public void TerminalActivatesOnceWithEnoughTokensAndUnlocksTheExit()
    {
        var world = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        var driver = new Driver();

        CollectAllTokens(world, driver);
        Assert.Equal(2, world.TokensCollected);
        Driver.Drain(world);

        Interact(world, driver, world.Terminal!.InteractZone);
        Assert.True(world.TerminalActivated);
        Assert.True(world.ExitUnlocked);
        Assert.True(world.Exit!.Unlocked);
        Assert.Single(Driver.Drain(world), e => e.Kind == WorldEventKind.TerminalActivated);

        // Using it again yields no second activation event.
        Interact(world, driver, world.Terminal.InteractZone);
        var events = Driver.Drain(world);
        Assert.DoesNotContain(events, e => e.Kind == WorldEventKind.TerminalActivated);
        Assert.Contains(events, e => e.Kind == WorldEventKind.TerminalAlreadyActive);
    }

    [Fact]
    public void HoldingInteractDoesNotRepeatTheInteraction()
    {
        var world = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        var driver = new Driver();
        world.Player.Teleport(new Point(world.Terminal!.CenterX, 164));
        driver.Step(world, steps: 2);

        driver.Press(world, InputButtons.Interact, holdSteps: 120);

        Assert.Single(Driver.Drain(world), e => e.Kind == WorldEventKind.TerminalDenied);
    }

    [Fact]
    public void ExitStaysLockedUntilTheTerminalIsActivated()
    {
        var world = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        var driver = new Driver();

        world.Player.Teleport(new Point(world.Exit!.CenterX, 164));
        driver.Seconds(world, 0.5);

        Assert.False(world.Exit.Entered);
        var events = Driver.Drain(world);
        Assert.Single(events, e => e.Kind == WorldEventKind.ExitLocked);
        Assert.DoesNotContain(events, e => e.Kind == WorldEventKind.ExitReached);
    }

    [Fact]
    public void CheckpointActivatesOnceAndKeepsProgressOnRestore()
    {
        var world = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        var driver = new Driver();
        CollectAllTokens(world, driver);
        Interact(world, driver, world.Terminal!.InteractZone);

        world.Player.Teleport(new Point(world.Checkpoint!.CenterX, 164));
        driver.Step(world, steps: 3);
        Assert.True(world.CheckpointActivated);
        driver.Seconds(world, 1.0);
        Assert.Single(Driver.Drain(world), e => e.Kind == WorldEventKind.CheckpointActivated);

        var progress = new ProgressSnapshot(
            Score: 1234,
            CollectedIds: world.Collectibles.Where(c => c.Collected).Select(c => c.Id).ToHashSet(),
            CoffeeConsumed: 0,
            TerminalActivated: true,
            CheckpointActivated: true,
            InBossArena: false,
            CheckpointId: world.ActiveCheckpoint!.Id);

        var restored = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        restored.ApplyProgress(progress);

        Assert.Equal(2, restored.TokensCollected);
        Assert.All(restored.Collectibles.Where(c => c.Kind == CollectibleKind.Token), c => Assert.True(c.Collected));
        Assert.True(restored.TerminalActivated);
        Assert.True(restored.ExitUnlocked);
        Assert.True(restored.CheckpointActivated);
        Assert.Equal(restored.Checkpoint!.RespawnPoint.X, restored.Player.Feet.X, 0.01);
        Assert.Equal(restored.Player.MaxHealth, restored.Player.Health);

        // No duplicate rewards: stepping on the checkpoint again raises nothing.
        var restoredDriver = new Driver();
        restoredDriver.Seconds(restored, 1.0);
        Assert.DoesNotContain(Driver.Drain(restored), e => e.Kind is WorldEventKind.CheckpointActivated or WorldEventKind.TokenCollected or WorldEventKind.TerminalActivated);
    }

    [Fact]
    public void TheLatestCheckpointBecomesTheRespawnPointAndEarlierOnesStayLit()
    {
        var world = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        var driver = new Driver();
        Assert.Equal(2, world.Checkpoints.Count);
        var first = world.Checkpoints[0];
        var last = world.Checkpoints[1];

        world.Player.Teleport(new Point(first.CenterX, 164));
        driver.Step(world, steps: 3);
        Assert.Same(first, world.ActiveCheckpoint);

        var progress = new ProgressSnapshot(0, new HashSet<string>(), 0, false, true, false, first.Id);
        var restored = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        restored.ApplyProgress(progress);
        Assert.Equal(first.RespawnPoint.X, restored.Player.Feet.X, 0.01);
        Assert.True(restored.Checkpoints[0].Activated);
        Assert.False(restored.Checkpoints[1].Activated);

        world.Player.Teleport(new Point(last.CenterX, 164));
        driver.Step(world, steps: 3);
        Assert.Same(last, world.ActiveCheckpoint);
        Assert.True(first.Activated && last.Activated);
        Assert.Equal(2, Driver.Drain(world).Count(e => e.Kind == WorldEventKind.CheckpointActivated));
    }

    [Fact]
    public void StandingAtTheTerminalShowsAPressEPromptUntilItIsUsed()
    {
        var world = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        var driver = new Driver();
        var snapshot = new FrameSnapshot();

        world.Render(snapshot, includeStatic: false);
        Assert.DoesNotContain(snapshot.Labels, l => l.Text == "PRESS E");

        world.Player.Teleport(new Point(world.Terminal!.CenterX, 164));
        driver.Step(world);
        snapshot.Clear();
        world.Render(snapshot, includeStatic: false);
        Assert.Contains(snapshot.Labels, l => l.Text == "PRESS E");

        CollectAllTokens(world, driver);
        Interact(world, driver, world.Terminal.InteractZone);
        Assert.True(world.TerminalActivated);
        snapshot.Clear();
        world.Render(snapshot, includeStatic: false);
        Assert.DoesNotContain(snapshot.Labels, l => l.Text == "PRESS E");
    }

    [Fact]
    public void ReachingAnUnlockedExitRaisesExitReachedOnce()
    {
        var world = TestContent.NewWorld(ObjectiveLevel, tokensRequired: 2);
        var driver = new Driver();
        CollectAllTokens(world, driver);
        Interact(world, driver, world.Terminal!.InteractZone);
        Driver.Drain(world);

        world.Player.Teleport(new Point(world.Exit!.CenterX, 164));
        driver.Seconds(world, 0.5);

        Assert.Single(Driver.Drain(world), e => e.Kind == WorldEventKind.ExitReached);
    }
}
