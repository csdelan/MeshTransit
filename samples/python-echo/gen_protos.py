"""Regenerate Python bindings from the canonical .proto files.

Run once after checkout, then commit the generated *_pb2.py files alongside
this script (or .gitignore them and regenerate on demand).
"""
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).parent
ROOT = HERE.parent.parent

PROTOS = [
    ROOT / "src" / "MeshTransit.Contracts" / "proto" / "envelope.proto",
    ROOT / "src" / "MeshTransit.Contracts" / "proto" / "heartbeat.proto",
    ROOT / "samples" / "csharp-echo" / "Shared" / "proto" / "echo.proto",
]

INCLUDE_DIRS = [
    ROOT / "src" / "MeshTransit.Contracts" / "proto",
    ROOT / "samples" / "csharp-echo" / "Shared" / "proto",
]


def main() -> int:
    cmd = [sys.executable, "-m", "grpc_tools.protoc"]
    for inc in INCLUDE_DIRS:
        cmd += [f"--proto_path={inc}"]
    cmd += [f"--python_out={HERE}"]
    cmd += [str(p) for p in PROTOS]
    print(" ".join(cmd))
    return subprocess.call(cmd)


if __name__ == "__main__":
    sys.exit(main())
