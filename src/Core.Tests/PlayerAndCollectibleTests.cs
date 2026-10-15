using Videoland4000.Core.Tests.Support;

namespace Videoland4000.Core.Tests;

public class PlayerAndCollectibleTests
{
    private const string FloorWithItems = """
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        ....................
        .P.T.C.B.K.R........
        ####################
        """;

    private static void TeleportOnto(World world, Collectible item)
    {
        world.Player.Teleport(new Point(item.Origin.X, item.Origin.Y + 6));
    }

    [Fact]
    public void TokenIsCollectedOnceAndRaisesOneEvent()
    {
        var world = TestContent.NewWorld(FloorWithItems);
        var driver = new Driver();
        var token = world.Collectibles.Single(c => c.Kind == CollectibleKind.Token);

        TeleportOnto(world, token);
        driver.Step(world, steps: 5);

        Assert.True(token.Collected);
        Assert.Equal(1, world.TokensCollected);
        var events = Driver.Drain(world);
        Assert.Single(events, e => e.Kind == WorldEventKind.TokenCollected);

        // Standing on the same spot never collects it again.
        driver.Seconds(world, 1.0);
        Assert.Equal(1, world.TokensCollected);
        Assert.DoesNotContain(Driver.Drain(world), e => e.Kind == WorldEventKind.TokenCollected);
    }

    [Fact]
    public void CoffeeHealsButNeverAboveMaximum()
    {
        var world = TestContent.NewWorld(FloorWithItems);
        var driver = new Driver();
        var coffee = world.Collectibles.Single(c => c.Kind == CollectibleKind.Coffee);

        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
        TeleportOnto(world, coffee);
        driver.Step(world, steps: 3);

        Assert.True(coffee.Collected);
        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
        Assert.Equal(1, world.CoffeeConsumed);
    }

    [Fact]
    public void CoffeeRestoresOneHealthWhenHurt()
    {
        var world = TestContent.NewWorld(FloorWithItems);
        var driver = new Driver();
        world.DamagePlayer(0, knockback: false);
        Assert.Equal(2, world.Player.Health);

        TeleportOnto(world, world.Collectibles.Single(c => c.Kind == CollectibleKind.Coffee));
        driver.Step(world, steps: 3);

        Assert.Equal(3, world.Player.Health);
    }

    [Fact]
    public void DamageGrantsInvulnerabilityAndIsNotRepeatedEveryFrame()
    {
        var world = TestContent.NewWorld(FloorWithItems);

        Assert.True(world.DamagePlayer(0));
        Assert.Equal(2, world.Player.Health);
        Assert.True(world.Player.IsInvulnerable);

        Assert.False(world.DamagePlayer(0));
        Assert.Equal(2, world.Player.Health);

        var driver = new Driver();
        driver.Seconds(world, PhysicsConfig.InvulnerabilityTime + 0.1);
        Assert.False(world.Player.IsInvulnerable);
        Assert.True(world.DamagePlayer(0));
        Assert.Equal(1, world.Player.Health);
    }

    [Fact]
    public void HealthReachingZeroKillsThePlayerOnce()
    {
        var world = TestContent.NewWorld(FloorWithItems);
        var driver = new Driver();

        for (var i = 0; i < 3; i++)
        {
            Assert.True(world.DamagePlayer(0, knockback: false));
            driver.Seconds(world, PhysicsConfig.InvulnerabilityTime + 0.1);
        }

        Assert.True(world.Player.Dead);
        Assert.Equal(0, world.Player.Health);
        var events = Driver.Drain(world);
        Assert.Single(events, e => e.Kind == WorldEventKind.PlayerDied);
        Assert.Equal(3, events.Count(e => e.Kind == WorldEventKind.PlayerDamaged));

        Assert.False(world.DamagePlayer(0));
        Assert.Equal(0, world.Player.Health);
    }

