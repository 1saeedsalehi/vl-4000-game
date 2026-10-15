namespace Videoland4000.Model.Session;

/// <summary>
/// Top-level game state machine. Owns the current world (level or boss arena),
/// scoring, the checkpoint snapshot and the HUD state. All UI-visible
/// notifications are raised as synchronous C# events; the Blazor host forwards
/// them through the mediator.
///
/// This is a skeleton: gameplay wiring is added in GameSession.Gameplay.cs.
/// </summary>
public sealed partial class GameSession
{
    private readonly FrameSnapshot _snapshot = new();
    private GamePhase _phaseBeforePause = GamePhase.Playing;
    private InputButtons _previouslyHeld;
    private InputButtons _pendingPressed;
    private bool _suppressEdgesOnce;
    private HudState _hud = HudState.Empty;

    public GameContent Content { get; }
    public IRandomSource Random { get; }
    public GamePhase Phase { get; private set; } = GamePhase.Booting;
    public HudState Hud => _hud;
    public int HighScore { get; private set; }
    public VictoryStats? LastVictory { get; private set; }

    public event Action<GamePhase, GamePhase>? PhaseChanged;
    public event Action<HudState>? HudChanged;
    public event Action<GameMessage>? MessageShown;
    public event Action<VictoryStats>? VictoryAchieved;
    public event Action<int, int, bool>? GameEnded;
    public event Action<int>? HighScoreChanged;

    public GameSession(GameContent content, int highScore, IRandomSource? random = null)
    {
        Content = content;
        Random = random ?? new SeededRandom();
        HighScore = HighScoreValidator.Sanitize(highScore);
        PublishHud(HudState.Empty with { HighScore = HighScore, MaxHealth = content.Manifest.Spec.Player.MaxHealth, Health = content.Manifest.Spec.Player.MaxHealth });
    }

    // ----- Transitions ------------------------------------------------------

    public bool CompleteBoot()
    {
        if (Phase != GamePhase.Booting)
        {
            return false;
        }

        EnterMainMenu();
        return true;
    }

    public bool StartNewGame()
    {
        if (Phase != GamePhase.MainMenu)
        {
            return false;
        }

        BeginNewRun();
        return true;
    }

    public bool SkipIntro()
    {
        if (Phase != GamePhase.LevelIntro)
        {
            return false;
        }

        StartLevelPlay();
        return true;
    }

    public bool TogglePause()
    {
        switch (Phase)
        {
            case GamePhase.Playing:
            case GamePhase.BossEncounter:
                _phaseBeforePause = Phase;
                ChangePhase(GamePhase.Paused);
                return true;
            case GamePhase.Paused:
                ChangePhase(_phaseBeforePause);
                return true;
            default:
                return false;
        }
    }

    public bool Retry()
    {
        if (Phase != GamePhase.GameOver)
        {
            return false;
        }

        RetryFromCheckpointOrStart();
        return true;
    }

    public bool ReturnToMenu()
    {
        if (Phase is not (GamePhase.Paused or GamePhase.GameOver or GamePhase.Victory))
        {
            return false;
        }

        EnterMainMenu();
        return true;
    }

    // ----- Frame ------------------------------------------------------------

    /// <summary>Advances the simulation (when running) and returns the render snapshot.</summary>
    public FrameSnapshot Tick(double deltaSeconds, InputButtons held)
    {
        var pressedNow = held & ~_previouslyHeld;
        _previouslyHeld = held;

        if (_suppressEdgesOnce)
        {
            // The key that confirmed a menu (e.g. Space skipping the intro) must not also act in the game.
            pressedNow = InputButtons.None;
            _suppressEdgesOnce = false;
        }

        if (Phase is GamePhase.Playing or GamePhase.BossEncounter)
        {
            // Latch press edges until a fixed step consumes them: on 120 Hz+ displays a frame can be
            // shorter than one fixed step, and a press seen only on such a frame would otherwise be lost.
            _pendingPressed |= pressedNow;
        }
        else
        {
            _pendingPressed = InputButtons.None;
        }

        var input = new InputState(held, _pendingPressed);

        _snapshot.Clear();
        _snapshot.Running = Phase is GamePhase.Playing or GamePhase.BossEncounter;

        var clamped = Math.Clamp(deltaSeconds, 0, PhysicsConfig.MaxFrameDelta);
        Advance(clamped, input);
        Render(_snapshot);

        return _snapshot;
    }

    // ----- Helpers ----------------------------------------------------------

    private void ChangePhase(GamePhase next)
    {
        if (next == Phase)
        {
            return;
        }

        var previous = Phase;
        Phase = next;
        PhaseChanged?.Invoke(next, previous);
    }

    private void PublishHud(HudState hud)
    {
        if (hud == _hud)
        {
            return;
        }

        _hud = hud;
        HudChanged?.Invoke(hud);
    }

    private void Say(GameMessage message) => MessageShown?.Invoke(message);

    private void UpdateHighScore(int score)
    {
        var sanitized = HighScoreValidator.Sanitize(score);
        if (sanitized > HighScore)
        {
            HighScore = sanitized;
            HighScoreChanged?.Invoke(HighScore);
        }
    }
}
