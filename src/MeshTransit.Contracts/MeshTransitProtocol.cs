namespace MeshTransit.Contracts;

/// <summary>
/// Framework-wide constants for the MeshTransit wire protocol.
/// </summary>
public static class MeshTransitProtocol
{
    /// <summary>Current envelope schema version stamped into outbound headers.</summary>
    public const uint SchemaVersion = 1;

    /// <summary>
    /// Reserved topic prefix for framework-internal PUB streams. Consumers must
    /// not publish topics under this namespace.
    /// </summary>
    public const string ReservedTopicPrefix = "_mt";

    /// <summary>Topic prefix for heartbeat broadcasts.</summary>
    public const string HeartbeatTopicPrefix = "_mt.heartbeat";

    /// <summary>Builds the full heartbeat topic for a given service name.</summary>
    public static string HeartbeatTopic(string serviceName) =>
        $"{HeartbeatTopicPrefix}.{serviceName}";
}
