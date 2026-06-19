using MeshTransit.Contracts;

namespace MeshTransit;

/// <summary>
/// Configuration for a heartbeat publisher. Carries everything needed to advertise
/// liveness on the reserved <c>_mt.heartbeat.&lt;service-name&gt;</c> topic without
/// requiring a full command server. <see cref="MeshTransitServerOptions"/> extends
/// this for the command-server case.
/// </summary>
public class HeartbeatOptions
{
    /// <summary>Logical service name — stamped on every heartbeat.</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>PUB socket bind address the heartbeat is broadcast on, e.g. <c>tcp://*:9001</c>.</summary>
    public string EventEndpoint { get; set; } = string.Empty;

    /// <summary>Heartbeat cadence in milliseconds (default 1000).</summary>
    public int HeartbeatIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Static metadata seeded onto every heartbeat (e.g. version, pid). Used as the
    /// base each tick before <see cref="MetadataProvider"/> runs.
    /// </summary>
    public Dictionary<string, string> HeartbeatMetadata { get; } = new();

    /// <summary>
    /// Optional health probe evaluated on every heartbeat tick. When set, its result
    /// governs the published <see cref="ServiceStatus"/> (a <see cref="ServiceStatus.Stopping"/>
    /// shutdown transition always wins). Lets a consumer report Degraded from live state
    /// without pushing via <see cref="HeartbeatPublisher.SetStatus"/>.
    /// </summary>
    public Func<ServiceStatus>? HealthSource { get; set; }

    /// <summary>
    /// Optional callback to refresh dynamic metadata on every heartbeat tick. Receives a
    /// fresh map pre-seeded from <see cref="HeartbeatMetadata"/>; mutate it in place. Use
    /// for values that change at runtime (last-job outcome, queue depth, etc.).
    /// </summary>
    public Action<IDictionary<string, string>>? MetadataProvider { get; set; }
}
