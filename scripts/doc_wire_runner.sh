#!/usr/bin/env bash
# doc_wire_runner.sh — the DOC-WIRE fixture runner for signalwire-dotnet.
#
# porting-sdk scripts/doc_wire.py spawns mock_signalwire in FLAG mode, exports
# MOCK_SIGNALWIRE_PORT, then runs THIS script; it reads the mock journal after and
# fails on any journaled wire_violations. We build + run tools/DocWire, which
# replays the documented REST calls (README / rest quickstarts) against the mock.
#
# Newest-by-mtime DLL selection (NOT `find | head`): a stale DLL from an earlier
# build must never be run — pick the most recently built one so a fresh source
# change is what actually executes (the route-registry.sh stale-DLL trap).
#
# Usage (invoked by doc_wire.py with MOCK_SIGNALWIRE_PORT in the env):
#   bash scripts/doc_wire_runner.sh

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/DocWire/DocWire.csproj"

cd "$PORT_ROOT"

newest_dll() {
    # Print the most-recently-modified DocWire.dll, or nothing if none exists.
    find tools/DocWire/bin -name DocWire.dll -print0 2>/dev/null \
        | xargs -0 ls -t 2>/dev/null | head -1
}

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(newest_dll)"
    if [ -z "$dll" ]; then
        echo "doc_wire_runner.sh: DocWire.dll not found after build" >&2
        exit 1
    fi
    exec "$dotnet_bin" "$dll"
else
    # dotnet absent locally → run inside the SDK image. The mock is on the host,
    # reachable via --network host; forward the mock env vars into the container.
    exec docker run --rm \
        --network host \
        --user "$(id -u):$(id -g)" \
        -e HOME=/tmp \
        -e MOCK_SIGNALWIRE_PORT \
        -e MOCK_SIGNALWIRE_HOST \
        -e SIGNALWIRE_MOCK_URL \
        -v "$PWD:/src" -w /src \
        mcr.microsoft.com/dotnet/sdk:10.0 \
        bash -c '
            set -e
            dotnet build '"$PROJ"' -c Release -v quiet 1>&2
            dll=$(find tools/DocWire/bin -name DocWire.dll -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -1)
            if [ -z "$dll" ]; then
                echo "doc_wire_runner.sh: DocWire.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll"
        '
fi
