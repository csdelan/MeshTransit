using MeshTransit.Contracts;
using MeshTransit.TestContracts;

namespace MeshTransit.Tests;

public class EventPublisherTests
{
    [Fact]
    public void Publish_OnReservedTopic_Throws()
    {
        using var bus = new EventBus();
        var publisher = new EventPublisher<PingEvent>(bus, "svc");

        var ex = Assert.Throws<ArgumentException>(
            () => publisher.Publish($"{MeshTransitProtocol.ReservedTopicPrefix}.bad", new PingEvent()));
        Assert.Contains("reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
