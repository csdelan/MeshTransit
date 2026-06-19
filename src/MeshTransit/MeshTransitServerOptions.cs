namespace MeshTransit;

/// <summary>
/// Static configuration for a MeshTransit server (one control endpoint, one
/// event endpoint, one heartbeat cadence). Extends <see cref="HeartbeatOptions"/>
/// — the event endpoint, service name, and heartbeat fields are inherited; the
/// command server adds <see cref="ControlEndpoint"/>.
/// </summary>
public sealed class MeshTransitServerOptions : HeartbeatOptions
{
    /// <summary>REP socket bind address, e.g. <c>tcp://*:9000</c>.</summary>
    public string ControlEndpoint { get; set; } = string.Empty;
}
