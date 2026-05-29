# python-echo

Validates wire compatibility between the reference C# MeshTransit implementation
and a third-party Protobuf + ZeroMQ stack. The Python client speaks the exact
same `Envelope` framing as `MeshTransit.Client.CommandClient<,>` does in C#.

## Setup

```bash
python -m venv .venv
. .venv/bin/activate            # or .venv\Scripts\activate on Windows
pip install pyzmq protobuf grpcio-tools
python gen_protos.py            # regenerates envelope_pb2.py, heartbeat_pb2.py, echo_pb2.py
```

## Run

In one terminal, start the C# echo server:

```bash
cd ../csharp-echo/Server && dotnet run
```

In another:

```bash
python client.py "hello from python"
```

You should see the server's reply printed locally — proof that the
`meshtransit.v1.Envelope` round-trips across the language boundary.
