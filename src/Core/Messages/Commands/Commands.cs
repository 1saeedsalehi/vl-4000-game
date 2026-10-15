namespace Videoland4000.Messages.Commands;

/// <summary>The boot sequence finished or was skipped; show the main menu.</summary>
public record BootCompleted() : ICommand;

/// <summary>Start a new game from the main menu.</summary>
public record StartGame() : ICommand;

/// <summary>Skip the level introduction card.</summary>
public record SkipIntro() : ICommand;

/// <summary>Pause or resume gameplay (P / Escape).</summary>
public record TogglePause() : ICommand;

/// <summary>Retry after game over (R). Restores the last checkpoint when available.</summary>
public record RetryGame() : ICommand;

/// <summary>Return to the main menu (M) from pause, game over or victory.</summary>
public record ReturnToMainMenu() : ICommand;

/// <summary>Toggle sound on/off.</summary>
public record ToggleSound() : ICommand;

/// <summary>Toggle the CRT scanline overlay.</summary>
public record ToggleCrt() : ICommand;
