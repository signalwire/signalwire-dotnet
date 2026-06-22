#!/usr/bin/env bash
# route-registry.sh — clean-stdout wrapper around the .NET REST route-registry
# program (tools/RouteRegistry). It builds the console app, then runs the
# compiled binary directly so that ONLY the registry JSON reaches stdout.
#
# Why a wrapper (and not just `dotnet run --project tools/RouteRegistry`):
#   `dotnet run` / `dotnet build` print restore/build chatter to STDOUT, which
#   would corrupt the single JSON object the SPEC-PARITY differ
#   (porting-sdk/scripts/diff_spec_implementation.py) parses from stdout via
#   --registry-json. This wrapper routes every byte of MSBuild output to stderr
#   and execs the built DLL, whose only stdout write is the JSON.
#
# Exit status is the program's own: 0 = Set B complete; non-zero = an
# uninvokable/no-request method (Set B incomplete) — the differ then refuses
# the file rather than diffing a partial Set B.
#
# Usage (from the signalwire-dotnet repo root):
#   bash scripts/route-registry.sh > registry.json
#
# dotnet resolution mirrors scripts/emit-corpus.sh / run-ci.sh: prefer a
# host-installed dotnet, otherwise fall back to the official SDK docker image.

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/RouteRegistry/RouteRegistry.csproj"

cd "$PORT_ROOT"

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(find tools/RouteRegistry/bin -name RouteRegistry.dll | head -1)"
    if [ -z "$dll" ]; then
        echo "route-registry.sh: RouteRegistry.dll not found after build" >&2
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
            dll=$(find tools/RouteRegistry/bin -name RouteRegistry.dll | head -1)
            if [ -z "$dll" ]; then
                echo "route-registry.sh: RouteRegistry.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll"
        '
fi
