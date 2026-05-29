using MeshTransit.Client;
using MeshTransit.Sample.Echo.Shared;

var endpoint = args.Length > 0 ? args[0] : "tcp://127.0.0.1:9999";
var message  = args.Length > 1 ? args[1] : "hello from MeshTransit";

using var client = new CommandClient<EchoCommand, EchoReply>(endpoint, sourceService: "echo-client");

var reply = await client.SendAsync(new EchoCommand { Text = message });
Console.WriteLine($"[echo-client] reply: \"{reply.Text}\" (echoed at unix-ms {reply.EchoedAtUnixMs})");
