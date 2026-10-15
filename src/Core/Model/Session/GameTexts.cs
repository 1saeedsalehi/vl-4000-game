namespace Videoland4000.Model.Session;

/// <summary>All the funny engineering messages, in one place.</summary>
public static class GameTexts
{
    public static GameMessage Death() => GameMessage.Banner(2.0,
        "STREAM INTERRUPTED",
        "Root cause: Unknown",
        "Logs: Insufficient",
        "Blame: DNS");

    public static GameMessage DeploymentSuccessful() => GameMessage.Banner(3.0,
        "DEPLOYMENT SUCCESSFUL",
        "No rollback required.",
        "For once.");

    public static GameMessage CheckpointSaved() => GameMessage.Toast(2.5,
        "CHECKPOINT SAVED",
        "Your changes have been committed.");

    public static GameMessage TokenExpired() => GameMessage.Toast(2.2,
        "401 UNAUTHORIZED",
        "Your invitation to prime time has expired.");

    public static GameMessage PrimeTimeWarning() => GameMessage.Alert(3.2,
        "WARNING",
        "PRIME TIME TRAFFIC DETECTED.",
        "COFFEE LEVELS CRITICAL.");

    public static GameMessage Authenticating() => GameMessage.Banner(2.6,
        "AUTHENTICATING...",
        "TOKEN VALID.",
        "SESSION RESTORED.");

    public static GameMessage RootCause() => GameMessage.Toast(2.6,
        "ROOT CAUSE IDENTIFIED",
        "It was not DNS.",
        "This time.");

    public static GameMessage AccessDenied(int tokens, int required) => GameMessage.Toast(2.4,
        "ACCESS DENIED.",
        $"MISSING VALID TOKENS ({tokens:00}/{required:00}). Collect more and come back.");

    public static GameMessage AlreadyAuthenticated() => GameMessage.Toast(1.8,
        "SESSION ALREADY RESTORED.",
        "Please proceed to the exit.");

    public static GameMessage ExitLocked() => GameMessage.Toast(3.0,
        "EXIT LOCKED (401)",
        "Go back to the terminal with the red screen and press E.");

    public static GameMessage ExitOpen() => GameMessage.Toast(2.4,
        "EXIT UNLOCKED",
        "Head right. Mind the gaps.");

    public static GameMessage ConnectionLost() => GameMessage.Toast(2.0,
        "CONNECTION LOST",
        "Reconnecting at last known position...");

    public static GameMessage CacheHit() => GameMessage.Toast(2.2,
        "CACHE HIT!",
        "Enemies slowed for 8 seconds.");

    public static GameMessage BreakerTripped(int frozen) => GameMessage.Toast(2.2,
        "CIRCUIT BREAKER TRIPPED",
        frozen > 0 ? $"{frozen} nearby service(s) frozen." : "Nothing nearby to freeze. Still satisfying.");

    public static GameMessage RollbackArmed() => GameMessage.Toast(2.4,
        "ROLLBACK ARMED",
        "Your next incident will be reverted.");

    public static GameMessage RollbackDeployed() => GameMessage.Toast(2.4,
        "ROLLBACK DEPLOYED",
        "Incident reverted. Nobody noticed.");

    public static GameMessage BossIntro() => GameMessage.Banner(2.6,
        "INCIDENT: THE INFINITE LOADING SPINNER",
        "Read the diagnostic.",
        "Repair the RIGHT service. Then jump into it.");

    public static GameMessage RepairCorrect(string service) => GameMessage.Toast(2.4,
        $"ROOT CAUSE FOUND: {service} SERVICE",
        "Spinner stunned. Jump into it!");

    public static GameMessage RepairWrong(string service) => GameMessage.Toast(2.4,
        $"RESTARTED {service} SERVICE.",
        "Nothing changed. Classic.");

    public static GameMessage RepairUnavailable() => GameMessage.Toast(1.6,
        "SERVICE RESTART IN PROGRESS...",
        "Have you tried waiting?");

    public static GameMessage StunExpired() => GameMessage.Toast(2.0,
        "STUN EXPIRED",
        "The spinner is spinning again.");

    public static GameMessage TrafficSpike() => GameMessage.Alert(2.4,
        "TRAFFIC SPIKE",
        "Viewers: a lot. Patience: none.");

    public static GameMessage InfiniteLoop() => GameMessage.Alert(2.6,
        "INFINITE LOOP DETECTED",
        "while (true) { buffer(); }");

    public static GameMessage TrafficWave() => GameMessage.Toast(1.4,
        "TRAFFIC WAVE INCOMING",
        "Jump!");

    public static GameMessage ArenaShifted() => GameMessage.Toast(2.0,
        "INFRASTRUCTURE DRIFT",
        "The platforms moved. Nobody approved this.");

    public static GameMessage BossHit(int remaining) => GameMessage.Toast(1.6,
        "DIRECT HIT",
        remaining > 0 ? $"Spinner integrity: {remaining}/{BossController.MaxHealth}" : "Spinner integrity: 0. Finally.");

    public static GameMessage LevelStart() => GameMessage.Toast(3.0,
        "FRIDAY 19:58. PRIME TIME IN 2 MIN.",
        "Collect tokens. Avoid the expired ones.");
}
