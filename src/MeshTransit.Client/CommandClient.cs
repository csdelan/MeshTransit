using Google.Protobuf;
using MeshTransit.Contracts;
using NetMQ;
using NetMQ.Sockets;

namespace MeshTransit.Client;

/// <summary>
/// REQ-side client for a single peer's control endpoint. REQ sockets are
/// strict request-then-reply, so this client serializes calls behind an
/// internal mutex. On a timeout the socket is recreated to clear NetMQ's
/// "stuck" REQ state, then the next call can proceed.
/// </summary>
public sealed class CommandClient<TCommand, TReply> : IDisposable
    where TCommand : IMessage<TCommand>
    where TReply : IMessage<TReply>, new()
{
    private readonly string _endpoint;
    private readonly string _sourceService;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private RequestSocket? _socket;
    private bool _disposed;

    public CommandClient(string endpoint, string sourceService, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        _endpoint = endpoint;
        _sourceService = sourceService ?? string.Empty;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<TReply> SendAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureSocket();
            var envelope = EnvelopeCodec.Encode(command, _sourceService);
            var requestFrame = EnvelopeCodec.ToBytes(envelope);

            // NetMQ has no async wire API; run the blocking call on a worker
            // thread so we honor CancellationToken at the await boundary.
            return await Task.Run(() =>
            {
                var socket = _socket!;
                socket.SendFrame(requestFrame);
                if (!socket.TryReceiveFrameBytes(_timeout, out var replyFrame) || replyFrame is null)
                {
                    // REQ socket is now in an unrecoverable state; recreate it
                    // so the next call starts clean.
                    RecreateSocket();
                    throw new TimeoutException(
                        $"MeshTransit request to {_endpoint} timed out after {_timeout.TotalMilliseconds:F0} ms.");
                }
                var reply = EnvelopeCodec.FromBytes(replyFrame);
                return EnvelopeCodec.DecodePayload<TReply>(reply);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureSocket()
    {
        if (_socket is not null) return;
        _socket = new RequestSocket();
        _socket.Connect(_endpoint);
    }

    private void RecreateSocket()
    {
        try { _socket?.Dispose(); } catch { }
        _socket = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _socket?.Dispose(); } catch { }
        _gate.Dispose();
    }
}
