#!/usr/bin/env bash
# wait-liveness-dump.sh — clean-stdout wrapper around tools/WaitLivenessDump, the
# WAIT-LIVENESS dump for the cross-port liveness differ
# (porting-sdk/scripts/diff_port_wait_liveness.py). MSBuild chatter -> stderr; ONLY
# the classification JSON reaches stdout by exec'ing the built DLL.
#
# Newest-by-mtime DLL selection (NOT `find | head`): never run a stale DLL from an
# earlier build — pick the most recently built one (the route-registry.sh trap).
#
# Usage (invoked by the differ with --dump-cmd):
#   bash scripts/wait-liveness-dump.sh

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/WaitLivenessDump/WaitLivenessDump.csproj"

cd "$PORT_ROOT"

newest_dll() {
    find tools/WaitLivenessDump/bin -name WaitLivenessDump.dll -print0 2>/dev/null \
        | xargs -0 ls -t 2>/dev/null | head -1
}

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(newest_dll)"
    if [ -z "$dll" ]; then
        echo "wait-liveness-dump.sh: WaitLivenessDump.dll not found after build" >&2
        exit 1
    fi
    exec "$dotnet_bin" "$dll"
else
    exec docker run --rm \
        --user "$(id -u):$(id -g)" \
        -e HOME=/tmp \
        -v "$PWD:/src" -w /src \
        mcr.microsoft.com/dotnet/sdk:10.0 \
        bash -c '
            set -e
            dotnet build '"$PROJ"' -c Release -v quiet 1>&2
            dll=$(find tools/WaitLivenessDump/bin -name WaitLivenessDump.dll -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -1)
            if [ -z "$dll" ]; then
                echo "wait-liveness-dump.sh: WaitLivenessDump.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll"
        '
fi
