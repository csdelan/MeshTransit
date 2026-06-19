using MeshTransit.Client;
using MeshTransit.Contracts;

namespace MeshTransit.IntegrationTests;

public class HeartbeatWatcherTests
{
    [Fact]
    public async Task TwoPeers_BothReachUp()
    {
        var ep1 = $"inproc://mt-evt-{Guid.NewGuid():N}";
        var ep2 = $"inproc://mt-evt-{Guid.NewGuid():N}";

        using var bus1 = new EventBus();
        using var bus2 = new EventBus();
        bus1.Bind(ep1);
        bus2.Bind(ep2);

        await using var hb1 = new HeartbeatPublisher(bus1, new MeshTransitServerOptions
        {
            ServiceName = "alpha",
            HeartbeatIntervalMs = 100,
        });
        await using var hb2 = new HeartbeatPublisher(bus2, new MeshTransitServerOptions
        {
            ServiceName = "beta",
            HeartbeatIntervalMs = 100,
        });

        var ups = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var watcher = new HeartbeatWatcher(new[]
        {
            new ServiceEndpoint("alpha", null, ep1),
            new ServiceEndpoint("beta",  null, ep2),
        }, missTolerance: 3);
        watcher.ServiceUp += s => ups.Add(s.Name);
        await watcher.StartAsync();

        await hb1.StartAsync(CancellationToken.None);
        await hb2.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && ups.Distinct().Count() < 2)
            await Task.Delay(50);

        Assert.Contains("alpha", ups);
        Assert.Contains("beta", ups);
    }

    [Fact]
    public async Task GracefulStop_FiresServiceDownWithGracefulReason()
    {
        var ep = $"inproc://mt-evt-{Guid.NewGuid():N}";
        using var bus = new EventBus();
        bus.Bind(ep);

        var hb = new HeartbeatPublisher(bus, new MeshTransitServerOptions
        {
            ServiceName = "gamma",
            HeartbeatIntervalMs = 100,
        });

        using var watcher = new HeartbeatWatcher(new[]
        {
            new ServiceEndpoint("gamma", null, ep),
        });
        var upSeen = new TaskCompletionSource();
        var downSeen = new TaskCompletionSource<ServiceDownReason>();
        watcher.ServiceUp += _ => upSeen.TrySetResult();
        watcher.ServiceDown += s => downSeen.TrySetResult(s.LastDownReason);
        await watcher.StartAsync();

        await hb.StartAsync(CancellationToken.None);
        await upSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await hb.StopAsync(CancellationToken.None);

        var reason = await downSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ServiceDownReason.Graceful, reason);

        await hb.DisposeAsync();
    }

    [Fact]
    public async Task PeerSilence_TripsMissedHeartbeatsAfterTolerance()
    {
        var ep = $"inproc://mt-evt-{Guid.NewGuid():N}";
        using var bus = new EventBus();
        bus.Bind(ep);

        var hb = new HeartbeatPublisher(bus, new MeshTransitServerOptions
        {
            ServiceName = "delta",
            HeartbeatIntervalMs = 100,
        });

        using var watcher = new HeartbeatWatcher(new[]
            {
                new ServiceEndpoint("delta", null, ep),
            },
            missTolerance: 3,
            minIntervalMs: 100,
            checkInterval: TimeSpan.FromMilliseconds(100));

        var upSeen = new TaskCompletionSource();
        var downSeen = new TaskCompletionSource<ServiceDownReason>();
        watcher.ServiceUp += _ => upSeen.TrySetResult();
        watcher.ServiceDown += s => downSeen.TrySetResult(s.LastDownReason);
        await watcher.StartAsync();

        await hb.StartAsync(CancellationToken.None);
        await upSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Simulate a crash: kill the bus first so the publisher's eventual
        // STOPPING drain becomes a no-op and the watcher only sees silence.
        bus.Dispose();
        await hb.DisposeAsync();

        var reason = await downSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ServiceDownReason.MissedHeartbeats, reason);
    }

    [Fact]
    public async Task HealthSourceAndMetadataProvider_AreEvaluatedPerTick_AndUptimeIsExposed()
    {
        var ep = $"inproc://mt-evt-{Guid.NewGuid():N}";
        using var bus = new EventBus();
        bus.Bind(ep);

        var status = ServiceStatus.Healthy;
        var ticks = 0;

        await using var hb = new HeartbeatPublisher(bus, new HeartbeatOptions
        {
            ServiceName = "epsilon",
            HeartbeatIntervalMs = 100,
            HealthSource = () => status,
            MetadataProvider = meta => meta["ticks"] = (++ticks).ToString(),
        });

        ServiceLiveness? latest = null;
        var upSeen = new TaskCompletionSource();
        var degradedSeen = new TaskCompletionSource();
        using var watcher = new HeartbeatWatcher(new[]
        {
            new ServiceEndpoint("epsilon", null, ep),
        });
        watcher.ServiceUp += s => { latest = s; upSeen.TrySetResult(); };
        watcher.StatusChanged += s =>
        {
            latest = s;
            if (s.Status == ServiceStatus.Degraded) degradedSeen.TrySetResult();
        };
        await watcher.StartAsync();
        await hb.StartAsync(CancellationToken.None);

        // Observe at least one Healthy heartbeat before flipping, so the watcher
        // records a genuine Healthy -> Degraded transition (not a first-seen Degraded).
        await upSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Flip the health source; a subsequent tick must publish Degraded.
        status = ServiceStatus.Degraded;
        await degradedSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(latest);
        Assert.True(latest!.StartedAt > DateTime.MinValue);
        Assert.True(latest.Uptime >= TimeSpan.Zero);
        Assert.True(ticks > 0);
    }
}
