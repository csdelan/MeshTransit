namespace MeshTransit;

/// <summary>
/// Static configuration for a MeshTransit server (one control endpoint, one
/// event endpoint, one heartbeat cadence). All fields are required except
/// <see cref="HeartbeatIntervalMs"/>.
/// </summary>
public sealed class MeshTransitServerOptions
{
    /// <summary>Logical service name — stamped on heartbeats and headers.</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>REP socket bind address, e.g. <c>tcp://*:9000</c>.</summary>
    public string ControlEndpoint { get; set; } = string.Empty;

    /// <summary>PUB socket bind address, e.g. <c>tcp://*:9001</c>.</summary>
    public string EventEndpoint { get; set; } = string.Empty;

    /// <summary>Heartbeat cadence in milliseconds (default 1000).</summary>
    public int HeartbeatIntervalMs { get; set; } = 1000;

    /// <summary>Free-form metadata attached to every heartbeat.</summary>
    public Dictionary<string, string> HeartbeatMetadata { get; } = new();
}
