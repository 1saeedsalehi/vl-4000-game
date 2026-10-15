namespace Videoland4000.Infrastructure.Mediator;

/// <summary>
/// Simple in-process mediator. Subscriptions return an <see cref="IDisposable"/>
/// so components can unsubscribe on dispose and restarts never accumulate
/// duplicate handlers.
/// </summary>
public class DefaultMediator : IMediator
{
    private readonly Dictionary<Type, List<Func<object, Task>>> _handlersByType = new();
    private readonly ILogger _logger;

    public DefaultMediator(ILogger<DefaultMediator> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent
    {
        if (_handlersByType.TryGetValue(@event.GetType(), out var handlers))
        {
            // Copy: handlers may (un)subscribe while we iterate.
            var tasks = handlers.ToArray().Select(handler => handler.Invoke(@event)).ToList();
            await Task.WhenAll(tasks);
        }
    }

    public async Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        if (_handlersByType.TryGetValue(typeof(TCommand), out var handlers) && handlers.Count > 0)
        {
            if (handlers.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple handlers found for command '{typeof(TCommand).Name}' when maximum 1 was expected.");
            }

            await handlers[0].Invoke(command);
        }
        else
        {
            _logger.LogDebug("No handler for command {Command}", typeof(TCommand).Name);
        }
    }

    public IDisposable Subscribe<TMessage>(Func<TMessage, Task> handler) where TMessage : IMessage
    {
        if (!_handlersByType.TryGetValue(typeof(TMessage), out var handlers))
        {
            handlers = new List<Func<object, Task>>();
            _handlersByType.Add(typeof(TMessage), handlers);
        }

        Func<object, Task> wrapper = message => handler((TMessage)message);
        handlers.Add(wrapper);

        return new Subscription(() => handlers.Remove(wrapper));
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
