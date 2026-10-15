namespace Videoland4000.Rendering;

/// <summary>
/// Catalogue of atlas frame names. The atlas is generated from original
/// text pixel-maps by tools/atlas/build_atlas.py; names here must match
/// the generated sprites.json exactly (see docs/ARCHITECTURE.md). Animation
/// frame names are pre-built so the per-frame hot path never allocates strings.
/// </summary>
public static class Frames
{
    public const string AtlasKey = "default";

    private static string[] Sequence(string prefix, int count)
    {
        var names = new string[count];
        for (var i = 0; i < count; i++)
        {
            names[i] = $"{prefix}/{i}";
        }

        return names;
    }

    private static string Cycle(string[] names, int index) => names[(index % names.Length + names.Length) % names.Length];

    public static class Player
    {
        public const int Width = 16, Height = 20;
        private static readonly string[] IdleFrames = Sequence("player/idle", 2);
        private static readonly string[] RunFrames = Sequence("player/run", 4);
        private static readonly string[] CheerFrames = Sequence("player/cheer", 2);
        public static string Idle(int i) => Cycle(IdleFrames, i);
        public static string Run(int i) => Cycle(RunFrames, i);
        public const string Jump = "player/jump/0";
        public const string Fall = "player/fall/0";
        public const string Hurt = "player/hurt/0";
        public const string Dead = "player/dead/0";
        public static string Cheer(int i) => Cycle(CheerFrames, i);
        public const string Interact = "player/interact/0";
    }

    public static class Spinner
    {
        private static readonly string[] SpinFrames = Sequence("spinner/spin", 8);
        public static string Spin(int i) => Cycle(SpinFrames, i);
    }

    public static class Expired
    {
        private static readonly string[] FloatFrames = Sequence("expired/float", 2);
        public static string Float(int i) => Cycle(FloatFrames, i);
        public const string Warn = "expired/warn/0";
    }

    public static class Bug
    {
        private static readonly string[] WalkFrames = Sequence("bug/walk", 2);
        public static string Walk(int i) => Cycle(WalkFrames, i);
        public const string Squash = "bug/squash/0";
    }

    public static class Pickups
    {
        private static readonly string[] TokenFrames = Sequence("token/spin", 4);
        private static readonly string[] CoffeeFrames = Sequence("coffee/idle", 2);
        private static readonly string[] CacheFrames = Sequence("cache/idle", 2);
        private static readonly string[] BreakerFrames = Sequence("breaker/idle", 2);
        private static readonly string[] RollbackFrames = Sequence("rollback/idle", 2);
        public static string Token(int i) => Cycle(TokenFrames, i);
        public static string Coffee(int i) => Cycle(CoffeeFrames, i);
        public static string Cache(int i) => Cycle(CacheFrames, i);
        public static string Breaker(int i) => Cycle(BreakerFrames, i);
        public static string Rollback(int i) => Cycle(RollbackFrames, i);
    }

    public static class Effects
    {
        private static readonly string[] PopFrames = Sequence("pop", 3);
        private static readonly string[] SparkleFrames = Sequence("sparkle", 3);
        private static readonly string[] WaveFrames = Sequence("wave", 2);
        private static readonly string[] WarnFrames = Sequence("warn", 2);
        private static readonly string[] BulletFrames = Sequence("bullet", 4);
        public static string Pop(int i) => PopFrames[Math.Clamp(i, 0, 2)];
        public static string Sparkle(int i) => SparkleFrames[Math.Clamp(i, 0, 2)];
        public static string Wave(int i) => Cycle(WaveFrames, i);
        public static string Warn(int i) => Cycle(WarnFrames, i);
        public static string Bullet(int i) => Cycle(BulletFrames, i);
    }

    public static class Terminal
    {
        private static readonly string[] LockedFrames = Sequence("terminal/locked", 2);
        private static readonly string[] ActiveFrames = Sequence("terminal/active", 2);
        public static string Locked(int i) => Cycle(LockedFrames, i);
        public static string Active(int i) => Cycle(ActiveFrames, i);
        public const string Denied = "terminal/denied/0";
    }

    public static class Repair
    {
        private static readonly string[] IdleFrames = Sequence("repair/idle", 2);
        public static string Idle(int i) => Cycle(IdleFrames, i);
        public const string Ok = "repair/ok/0";
        public const string Bad = "repair/bad/0";
        public const string Cooldown = "repair/cooldown/0";
    }

    public static class Icon
    {
        public const string Identity = "icon/identity";
        public const string Content = "icon/content";
        public const string Streaming = "icon/streaming";
    }

    public static class Checkpoint
    {
        private static readonly string[] OnFrames = Sequence("checkpoint/on", 2);
        public const string Off = "checkpoint/off/0";
        public static string On(int i) => Cycle(OnFrames, i);
    }

    public static class Exit
    {
        private static readonly string[] LockedFrames = Sequence("exit/locked", 2);
        private static readonly string[] OpenFrames = Sequence("exit/open", 2);
        public static string Locked(int i) => Cycle(LockedFrames, i);
        public static string Open(int i) => Cycle(OpenFrames, i);
    }

    public static class Boss
    {
        private static readonly string[] SpinFrames = Sequence("boss/spin", 8);
        private static readonly string[] StunFrames = Sequence("boss/stun", 2);
        private static readonly string[] ChargeFrames = Sequence("boss/charge", 2);
        public static string Spin(int i) => Cycle(SpinFrames, i);
        public static string Stun(int i) => Cycle(StunFrames, i);
        public const string Hit = "boss/hit/0";
        public static string Charge(int i) => Cycle(ChargeFrames, i);
        public const string Dead = "boss/dead/0";
    }

    public static class Tiles
    {
        public const string FloorTop = "tiles/floor_top";
        public const string Floor = "tiles/floor";
        public const string Platform = "tiles/platform";
        public const string Wall = "tiles/wall";
        public const string Spike = "tiles/spike";
        public const string RackTop = "tiles/rack_top";
        public const string Rack = "tiles/rack";
        public const string Cable = "tiles/cable";
        public const string Pillar = "tiles/pillar";
        public const string Glass = "tiles/glass";
    }

    public static class Deco
    {
        public const string Monitor = "deco/monitor";
        public const string Server = "deco/server";
        public const string Lamp = "deco/lamp";
        public const string Poster = "deco/poster";
        public const string CoffeeMachine = "deco/coffee_machine";
    }
}
