# Guidance for AI assistants using MeshTransit in consumer codebases

## What this library is

A domain-agnostic ZeroMQ + Protobuf transport. It owns envelope framing,
correlation IDs, REQ-socket recovery, SUB reconnect, heartbeat emission, and
liveness watching. It owns **nothing else**.

## What it does NOT do

- No application types. `EchoCommand` / `PingEvent` are sample-only.
- No port constants. Endpoints come from the consumer's configuration.
- No service registry, no discovery, no DNS-SD. Endpoints are static.
- No durability, no replay, no exactly-once. ZMQ delivery semantics
  are inherited as-is.
- No authentication or encryption (Sprint 1 ships unprotected).

## Recipes

- **Commands with replies** → consumer-defined `TCommand` + `TReply` Protobuf
  messages; implement `IMessageHandler<TCommand, TReply>`; register with
  `AddMeshTransitServer<TCommand, TReply, TEvent, THandler>`.
- **Events** → consumer-defined `TEvent` Protobuf message; inject
  `EventPublisher<TEvent>`; call `Publish(topic, event)`. Do not use topics
  beginning with `_mt`.
- **Liveness** → `HeartbeatWatcher` over a config-driven `ServiceEndpoint`
  list. Heartbeats are emitted automatically by every server constructed via
  `AddMeshTransitServer`. A process with no command surface advertises liveness
  via `AddMeshTransitHeartbeat(opts => …)` (a bound event endpoint, no REQ/REP).
  Either way, never publish to `_mt.heartbeat.*` manually. Drive runtime health
  through `HeartbeatOptions.HealthSource` / `MetadataProvider` (pulled per tick),
  not by hand-rolling heartbeat messages.

## Don't reach into the transport

If you find yourself wanting to add a new socket type, new wire field, or new
reserved topic prefix, that is a library change — open a PR against
MeshTransit, do not work around it in consumer code.

## File layout in consumer projects

- Keep your `.proto` files alongside your domain code, not under
  `MeshTransit.*`. The library's `Contracts` package only carries framework
  schemas.
- Bind `ServiceEndpoint` records from your `appsettings.json` — do not
  hardcode endpoints in source.
