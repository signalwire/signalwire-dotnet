#!/usr/bin/env bash
# emit-skills.sh — clean-stdout wrapper around the .NET SKILL-DUMP program
# (tools/EmitSkills), the sibling of emit-corpus.sh for the SKILL-CONTRACT gate.
#
# It builds the EmitSkills console app, then runs the compiled binary directly
# so that ONLY the skill-contract JSON object reaches stdout. The cross-port
# differ (porting-sdk/scripts/diff_skill_contracts.py) parses that single object
# from stdout, so all MSBuild build chatter is routed to stderr (same rationale
# and host-or-docker fallback as emit-corpus.sh).
#
# Usage (from the signalwire-dotnet repo root):
#   bash scripts/emit-skills.sh
#   python3 .../diff_skill_contracts.py --dump-cmd 'bash scripts/emit-skills.sh' --port-repo .
#
# EmitSkills shells out to python3 to read the shared corpus
# (porting-sdk/scripts/skill_contract_corpus.py); $PORTING_SDK is forwarded so
# it resolves in CI (where porting-sdk is a workspace sibling, not ~/src).

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/EmitSkills/EmitSkills.csproj"

cd "$PORT_ROOT"

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(find tools/EmitSkills/bin -name EmitSkills.dll | head -1)"
    if [ -z "$dll" ]; then
        echo "emit-skills.sh: EmitSkills.dll not found after build" >&2
        exit 1
    fi
    exec "$dotnet_bin" "$dll"
else
    exec docker run --rm \
        --user "$(id -u):$(id -g)" \
        -e HOME=/tmp \
        -e PORTING_SDK="${PORTING_SDK:-}" \
        -v "$PWD:/src" -w /src \
        mcr.microsoft.com/dotnet/sdk:10.0 \
        bash -c '
            set -e
            dotnet build '"$PROJ"' -c Release -v quiet 1>&2
            dll=$(find tools/EmitSkills/bin -name EmitSkills.dll | head -1)
            if [ -z "$dll" ]; then
                echo "emit-skills.sh: EmitSkills.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll"
        '
fi
