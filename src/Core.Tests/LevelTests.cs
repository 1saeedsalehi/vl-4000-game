using Videoland4000.Core.Tests.Support;

namespace Videoland4000.Core.Tests;

public class LevelTests
{
    [Fact]
    public void RealLevelParsesWithAllObjectives()
    {
        var level = LevelParser.Parse(TestContent.Level());

        Assert.True(level.Columns > 40);
        Assert.Equal(11, level.Rows);
        Assert.Equal(PhysicsConfig.ViewportHeight, level.Height);
        Assert.True(level.PlayerSpawn.X > 0);
        Assert.Contains(level.Spawns, s => s.Kind == SpawnKind.Terminal);
        Assert.Contains(level.Spawns, s => s.Kind == SpawnKind.Checkpoint);
        Assert.Contains(level.Spawns, s => s.Kind == SpawnKind.Exit);
        Assert.True(level.Spawns.Count(s => s.Kind == SpawnKind.Token) >= level.TokensRequired, "enough tokens for the objective");
        Assert.True(level.Spawns.Count(s => s.Kind == SpawnKind.Spinner) >= 1);
        Assert.True(level.Spawns.Count(s => s.Kind == SpawnKind.ExpiredToken) >= 1);
        Assert.True(level.Spawns.Count(s => s.Kind == SpawnKind.Bug) >= 1);
        Assert.Contains(level.Spawns, s => s.Kind == SpawnKind.Coffee);
        Assert.Contains(level.Spawns, s => s.Kind == SpawnKind.CacheBoost);
        Assert.Contains(level.Spawns, s => s.Kind == SpawnKind.CircuitBreaker);
        Assert.Contains(level.Spawns, s => s.Kind == SpawnKind.Rollback);
    }

    [Fact]
    public void RealLevelObjectivesAreReachableWithTheJumpPhysics()
    {
        var level = LevelParser.Parse(TestContent.Level());
        var world = TestContent.NewWorld(TestContent.Level());
        var reachable = Reachability.ReachableStandPoints(level);

        foreach (var collectible in world.Collectibles)
        {
            Assert.True(Reachability.CanTouch(collectible.Bounds, reachable), $"{collectible.Kind} {collectible.Id} is unreachable");
        }

        Assert.True(Reachability.CanTouch(world.Terminal!.InteractZone, reachable), "terminal unreachable");
        Assert.All(world.Checkpoints, c => Assert.True(Reachability.CanTouch(c.Bounds, reachable), $"checkpoint {c.Id} unreachable"));
        Assert.True(Reachability.CanTouch(world.Exit!.Bounds, reachable), "exit unreachable");
    }

    [Fact]
    public void LastCheckpointSitsBetweenTheTerminalAndTheExit()
    {
        var level = LevelParser.Parse(TestContent.Level());
        var checkpoints = level.Spawns.Where(s => s.Kind == SpawnKind.Checkpoint).OrderBy(s => s.Position.X).ToList();
        var exit = level.Spawns.Single(s => s.Kind == SpawnKind.Exit);
        var terminal = level.Spawns.Single(s => s.Kind == SpawnKind.Terminal);

        Assert.True(checkpoints.Count >= 2, "a mid-level checkpoint and one before the exit");
        Assert.True(terminal.Position.X < checkpoints[^1].Position.X);
        Assert.True(checkpoints[^1].Position.X < exit.Position.X);
        Assert.True(checkpoints[0].Position.X < terminal.Position.X, "the first checkpoint comes before the terminal");
    }

