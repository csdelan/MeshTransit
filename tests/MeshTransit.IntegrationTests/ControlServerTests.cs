using MeshTransit.Client;
using MeshTransit.Contracts;
using MeshTransit.TestContracts;

namespace MeshTransit.IntegrationTests;

public class ControlServerTests
{
    private sealed class EchoHandler : IMessageHandler<EchoCommand, EchoReply>
    {
        public Task<EchoReply> HandleAsync(EchoCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(new EchoReply
            {
                Text = command.Text,
                EchoedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
    }

    private sealed class ThrowingHandler : IMessageHandler<EchoCommand, EchoReply>
    {
        public Task<EchoReply> HandleAsync(EchoCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task RoundTrip_EchoesCommandText()
    {
        var endpoint = $"inproc://mt-test-{Guid.NewGuid():N}";
        var opts = new MeshTransitServerOptions
        {
            ServiceName = "echo",
            ControlEndpoint = endpoint,
            EventEndpoint = $"inproc://mt-evt-{Guid.NewGuid():N}",
        };
        using var server = new ControlServer<EchoCommand, EchoReply>(new EchoHandler(), opts);
        await server.StartAsync(CancellationToken.None);

        using var client = new CommandClient<EchoCommand, EchoReply>(endpoint, "client", TimeSpan.FromSeconds(3));
        var reply = await client.SendAsync(new EchoCommand { Text = "ping" });

        Assert.Equal("ping", reply.Text);
    }

    [Fact]
    public async Task HandlerException_SurfacesAsRemoteException()
    {
        var endpoint = $"inproc://mt-test-{Guid.NewGuid():N}";
        var opts = new MeshTransitServerOptions
        {
            ServiceName = "echo",
            ControlEndpoint = endpoint,
            EventEndpoint = $"inproc://mt-evt-{Guid.NewGuid():N}",
        };
        using var server = new ControlServer<EchoCommand, EchoReply>(new ThrowingHandler(), opts);
        await server.StartAsync(CancellationToken.None);

        using var client = new CommandClient<EchoCommand, EchoReply>(endpoint, "client", TimeSpan.FromSeconds(3));
        var ex = await Assert.ThrowsAsync<MeshTransitRemoteException>(
            () => client.SendAsync(new EchoCommand { Text = "x" }));
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task Timeout_RecoversOnNextRequest()
    {
        // Reserve a free TCP port, release it, then point a CommandClient at
        // it before binding a server. NetMQ TCP connect succeeds asynchronously,
        // so the request will block until the receive timeout fires — exercising
        // the socket-recreate path. Inproc can't model this (it throws on
        // connect-before-bind), so we use loopback TCP here.
        var port = ReserveFreeTcpPort();
        var endpoint = $"tcp://127.0.0.1:{port}";
        using var client = new CommandClient<EchoCommand, EchoReply>(endpoint, "client", TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAsync<TimeoutException>(
            () => client.SendAsync(new EchoCommand { Text = "x" }));

        var opts = new MeshTransitServerOptions
        {
            ServiceName = "echo",
            ControlEndpoint = $"tcp://*:{port}",
            EventEndpoint = $"inproc://mt-evt-{Guid.NewGuid():N}",
        };
        using var server = new ControlServer<EchoCommand, EchoReply>(new EchoHandler(), opts);
        await server.StartAsync(CancellationToken.None);

        var reply = await client.SendAsync(new EchoCommand { Text = "after" });
        Assert.Equal("after", reply.Text);
    }

    private static int ReserveFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
