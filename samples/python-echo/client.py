"""Minimal MeshTransit REQ client in Python — proves wire compatibility with C#.

Usage:
    python gen_protos.py            # one-time: regenerate *_pb2.py
    python client.py "hello"        # sends an EchoCommand to tcp://127.0.0.1:9999
"""
import sys
import time
import uuid

import zmq
from google.protobuf import timestamp_pb2

import envelope_pb2
import echo_pb2


SOURCE = "python-echo-client"
ENDPOINT = "tcp://127.0.0.1:9999"


def _now_ts() -> timestamp_pb2.Timestamp:
    ts = timestamp_pb2.Timestamp()
    ts.GetCurrentTime()
    return ts


def send_echo(text: str) -> str:
    cmd = echo_pb2.EchoCommand(text=text)

    envelope = envelope_pb2.Envelope(
        header=envelope_pb2.Header(
            correlation_id=uuid.uuid4().hex,
            sent_at=_now_ts(),
            source_service=SOURCE,
            message_type=echo_pb2.EchoCommand.DESCRIPTOR.full_name,
            schema_version=1,
        ),
        payload=cmd.SerializeToString(),
    )

    ctx = zmq.Context.instance()
    sock = ctx.socket(zmq.REQ)
    sock.setsockopt(zmq.LINGER, 0)
    sock.connect(ENDPOINT)
    try:
        sock.send(envelope.SerializeToString())
        if sock.poll(5000) == 0:
            raise TimeoutError(f"No reply from {ENDPOINT} within 5s")
        reply_bytes = sock.recv()
    finally:
        sock.close()

    reply_env = envelope_pb2.Envelope()
    reply_env.ParseFromString(reply_bytes)
    if reply_env.error.code:
        raise RuntimeError(f"{reply_env.error.code}: {reply_env.error.message}")

    reply = echo_pb2.EchoReply()
    reply.ParseFromString(reply_env.payload)
    return reply.text


if __name__ == "__main__":
    msg = sys.argv[1] if len(sys.argv) > 1 else "hello from python"
    t0 = time.perf_counter()
    out = send_echo(msg)
    dt_ms = (time.perf_counter() - t0) * 1000.0
    print(f'[python-echo] reply: "{out}"  ({dt_ms:.1f} ms)')
