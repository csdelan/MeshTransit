using MeshTransit.Contracts;
using MeshTransit.TestContracts;

namespace MeshTransit.Tests;

public class EnvelopeCodecTests
{
    [Fact]
    public void RoundTrip_PreservesPayload()
    {
        var cmd = new EchoCommand { Text = "hello" };
        var envelope = EnvelopeCodec.Encode(cmd, sourceService: "tester");
        var bytes = EnvelopeCodec.ToBytes(envelope);

        var decoded = EnvelopeCodec.FromBytes(bytes);
        var decodedCmd = EnvelopeCodec.DecodePayload<EchoCommand>(decoded);

        Assert.Equal("hello", decodedCmd.Text);
        Assert.Equal("tester", decoded.Header.SourceService);
        Assert.Equal(MeshTransitProtocol.SchemaVersion, decoded.Header.SchemaVersion);
        Assert.False(string.IsNullOrEmpty(decoded.Header.CorrelationId));
        Assert.Equal(EchoCommand.Descriptor.FullName, decoded.Header.MessageType);
    }

    [Fact]
    public void Encode_AcceptsProvidedCorrelationId()
    {
        var cmd = new EchoCommand { Text = "x" };
        var envelope = EnvelopeCodec.Encode(cmd, "svc", correlationId: "abc-123");
        Assert.Equal("abc-123", envelope.Header.CorrelationId);
    }

    [Fact]
    public void DecodePayload_OnErrorEnvelope_Throws()
    {
        var err = EnvelopeCodec.EncodeError("svc", "corr", "BOOM", "kaboom", "stack");
        var bytes = EnvelopeCodec.ToBytes(err);
        var decoded = EnvelopeCodec.FromBytes(bytes);

        var ex = Assert.Throws<MeshTransitRemoteException>(
            () => EnvelopeCodec.DecodePayload<EchoReply>(decoded));
        Assert.Equal("BOOM", ex.Code);
        Assert.Equal("kaboom", ex.Message);
    }

    [Fact]
    public void HeartbeatTopic_UsesReservedPrefix()
    {
        var topic = MeshTransitProtocol.HeartbeatTopic("alerts");
        Assert.Equal("_mt.heartbeat.alerts", topic);
        Assert.StartsWith(MeshTransitProtocol.ReservedTopicPrefix, topic);
    }
}
