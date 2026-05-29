using MeshTransit;
using MeshTransit.Sample.Echo.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMeshTransitServer<EchoCommand, EchoReply, PingEvent, EchoHandler>(opts =>
{
    opts.ServiceName = "echo";
    opts.ControlEndpoint = "tcp://*:9999";
    opts.EventEndpoint = "tcp://*:9998";
    opts.HeartbeatIntervalMs = 1000;
});

using var host = builder.Build();
Console.WriteLine("[echo-server] listening on tcp://*:9999 (control), tcp://*:9998 (events)");
await host.RunAsync();

internal sealed class EchoHandler : IMessageHandler<EchoCommand, EchoReply>
{
    public Task<EchoReply> HandleAsync(EchoCommand command, CancellationToken ct) =>
        Task.FromResult(new EchoReply
        {
            Text = "I'm replying",
            EchoedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
}