    [Fact]
    public void WalkingAlongTheFloorNeverTouchesAFloatingEnemy()
    {
        // Floating enemies may guard platforms, but simply walking on the floor must always be safe:
        // their lowest point (including the bob) stays above a standing player's head.
        var level = LevelParser.Parse(TestContent.Level());
        var world = TestContent.NewWorld(TestContent.Level());
        var floorTop = level.Height - PhysicsConfig.TileSize;
        var headWhenStanding = floorTop - PhysicsConfig.PlayerHeight;

        foreach (var enemy in world.Enemies.Where(e => e.Kind is EnemyKind.Spinner or EnemyKind.ExpiredToken))
        {
            var lowest = enemy.Kind == EnemyKind.Spinner ? enemy.Bounds.Bottom + 3 : enemy.Box.Bottom;
            Assert.True(lowest <= headWhenStanding,
                $"{enemy.Kind} at ({enemy.Origin.X},{enemy.Origin.Y}) reaches y={lowest}, below a standing player's head ({headWhenStanding})");
        }
    }

    [Fact]
    public void ArenaParsesWithBossAndThreeDistinctRepairTerminals()
    {
        var arena = LevelParser.Parse(TestContent.Arena());

        Assert.True(arena.HasBoss);
        Assert.NotNull(arena.Alternate);
        Assert.Equal(PhysicsConfig.ViewportWidth, arena.Width);

        var services = arena.Spawns
            .Where(s => s.Kind is SpawnKind.RepairIdentity or SpawnKind.RepairContent or SpawnKind.RepairStreaming)
            .Select(s => s.Kind)
            .ToList();
        Assert.Equal(3, services.Count);
        Assert.Equal(3, services.Distinct().Count());
    }

    [Fact]
    public void ArenaLayoutsShareSolidsSoNothingCanBeTrapped()
    {
        var arena = LevelParser.Parse(TestContent.Arena());
        var primary = arena.Solids.OrderBy(r => r.Y).ThenBy(r => r.X).ToList();
        var alternate = arena.Alternate!.Solids.OrderBy(r => r.Y).ThenBy(r => r.X).ToList();

        Assert.Equal(primary, alternate);
        Assert.NotEqual(0, arena.OneWayPlatforms.Count);
        Assert.NotEqual(arena.OneWayPlatforms, arena.Alternate.OneWayPlatforms);
    }

    [Fact]
    public void ArenaTerminalsAndPlatformsAreReachableInBothLayouts()
    {
        var arena = LevelParser.Parse(TestContent.Arena());
        foreach (var layout in new[] { arena, arena.Alternate! })
        {
            var world = TestContent.NewWorld(TestContent.Arena());
            var reachable = Reachability.ReachableStandPoints(layout);
            foreach (var terminal in world.RepairTerminals)
            {
                Assert.True(Reachability.CanTouch(terminal.InteractZone, reachable), $"{terminal.Service} unreachable");
            }

            foreach (var platform in layout.OneWayPlatforms)
            {
                Assert.Contains(reachable, p => Math.Abs(p.Y - platform.Top) < 0.01 && p.X >= platform.Left && p.X <= platform.Right);
            }
        }
    }

    [Fact]
    public void ParserMergesHorizontalRunsAndAddsBoundaryWalls()
    {
        var level = LevelParser.Parse(TestContent.Synthetic(TestContent.FlatFloor));

        // one merged floor rect + two boundary walls
        Assert.Equal(3, level.Solids.Count);
        Assert.Contains(level.Solids, s => s.Width == 320 && Math.Abs(s.Top - 164) < 0.01);
        Assert.Contains(level.Solids, s => s.Right <= 0);
        Assert.Contains(level.Solids, s => s.Left >= 320);
    }

    [Fact]
    public void ParserRejectsUnknownCharactersAndMissingSpawn()
    {
        Assert.Throws<InvalidDataException>(() => LevelParser.Parse(TestContent.Synthetic("P?\n##")));
        Assert.Throws<InvalidDataException>(() => LevelParser.Parse(TestContent.Synthetic("..\n##")));
    }

    [Fact]
    public void FloorTopTilesAreUsedOnlyWhereNothingSolidIsAbove()
    {
        var level = LevelParser.Parse(TestContent.Synthetic("""
            P...
            ####
            ####
            """));

        Assert.Equal(4, level.Tiles.Count(t => t.Frame == Frames.Tiles.FloorTop));
        Assert.Equal(4, level.Tiles.Count(t => t.Frame == Frames.Tiles.Floor));
    }
}
