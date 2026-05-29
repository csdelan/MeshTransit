using Google.Protobuf;

namespace MeshTransit;

/// <summary>
/// Handles a decoded <typeparamref name="TCommand"/> and returns a
/// <typeparamref name="TReply"/>. Exceptions thrown from <see cref="HandleAsync"/>
/// are converted into a populated <c>Envelope.Error</c> by the transport.
/// </summary>
public interface IMessageHandler<TCommand, TReply>
    where TCommand : IMessage<TCommand>, new()
    where TReply : IMessage<TReply>, new()
{
    Task<TReply> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
