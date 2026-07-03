#!/usr/bin/env bash
# rest-test-plan.sh — clean-stdout wrapper around tools/RestTestPlan (the REST
# test-plan capture used by scripts/generate_rest_tests.py). Same pattern as
# scripts/route-registry.sh: MSBuild chatter -> stderr, and ONLY the plan JSON
# reaches stdout by exec'ing the built DLL.
#
# Exit status is the program's own: 0 = plan complete; non-zero = an
# uninvokable/no-request method (plan incomplete) — the generator then refuses.
#
# Usage (from the signalwire-dotnet repo root):
#   bash scripts/rest-test-plan.sh > plan.json

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/RestTestPlan/RestTestPlan.csproj"

cd "$PORT_ROOT"

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(find tools/RestTestPlan/bin -name RestTestPlan.dll | head -1)"
    if [ -z "$dll" ]; then
        echo "rest-test-plan.sh: RestTestPlan.dll not found after build" >&2
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
            dll=$(find tools/RestTestPlan/bin -name RestTestPlan.dll | head -1)
            if [ -z "$dll" ]; then
                echo "rest-test-plan.sh: RestTestPlan.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll"
        '
fi
