# MeshTransit — Usage

## Server

Define your command, reply, and event payloads as Protobuf messages in your
own `.proto` files. Then implement a handler and register everything in a
generic host:

```csharp
public sealed class EchoHandler : IMessageHandler<EchoCommand, EchoReply>
{
    public Task<EchoReply> HandleAsync(EchoCommand command, CancellationToken ct) =>
        Task.FromResult(new EchoReply { Text = command.Text });
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMeshTransitServer<EchoCommand, EchoReply, PingEvent, EchoHandler>(opts =>
{
    opts.ServiceName     = "echo";
    opts.ControlEndpoint = "tcp://*:9000";   // REP socket binds here
    opts.EventEndpoint   = "tcp://*:9001";   // PUB socket binds here
    opts.HeartbeatIntervalMs = 1000;
});

await builder.Build().RunAsync();
```

Heartbeats are emitted automatically — you do not call any heartbeat API.

To publish a domain event:

```csharp
public sealed class TickWorker(EventPublisher<PingEvent> events) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        ulong seq = 0;
        while (!ct.IsCancellationRequested)
        {
            events.Publish("ping", new PingEvent { Note = "tick", Sequence = ++seq });
            await Task.Delay(1000, ct);
        }
    }
}
```

## Heartbeat without a command server

A process that only needs to advertise liveness — a background-job worker, a
scheduler, anything with no REQ/REP surface — can register just the heartbeat:

```csharp
builder.Services.AddMeshTransitHeartbeat(opts =>
{
    opts.ServiceName   = "rates-worker";
    opts.EventEndpoint = "tcp://*:9101";       // PUB socket binds here
    opts.HeartbeatIntervalMs = 5000;
    opts.HeartbeatMetadata["version"] = "2.0.1";

    // Status is pulled every tick — report Degraded from live state without
    // calling SetStatus. A STOPPING shutdown transition always wins.
    opts.HealthSource = () => jobs.AnyRecentFailure ? ServiceStatus.Degraded : ServiceStatus.Healthy;

    // Dynamic metadata, refreshed every tick (seeded from HeartbeatMetadata).
    opts.MetadataProvider = meta => meta["jobs.failing"] = jobs.FailingCount.ToString();
});
```

This brings up a dedicated `EventBus` + `HeartbeatPublisher` + hosted service.
Do not combine it with `AddMeshTransitServer` in the same container — both bind
the event endpoint. Servers registered via `AddMeshTransitServer` already emit
heartbeats and now accept the same `HealthSource` / `MetadataProvider` options.

## Client

```csharp
using var client = new CommandClient<EchoCommand, EchoReply>(
    "tcp://echo-host:9000", sourceService: "trader");

var reply = await client.SendAsync(new EchoCommand { Text = "ping" });
```

```csharp
using var sub = new EventSubscriber<PingEvent>("tcp://echo-host:9001", topicPrefix: "ping");
sub.EventReceived += (topic, ev) => Console.WriteLine($"{topic} #{ev.Sequence}: {ev.Note}");
await sub.StartAsync();
```

## Heartbeat watcher

```csharp
var watcher = new HeartbeatWatcher(new[]
{
    new ServiceEndpoint("alerts",    null, "tcp://alerts-host:7802"),
    new ServiceEndpoint("execution", null, "tcp://exec-host:7902"),
});
watcher.ServiceUp     += s => Console.WriteLine($"{s.Name} up (instance {s.InstanceId})");
watcher.ServiceDown   += s => Console.WriteLine($"{s.Name} down ({s.LastDownReason})");
watcher.StatusChanged += s => Console.WriteLine($"{s.Name} → {s.Status}");
await watcher.StartAsync();

foreach (var s in watcher.Services)
    Console.WriteLine($"{s.Name} {s.Status} last-seen {s.LastHeartbeat:o}");
```

A service is **Up** the first time a heartbeat is observed; **Down** when no
heartbeat arrives within `max(interval_ms, configuredMin) × missTolerance`
(default tolerance 3). An explicit `STOPPING` heartbeat raises `ServiceDown`
immediately with `LastDownReason = Graceful`. A new `instance_id` from the
same service raises `ServiceRestarted`.
