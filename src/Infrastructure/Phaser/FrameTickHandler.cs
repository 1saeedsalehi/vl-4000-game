namespace Videoland4000.Infrastructure.Phaser;

/// <summary>
/// Receives the per-frame callback from the Phaser scene. JavaScript calls
/// <see cref="Tick"/> synchronously once per rendered frame with the elapsed
/// time and the held input bit mask, and receives the frame snapshot back.
/// </summary>
public sealed class FrameTickHandler
{
    private readonly Func<double, InputButtons, FrameSnapshot> _tick;
    private readonly Action? _onReady;
    private readonly Func<string, string>? _debug;

    public FrameTickHandler(
        Func<double, InputButtons, FrameSnapshot> tick,
        Action? onReady = null,
        Func<string, string>? debug = null)
    {
        _tick = tick;
        _onReady = onReady;
        _debug = debug;
    }

    /// <summary>Developer hook (wired only with ?dev): forwards a text command to the session.</summary>
    [JSInvokable]
    public string Debug(string command) => _debug is null ? "debug hook disabled" : _debug(command);

    /// <summary>Called by Phaser once the scene has been created and the atlas is loaded.</summary>
    [JSInvokable]
    public void Ready() => _onReady?.Invoke();

    [JSInvokable]
    public FrameSnapshot Tick(double deltaMs, int inputBits) =>
        _tick(deltaMs, (InputButtons)inputBits);
}
