#!/usr/bin/env bash
# dump-corpus.sh — clean-stdout wrapper around the .NET Layer-D DUMP program
# (tools/DumpCorpus). It builds the DumpCorpus console app, then runs the
# compiled binary directly so that ONLY the corpus JSON object reaches stdout.
#
# Same rationale as scripts/emit-corpus.sh: `dotnet run`/`dotnet build` print
# restore/build chatter to STDOUT, which would corrupt the single JSON object
# the cross-port differ parses. This wrapper routes every byte of MSBuild output
# to stderr and execs the built DLL, whose only stdout write is the JSON.
#
# Usage (from the signalwire-dotnet repo root):
#   bash scripts/dump-corpus.sh <surface>       # surface ∈ wire|swml|state|http|wire-relay|envelope
#   python3 .../diff_port_wire.py --port dotnet \
#       --dump-cmd 'bash scripts/dump-corpus.sh wire' --python-sdk ~/src/signalwire-python
#
# dotnet resolution mirrors scripts/emit-corpus.sh: prefer a host-installed
# dotnet, otherwise fall back to the official SDK docker image.

set -u
set -o pipefail

SURFACE="${1:-}"
if [ -z "$SURFACE" ]; then
    echo "usage: dump-corpus.sh <wire|swml|strict-render|state|http|wire-relay|envelope>" >&2
    exit 2
fi

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/DumpCorpus/DumpCorpus.csproj"

cd "$PORT_ROOT"

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(find tools/DumpCorpus/bin -name DumpCorpus.dll | head -1)"
    if [ -z "$dll" ]; then
        echo "dump-corpus.sh: DumpCorpus.dll not found after build" >&2
        exit 1
    fi
    exec "$dotnet_bin" "$dll" "$SURFACE"
else
    exec docker run --rm \
        --user "$(id -u):$(id -g)" \
        -e HOME=/tmp \
        -v "$PWD:/src" -w /src \
        mcr.microsoft.com/dotnet/sdk:10.0 \
        bash -c '
            set -e
            dotnet build '"$PROJ"' -c Release -v quiet 1>&2
            dll=$(find tools/DumpCorpus/bin -name DumpCorpus.dll | head -1)
            if [ -z "$dll" ]; then
                echo "dump-corpus.sh: DumpCorpus.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll" '"$SURFACE"'
        '
fi
