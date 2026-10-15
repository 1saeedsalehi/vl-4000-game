namespace Videoland4000.Infrastructure.Phaser;

/// <summary>Names of the JavaScript functions in wwwroot/js (see docs/ARCHITECTURE.md §3).</summary>
public static class PhaserConstants
{
    public static class Functions
    {
        public const string StartPhaser = "startPhaser";
        public const string DestroyPhaser = "destroyPhaser";
        public const string SetGamePaused = "setGamePaused";
        public const string SetSoundEnabled = "setSoundEnabled";
        public const string SetReducedMotion = "setReducedMotion";
        public const string PlaySound = "playSound";
        public const string CaptureKeyDown = "captureKeyDown";
        public const string ReleaseKeyDown = "releaseKeyDown";
        public const string PrefersReducedMotion = "prefersReducedMotion";
        public const string RegisterDebug = "vl4000RegisterDebug";
    }
}
