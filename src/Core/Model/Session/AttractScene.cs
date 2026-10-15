namespace Videoland4000.Model.Session;

/// <summary>Decorative title-screen scene rendered behind the main menu.</summary>
public sealed class AttractScene
{
    private const int T = PhysicsConfig.TileSize;
    private double _time;

    public void Update(double dt) => _time += dt;

    public void Render(FrameSnapshot snapshot, bool includeStatic)
    {
        var floorY = PhysicsConfig.ViewportHeight - T;

        if (includeStatic)
        {
            var level = new LevelSnapshot { Width = PhysicsConfig.ViewportWidth, Height = PhysicsConfig.ViewportHeight, Theme = "title" };
            for (var col = 0; col < PhysicsConfig.ViewportWidth / T; col++)
            {
                level.Tiles.Add(new TileState { Frame = Frames.Tiles.FloorTop, X = col * T, Y = floorY });
            }

            foreach (var col in new[] { 1, 2, 17, 18 })
            {
                level.Tiles.Add(new TileState { Frame = Frames.Tiles.RackTop, X = col * T, Y = floorY - T * 2 });
                level.Tiles.Add(new TileState { Frame = Frames.Tiles.Rack, X = col * T, Y = floorY - T });
            }

            level.Tiles.Add(new TileState { Frame = Frames.Deco.Server, X = 4 * T, Y = floorY - T * 2, Depth = RenderDepth.Decor });
            level.Tiles.Add(new TileState { Frame = Frames.Deco.CoffeeMachine, X = 15 * T, Y = floorY - T * 2, Depth = RenderDepth.Decor });
            level.Tiles.Add(new TileState { Frame = Frames.Deco.Monitor, X = 1 * T, Y = floorY - T * 3, Depth = RenderDepth.Decor });
            level.Tiles.Add(new TileState { Frame = Frames.Deco.Poster, X = 18 * T, Y = floorY - T * 3, Depth = RenderDepth.Decor });
            snapshot.Level = level;
        }

        World.Sprite(snapshot, "attract-player", Frames.Player.Idle((int)(_time * 2)), 160, floorY, RenderDepth.Player);

        for (var i = 0; i < 3; i++)
        {
            var x = 160 + Math.Sin(_time * 0.7 + i * 2.1) * (70 + i * 25);
            var y = 70 + Math.Sin(_time * 1.6 + i) * 10 + i * 18;
            World.Sprite(snapshot, $"attract-spinner{i}", Frames.Spinner.Spin((int)(_time * 12 + i * 3)), x, y, RenderDepth.Enemies);
        }

        var bugX = 160 + Math.Sin(_time * 0.5) * 90;
        var bugFacingRight = Math.Cos(_time * 0.5) > 0;
        World.Sprite(snapshot, "attract-bug", Frames.Bug.Walk((int)(_time * 8)), bugX, floorY, RenderDepth.Enemies, flipX: !bugFacingRight);

        World.Sprite(snapshot, "attract-token", Frames.Pickups.Token((int)(_time * 8)), 200, floorY - 8 + Math.Sin(_time * 4) * 2, RenderDepth.Collectibles);

        snapshot.Camera = new CameraState(0, 0, 0);
    }
}
