#!/usr/bin/env bash
# emit-corpus.sh — clean-stdout wrapper around the .NET EMISSION-DUMP program
# (tools/EmitCorpus). It builds the EmitCorpus console app, then runs the
# compiled binary directly so that ONLY the corpus JSON object reaches stdout.
#
# Why a wrapper (and not just `dotnet run --project tools/EmitCorpus`):
#   `dotnet run` and `dotnet build` print restore/build chatter to STDOUT, which
#   would corrupt the single JSON object the cross-port emission differ
#   (porting-sdk/scripts/diff_port_emission.py) parses from stdout. This wrapper
#   routes every byte of MSBuild output to stderr and execs the built DLL, whose
#   only stdout write is the JSON. See the per-port dump contract in the differ's
#   --help and IDIOM_PASS_JOURNAL.md §4 Tier-0.
#
# Usage (from the signalwire-dotnet repo root):
#   bash scripts/emit-corpus.sh                 # prints {id: emission} JSON
#   python3 .../diff_port_emission.py --dump-cmd 'bash scripts/emit-corpus.sh'
#
# dotnet resolution mirrors scripts/run-ci.sh: prefer a host-installed dotnet
# (CI runners / dev boxes that have it on PATH), otherwise fall back to the
# official SDK docker image (mcr.microsoft.com/dotnet/sdk:10.0). The docker
# fallback uses `--user $(id -u):$(id -g)` + `HOME=/tmp` so build artifacts are
# written as the host user into a writable HOME (same rationale as run-ci.sh).

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/EmitCorpus/EmitCorpus.csproj"

cd "$PORT_ROOT"

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    # Build with all output on stderr; then run the built DLL (clean stdout).
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(find tools/EmitCorpus/bin -name EmitCorpus.dll | head -1)"
    if [ -z "$dll" ]; then
        echo "emit-corpus.sh: EmitCorpus.dll not found after build" >&2
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
            dll=$(find tools/EmitCorpus/bin -name EmitCorpus.dll | head -1)
            if [ -z "$dll" ]; then
                echo "emit-corpus.sh: EmitCorpus.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll"
        '
fi
