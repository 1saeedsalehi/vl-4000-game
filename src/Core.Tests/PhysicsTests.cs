using Videoland4000.Core.Tests.Support;

namespace Videoland4000.Core.Tests;

public class PhysicsTests
{
    [Fact]
    public void PlayerFallsAndLandsOnTheFloor()
    {
        var world = TestContent.NewWorld("""
            ....................
            ....................
            P...................
            ....................
            ....................
            ####################
            """);
        var driver = new Driver();

        var startY = world.Player.Bounds.Bottom;
        driver.Seconds(world, 1.0);

        Assert.True(world.Player.Bounds.Bottom > startY);
        Assert.True(world.Player.Grounded);
        Assert.Equal(164, world.Player.Bounds.Bottom, 1);
        Assert.Equal(0, world.Player.VelY, 3);
    }

    [Fact]
    public void WalkingIntoAWallStopsAtTheWall()
    {
        var world = TestContent.NewWorld("""
            ....................
            ....................
            ....................
            P....W..............
            #####W##############
            """);
        var driver = new Driver();

        driver.Seconds(world, 2.0, InputButtons.Right);

        var wallLeft = 5 * 16;
        Assert.True(world.Player.Bounds.Right <= wallLeft + 0.01, $"right edge {world.Player.Bounds.Right} should stop at {wallLeft}");
        Assert.True(world.Player.Bounds.Right > wallLeft - 2, "should be touching the wall");
        Assert.Equal(0, world.Player.VelX, 3);
    }

    [Fact]
    public void JumpReachesAtLeastThreeTilesAndRespectsVariableHeight()
    {
        var world = TestContent.NewWorld(TestContent.FlatFloor);
        var driver = new Driver();
        driver.Seconds(world, 0.2);
        var floor = world.Player.Bounds.Bottom;

        // full jump: hold the button
        var highest = floor;
        driver.Press(world, InputButtons.Jump, holdSteps: 1);
        for (var i = 0; i < 120; i++)
        {
            driver.Step(world, InputButtons.Jump);
            highest = Math.Min(highest, world.Player.Bounds.Bottom);
        }

        var fullHeight = floor - highest;
        Assert.True(fullHeight >= 48, $"full jump height {fullHeight} should clear 3 tiles");
        Assert.True(fullHeight <= 64, $"full jump height {fullHeight} should stay below 4 tiles");

        // short jump: tap
        driver.Seconds(world, 0.5);
        highest = floor;
        driver.Press(world, InputButtons.Jump, holdSteps: 2);
        for (var i = 0; i < 120; i++)
        {
            driver.Step(world);
            highest = Math.Min(highest, world.Player.Bounds.Bottom);
        }

        var shortHeight = floor - highest;
        Assert.True(shortHeight < fullHeight * 0.7, $"tap jump {shortHeight} should be clearly lower than {fullHeight}");
        Assert.True(world.Player.Grounded);
    }

    [Fact]
    public void JumpInputIsBufferedShortlyBeforeLanding()
    {
        var world = TestContent.NewWorld(TestContent.FlatFloor);
        var driver = new Driver();
        driver.Seconds(world, 0.2);

        // Full jump (hold through the ascent), release, then press again just before landing.
        driver.Press(world, InputButtons.Jump, holdSteps: 1);
        driver.Seconds(world, 0.35, InputButtons.Jump);
        driver.Seconds(world, 0.25);
        Assert.False(world.Player.Grounded, "still airborne shortly before landing");

        driver.Press(world, InputButtons.Jump, holdSteps: 1);
        driver.Seconds(world, 0.25, InputButtons.Jump);
        Assert.False(world.Player.Grounded, "the buffered press triggered a second jump on landing");
        Assert.True(world.Player.VelY < 0 || world.Player.Bounds.Bottom < 160);
    }

    [Fact]
    public void JumpIsStillPossibleBrieflyAfterLeavingALedge()
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
            ..P.................
            #####...............
            """);
        var driver = new Driver();
        driver.Seconds(world, 0.2);

        // Walk off the ledge, then press jump within the coyote window.
        while (world.Player.Grounded)
        {
            driver.Step(world, InputButtons.Right);
        }

        driver.Step(world, InputButtons.Right, steps: 4); // ~0.033 s after leaving the edge
        driver.Press(world, InputButtons.Jump, holdSteps: 6, alsoHeld: InputButtons.Right);

        Assert.True(world.Player.VelY < -200, $"coyote jump should launch the player upwards (VelY={world.Player.VelY})");
    }

    [Fact]
    public void OneWayPlatformCanBePassedFromBelowAndLandedOnFromAbove()
    {
        var world = TestContent.NewWorld("""
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            ....................
            .===................
            ....................
            .P..................
            ####################
            """);
        var driver = new Driver();
        driver.Seconds(world, 0.2);

        var floor = world.Player.Bounds.Bottom;
        var platformTop = world.Level.OneWayPlatforms[0].Top; // y = 116 (3 tiles above the floor)
        Assert.Equal(floor - 48, platformTop, 1);

        // Jump: the head passes through the platform, then we land on top of it.
        driver.Press(world, InputButtons.Jump, holdSteps: 1);
        var passedThrough = false;
        for (var i = 0; i < 240; i++)
        {
            driver.Step(world, InputButtons.Jump);
            if (world.Player.Bounds.Top < platformTop && world.Player.Bounds.Bottom > platformTop)
            {
                passedThrough = true;
            }

            if (world.Player.Grounded && Math.Abs(world.Player.Bounds.Bottom - platformTop) < 0.01)
            {
                break;
            }
        }

        Assert.True(passedThrough, "body should pass through the one-way platform while rising");
        Assert.True(world.Player.Grounded);
        Assert.Equal(platformTop, world.Player.Bounds.Bottom, 2);
    }

    [Fact]
    public void SimulationIsFrameRateIndependent()
    {
        var a = TestContent.NewWorld(TestContent.FlatFloor);
        var b = TestContent.NewWorld(TestContent.FlatFloor);

        // 60 Hz vs 30 Hz frames through the fixed-step accumulator pattern used by the session.
        RunFrames(a, 1.0 / 60.0, 60);
        RunFrames(b, 1.0 / 30.0, 30);

        Assert.Equal(a.Player.Bounds.X, b.Player.Bounds.X, 0.01);
    }

    private static void RunFrames(World world, double frame, int frames)
    {
        double accumulator = 0;
        var previous = InputButtons.None;
        for (var i = 0; i < frames; i++)
        {
            accumulator += frame;
            while (accumulator >= PhysicsConfig.FixedStep - 1e-9)
            {
                accumulator -= PhysicsConfig.FixedStep;
                world.Step(PhysicsConfig.FixedStep, InputState.FromHeld(InputButtons.Right, previous));
                previous = InputButtons.Right;
            }
        }
    }

    [Fact]
    public void PlayerCannotLeaveTheLevelHorizontally()
    {
        var world = TestContent.NewWorld(TestContent.FlatFloor);
        var driver = new Driver();

        driver.Seconds(world, 3.0, InputButtons.Left);
        Assert.True(world.Player.Bounds.Left >= -0.01);

        driver.Seconds(world, 6.0, InputButtons.Right);
        Assert.True(world.Player.Bounds.Right <= world.Level.Width + 0.01);
    }
}
