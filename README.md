# MeshTransit

A domain-agnostic ZeroMQ + Protobuf messaging toolkit for peer-to-peer
microservice meshes on a LAN. REQ/REP commands, PUB/SUB events, built-in
heartbeats, static endpoint configuration, language-portable wire format.

## Packages

| NuGet | Purpose |
|---|---|
| `MeshTransit.Contracts` | Wire format (`Envelope`, `Header`, `Error`, `Heartbeat`) + `EnvelopeCodec`. |
| `MeshTransit`           | Server primitives: `ControlServer<,>`, `EventPublisher<>`, `HeartbeatPublisher`, `AddMeshTransitServer` extension. |
| `MeshTransit.Client`    | Client primitives: `CommandClient<,>`, `EventSubscriber<>`, `HeartbeatWatcher`. |

## When to use it

- You have a small fleet of cross-process services on a LAN that need fast,
  brokerless messaging without standing up RabbitMQ / NATS / Kafka.
- You want REQ/REP commands + PUB/SUB events on a uniform envelope.
- You want per-service liveness for free, with no extra code per service.

## When NOT to use it

- You need durable queues, replay, or exactly-once delivery — ZMQ semantics
  are inherited as-is.
- You need cross-WAN transport with auth/encryption — CurveZMQ integration is
  out of scope for Sprint 1.
- You want dynamic service discovery — endpoints come from config, period.

## Quickstart

See [`USAGE.md`](docs/USAGE.md) for server, client, and watcher recipes; see
[`PROTOCOL.md`](docs/PROTOCOL.md) for the wire format.

## Build

```powershell
dotnet build -c Release
dotnet test
```

## Samples

- `samples/csharp-echo/Server` and `samples/csharp-echo/Client` — minimal
  echo command server + client.
- `samples/python-echo` — `pyzmq` + `protobuf` client that talks to the C#
  server unchanged. Proves cross-language wire compatibility.
- `samples/coordinator` — generic-host worker running a `HeartbeatWatcher`
  over a config-driven peer list; logs `ServiceUp` / `ServiceDown` /
  `ServiceRestarted` transitions.
