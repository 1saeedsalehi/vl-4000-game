namespace Videoland4000.Core.Tests.Support;

/// <summary>Scripted playthroughs that exercise the real game flow through the session API.</summary>
public static class Playthrough
{
    /// <summary>From Playing: collect the required tokens, use the terminal, hit the checkpoint and enter the exit.</summary>
    public static void ReachBossArena(GameSession session)
    {
        var world = session.World!;
        foreach (var token in world.Collectibles.Where(c => c.Kind == CollectibleKind.Token).Take(world.TokensRequired))
        {
            world.Player.Teleport(new Point(token.Origin.X, token.Origin.Y + 6));
            SessionDriver.Frames(session, 2);
        }

        world.Player.Teleport(new Point(world.Terminal!.CenterX, 164));
        world.Player.InvulnerableTimer = 100;
        SessionDriver.Frames(session, 2);
        SessionDriver.Press(session, InputButtons.Interact);
        Assert.True(world.TerminalActivated);

        world.Player.Teleport(new Point(world.Checkpoint!.CenterX, 164));
        SessionDriver.Frames(session, 3);
        Assert.True(session.CheckpointAvailable);

        world.Player.Teleport(new Point(world.Exit!.CenterX, 164));
        SessionDriver.Frames(session, 3);
        Assert.Equal(GamePhase.BossEncounter, session.Phase);
    }

    /// <summary>From the boss encounter: defeat the boss with the intended repair-terminal mechanic.</summary>
    public static void DefeatBoss(GameSession session)
    {
        var arena = session.World!;
        var boss = arena.Boss!;
        SessionDriver.Seconds(session, BossController.IntroDuration + 0.2);
        arena.Player.InvulnerableTimer = 1000;

        for (var hit = 0; hit < BossController.MaxHealth; hit++)
        {
            for (var i = 0; i < 60 * 6 && boss.State != BossState.Attacking; i++)
            {
                session.Tick(SessionDriver.FrameSeconds, InputButtons.None);
            }

            Assert.Equal(BossState.Attacking, boss.State);
            var terminal = arena.RepairTerminals.Single(t => t.Service == boss.Diagnostic);
            arena.Player.Teleport(new Point(terminal.CenterX, arena.FloorTop));
            SessionDriver.Frames(session, 2);
            SessionDriver.Press(session, InputButtons.Interact);
            Assert.Equal(BossState.Stunned, boss.State);

            SessionDriver.Seconds(session, 0.6);
            arena.Player.Teleport(new Point(boss.Center.X, boss.Center.Y + 8));
            SessionDriver.Frames(session, 1);
            Assert.Equal(BossController.MaxHealth - hit - 1, boss.Health);
            SessionDriver.Seconds(session, BossController.HitReactDuration + 0.1);
        }

        SessionDriver.Seconds(session, BossController.DeathDuration + 0.5);
        Assert.Equal(GamePhase.Victory, session.Phase);
    }
}
