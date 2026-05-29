using MeshTransit.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Bind ServiceEndpoint[] from appsettings.json -> "Peers" section.
var peers = builder.Configuration.GetSection("Peers").Get<List<PeerConfig>>() ?? new();

builder.Services.AddHostedService(sp =>
    new CoordinatorWorker(
        peers.Select(p => new ServiceEndpoint(p.Name, p.ControlEndpoint, p.EventEndpoint)).ToList(),
        sp.GetRequiredService<ILogger<CoordinatorWorker>>()));

using var host = builder.Build();
await host.RunAsync();

internal sealed record PeerConfig(string Name, string? ControlEndpoint, string? EventEndpoint);

internal sealed class CoordinatorWorker : BackgroundService
{
    private readonly IReadOnlyList<ServiceEndpoint> _peers;
    private readonly ILogger<CoordinatorWorker> _log;

    public CoordinatorWorker(IReadOnlyList<ServiceEndpoint> peers, ILogger<CoordinatorWorker> log)
    {
        _peers = peers;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var watcher = new HeartbeatWatcher(_peers);
        watcher.ServiceUp        += s => _log.LogInformation("{Service} UP (instance {Instance})", s.Name, s.InstanceId);
        watcher.ServiceDown      += s => _log.LogWarning("{Service} DOWN ({Reason})", s.Name, s.LastDownReason);
        watcher.ServiceRestarted += s => _log.LogInformation("{Service} RESTARTED (new instance {Instance})", s.Name, s.InstanceId);
        watcher.StatusChanged    += s => _log.LogInformation("{Service} status → {Status}", s.Name, s.Status);

        await watcher.StartAsync(stoppingToken);
        _log.LogInformation("Coordinator watching {Count} peer(s).", _peers.Count);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (TaskCanceledException) { }

        await watcher.StopAsync(CancellationToken.None);
    }
}
