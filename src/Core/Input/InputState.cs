namespace Videoland4000.Input;

/// <summary>
/// Gameplay input as a bit mask. JavaScript samples the held keys/touch
/// buttons every frame and passes the mask to <c>Tick</c>. One-shot UI keys
/// (pause, retry, menu) travel through the mediator instead.
/// </summary>
[Flags]
public enum InputButtons
{
    None = 0,
    Left = 1,
    Right = 2,
    Jump = 4,
    Interact = 8,
    Down = 16,
}

/// <summary>Held buttons plus per-frame edge detection.</summary>
public readonly record struct InputState(InputButtons Held, InputButtons Pressed)
{
    public static readonly InputState None = new(InputButtons.None, InputButtons.None);

    public bool IsHeld(InputButtons button) => (Held & button) != 0;

    public bool WasPressed(InputButtons button) => (Pressed & button) != 0;

    /// <summary>Creates the state for this frame given the previously held buttons.</summary>
    public static InputState FromHeld(InputButtons held, InputButtons previouslyHeld) =>
        new(held, held & ~previouslyHeld);
}
