namespace Videoland4000.Model.Simulation;

/// <summary>
/// Tunable platforming constants (pixels and seconds at 320x180 logical resolution).
/// Jump height ≈ v²/2g = 340²/2000 ≈ 58 px (3.6 tiles); air time ≈ 0.68 s → ≈ 68 px (4.25 tiles) at run speed.
/// Level design keeps gaps ≤ 2 tiles and ledges ≤ 3 tiles so no jump needs pixel-perfect timing.
/// </summary>
public static class PhysicsConfig
{
    public const double FixedStep = 1.0 / 120.0;
    public const double MaxFrameDelta = 0.1;

    public const double Gravity = 1000;
    public const double MaxFallSpeed = 300;
    public const double RunSpeed = 100;
    public const double GroundAcceleration = 1400;
    public const double AirAcceleration = 900;
    public const double GroundFriction = 1600;
    public const double JumpVelocity = 340;
    /// <summary>Releasing jump early multiplies upward velocity by this factor (variable jump height).</summary>
    public const double JumpCutFactor = 0.45;
    public const double CoyoteTime = 0.10;
    public const double JumpBufferTime = 0.12;
    public const double StompBounceVelocity = 210;

    public const double PlayerWidth = 10;
    public const double PlayerHeight = 16;

    public const int MaxHealth = 3;
    public const double InvulnerabilityTime = 2.0;
    public const double HurtTime = 0.3;
    public const double KnockbackX = 120;
    public const double KnockbackY = 170;
    public const double DeathDelay = 1.6;

    public const int TileSize = 16;
    public const int ViewportWidth = 320;
    public const int ViewportHeight = 180;
}
