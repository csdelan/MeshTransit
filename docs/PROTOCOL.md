# MeshTransit — Wire Protocol

## Envelope

Every MeshTransit frame is a serialized `meshtransit.v1.Envelope` (see
`src/MeshTransit.Contracts/proto/envelope.proto`):

```proto
message Envelope {
  Header header  = 1;
  bytes  payload = 2;   // consumer-specific protobuf message
  Error  error   = 3;   // populated on REP failures; absent on success
}
message Header {
  string correlation_id            = 1;
  google.protobuf.Timestamp sent_at = 2;
  string source_service            = 3;
  string message_type              = 4;   // FQ name of inner payload type
  uint32 schema_version            = 5;
}
message Error { string code = 1; string message = 2; string details = 3; }
```

`payload` is opaque bytes — the consumer's own Protobuf message. The library
never inspects it.

## Transport patterns

| Pattern | Socket types | Use |
|---|---|---|
| REQ ↔ REP | `RequestSocket` ↔ `ResponseSocket` | Commands with replies. Single-flight per client; timeout-bounded; REQ socket recreated after a timeout to clear NetMQ's stuck state. |
| PUB ↔ SUB | `PublisherSocket` ↔ `SubscriberSocket` | Events and heartbeats. Two-frame messages: topic string, then envelope bytes. Topic filtering via prefix subscription. |

## Reserved topic namespace

The prefix `_mt.*` is reserved for framework use. `EventPublisher<TEvent>`
rejects publishes under this namespace. Currently used:

- `_mt.heartbeat.<service-name>` — `Heartbeat` messages from every running
  service.

## Heartbeat

```proto
enum ServiceStatus { SERVICE_STATUS_UNSPECIFIED = 0; STARTING = 1; HEALTHY = 2; DEGRADED = 3; STOPPING = 4; }
message Heartbeat {
  string service_name                       = 1;
  string instance_id                        = 2;   // GUID per process start
  google.protobuf.Timestamp started_at      = 3;
  google.protobuf.Timestamp sent_at         = 4;
  uint64 sequence                           = 5;
  ServiceStatus status                      = 6;
  uint32 interval_ms                        = 7;
  map<string, string> metadata              = 8;
}
```

A service emits heartbeats on its own event PUB socket — no separate port.
On graceful shutdown a final `STOPPING` heartbeat is drained so watchers can
distinguish clean exits from crashes.

## Versioning rules

- `Header.schema_version` is incremented on breaking envelope changes only.
  Sprint 1 ships at `schema_version = 1`.
- Adding fields to the inner consumer payload is the consumer's concern;
  MeshTransit does not gate or validate inner payload schemas beyond carrying
  `message_type` for diagnostics.
- New entries under the reserved `_mt.*` topic namespace require a minor
  version bump and a `PROTOCOL.md` entry.
