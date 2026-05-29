using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace MeshTransit.Contracts;

/// <summary>
/// Encodes and decodes <see cref="Envelope"/>s carrying inner Protobuf payloads.
/// The transport layer never inspects payload bytes — only the codec deserializes
/// them into the typed message known by the consumer.
/// </summary>
public static class EnvelopeCodec
{
    /// <summary>
    /// Builds a payload-carrying envelope. <paramref name="correlationId"/> is
    /// echoed back on the reply so callers can match request/response.
    /// </summary>
    public static Envelope Encode<T>(
        T payload,
        string sourceService,
        string? correlationId = null) where T : IMessage<T>
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new Envelope
        {
            Header = new Header
            {
                CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
                SentAt = Timestamp.FromDateTime(DateTime.UtcNow),
                SourceService = sourceService ?? string.Empty,
                MessageType = payload.Descriptor.FullName,
                SchemaVersion = MeshTransitProtocol.SchemaVersion,
            },
            Payload = payload.ToByteString(),
        };
    }

    /// <summary>
    /// Builds an error envelope (no payload) for REP failure responses.
    /// </summary>
    public static Envelope EncodeError(
        string sourceService,
        string correlationId,
        string code,
        string message,
        string? details = null)
    {
        return new Envelope
        {
            Header = new Header
            {
                CorrelationId = correlationId ?? string.Empty,
                SentAt = Timestamp.FromDateTime(DateTime.UtcNow),
                SourceService = sourceService ?? string.Empty,
                MessageType = string.Empty,
                SchemaVersion = MeshTransitProtocol.SchemaVersion,
            },
            Error = new Error
            {
                Code = code ?? string.Empty,
                Message = message ?? string.Empty,
                Details = details ?? string.Empty,
            },
        };
    }

    public static byte[] ToBytes(Envelope envelope) => envelope.ToByteArray();

    public static Envelope FromBytes(byte[] frame) =>
        Envelope.Parser.ParseFrom(frame);

    /// <summary>
    /// Decodes the payload bytes inside <paramref name="envelope"/> into
    /// <typeparamref name="T"/>. Throws if the envelope carries an error.
    /// </summary>
    public static T DecodePayload<T>(Envelope envelope) where T : IMessage<T>, new()
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Error is { } err && !string.IsNullOrEmpty(err.Code))
        {
            throw new MeshTransitRemoteException(err.Code, err.Message, err.Details);
        }
        var parser = new MessageParser<T>(() => new T());
        return parser.ParseFrom(envelope.Payload);
    }
}

/// <summary>
/// Raised on the client side when a REP responds with an <c>Envelope.Error</c>.
/// </summary>
public sealed class MeshTransitRemoteException : Exception
{
    public string Code { get; }
    public string Details { get; }

    public MeshTransitRemoteException(string code, string message, string details)
        : base(message)
    {
        Code = code ?? string.Empty;
        Details = details ?? string.Empty;
    }
}
