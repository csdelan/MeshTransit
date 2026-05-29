namespace MeshTransit.Client;

/// <summary>
/// Static peer descriptor. Either or both endpoints may be null — a peer that
/// only emits events but accepts no commands still gets a record here for
/// liveness tracking.
/// </summary>
/// <param name="Name">Logical service name; must match the peer's <c>service_name</c> heartbeat field.</param>
/// <param name="ControlEndpoint">REQ-side connect address, e.g. <c>tcp://host:9000</c>.</param>
/// <param name="EventEndpoint">SUB-side connect address, e.g. <c>tcp://host:9001</c>.</param>
public sealed record ServiceEndpoint(
    string Name,
    string? ControlEndpoint,
    string? EventEndpoint);
