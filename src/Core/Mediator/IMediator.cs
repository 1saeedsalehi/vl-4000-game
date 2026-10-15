namespace Videoland4000.Mediator;

/// <summary>
/// In-process message bus used to decouple the Blazor UI components from the
/// game session host.
/// Commands have exactly one handler, events may have many.
/// </summary>
public interface IMediator
{
    Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand;

    Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent;

    IDisposable Subscribe<TMessage>(Func<TMessage, Task> handler) where TMessage : IMessage;
}