    [Fact]
    public void FallingIntoAPitCostsOneHealthAndRespawnsOnSafeGround()
    {
        var world = TestContent.NewWorld("""
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
            #####...............
            """);
        var driver = new Driver();
        driver.Seconds(world, 0.2);

        // Run off the ledge and let go once we have fallen.
        for (var i = 0; i < 120 * 4 && world.Player.Health == 3; i++)
        {
            driver.Step(world, InputButtons.Right);
        }

        driver.Seconds(world, 0.3);

        Assert.Equal(2, world.Player.Health);
        Assert.True(world.Player.Bounds.Right <= 5 * 16 + 0.5, $"respawned on the floor segment (right edge {world.Player.Bounds.Right})");
        Assert.True(world.Player.Grounded);
        Assert.Equal(164, world.Player.Bounds.Bottom, 1);
        Assert.True(world.Player.IsInvulnerable, "a grace period follows the respawn");
        Assert.Contains(Driver.Drain(world), e => e.Kind == WorldEventKind.PlayerFell);
    }

    [Fact]
    public void CacheBoostSlowsEnemiesAndExpires()
    {
        var world = TestContent.NewWorld("""
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ......G.............
            .P.B................
            ####################
            """);
        var driver = new Driver();
        var bug = world.Enemies.Single();

        var before = bug.CenterX;
        driver.Seconds(world, 0.5);
        var normalDistance = Math.Abs(bug.CenterX - before);

        TeleportOnto(world, world.Collectibles.Single());
        driver.Step(world, steps: 2);
        Assert.True(world.CacheBoostTimer > 7);

        before = bug.CenterX;
        driver.Seconds(world, 0.5);
        var slowDistance = Math.Abs(bug.CenterX - before);
        Assert.True(slowDistance < normalDistance * 0.6, $"slowed {slowDistance} vs normal {normalDistance}");

        driver.Seconds(world, 8.0);
        Assert.Equal(0, world.CacheBoostTimer);
    }

    [Fact]
    public void CircuitBreakerFreezesNearbyEnemiesOnly()
    {
        var world = TestContent.NewWorld("""
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ....G.............G.
            .P.K................
            ####################
            """);
        var driver = new Driver();
        var near = world.Enemies.OrderBy(e => e.CenterX).First();
        var far = world.Enemies.OrderBy(e => e.CenterX).Last();

        TeleportOnto(world, world.Collectibles.Single());
        driver.Step(world, steps: 2);

        Assert.True(near.Frozen);
        Assert.False(far.Frozen);
        var frozenX = near.CenterX;
        driver.Seconds(world, 1.0);
        Assert.Equal(frozenX, near.CenterX, 0.01);

        driver.Seconds(world, 3.5);
        Assert.False(near.Frozen);
    }

    [Fact]
    public void RollbackAbsorbsTheNextHitAndRestoresASafePosition()
    {
        var world = TestContent.NewWorld(FloorWithItems);
        var driver = new Driver();
        driver.Seconds(world, 0.3);

        TeleportOnto(world, world.Collectibles.Single(c => c.Kind == CollectibleKind.Rollback));
        driver.Step(world, steps: 2);
        Assert.True(world.Player.RollbackArmed);

        // walk for a while so the safe-position history differs from the current spot
        driver.Seconds(world, 2.5, InputButtons.Right);
        var hitPosition = world.Player.Feet;

        Assert.False(world.DamagePlayer(hitPosition.X + 5));

        Assert.Equal(3, world.Player.Health);
        Assert.False(world.Player.RollbackArmed);
        Assert.True(world.Player.IsInvulnerable);
        Assert.True(world.Player.Feet.X < hitPosition.X - 20, "moved back to an earlier safe position");
        Assert.DoesNotContain(world.Level.Solids, s => s.Overlaps(world.Player.Bounds));
        Assert.Contains(Driver.Drain(world), e => e.Kind == WorldEventKind.RollbackDeployed);
    }

    [Fact]
    public void HazardTilesDamageThePlayer()
    {
        var world = TestContent.NewWorld("""
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            .P..^...............
            ####################
            """);
        var driver = new Driver();

        driver.Seconds(world, 1.5, InputButtons.Right);

        Assert.True(world.Player.Health < 3);
    }
}
