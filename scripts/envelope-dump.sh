#!/usr/bin/env bash
# envelope-dump.sh — clean-stdout wrapper around tools/EnvelopeDump, the
# ENVELOPE dump for the cross-port REST error-ENVELOPE differ
# (porting-sdk/scripts/diff_port_envelope.py). MSBuild chatter + the mock's
# stderr -> stderr; ONLY the per-case artifact JSON reaches stdout by exec'ing
# the built DLL.
#
# Newest-by-mtime DLL selection (NOT `find | head`): never run a stale DLL from an
# earlier build — pick the most recently built one (the route-registry.sh trap).
#
# Usage (invoked by the differ with --dump-cmd):
#   bash scripts/envelope-dump.sh

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/EnvelopeDump/EnvelopeDump.csproj"

cd "$PORT_ROOT"

newest_dll() {
    find tools/EnvelopeDump/bin -name EnvelopeDump.dll -print0 2>/dev/null \
        | xargs -0 ls -t 2>/dev/null | head -1
}

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(newest_dll)"
    if [ -z "$dll" ]; then
        echo "envelope-dump.sh: EnvelopeDump.dll not found after build" >&2
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
            dll=$(find tools/EnvelopeDump/bin -name EnvelopeDump.dll -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -1)
            if [ -z "$dll" ]; then
                echo "envelope-dump.sh: EnvelopeDump.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll"
        '
fi
