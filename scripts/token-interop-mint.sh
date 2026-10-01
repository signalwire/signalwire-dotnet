#!/usr/bin/env bash
# token-interop-mint.sh — clean-stdout wrapper around tools/TokenInteropMint, the
# TOKEN-INTEROP mint fixture for the cross-port checker
# (porting-sdk/scripts/diff_port_token_interop.py). MSBuild chatter -> stderr; ONLY the
# minted token reaches stdout by exec'ing the built DLL.
#
# The checker exports the FIXED mint inputs (SW_TOKEN_INTEROP_SECRET_KEY / _CALL_ID /
# _FUNCTION_NAME) into our environment; the fixture reads them and fails loud if absent,
# so this wrapper only has to keep stdout clean and pass the environment through.
#
# Newest-by-mtime DLL selection (NOT `find | head`): never run a stale DLL from an
# earlier build — pick the most recently built one (the route-registry.sh trap).
#
# Usage (invoked by the checker with --mint-cmd):
#   bash scripts/token-interop-mint.sh

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/TokenInteropMint/TokenInteropMint.csproj"

cd "$PORT_ROOT"

newest_dll() {
    find tools/TokenInteropMint/bin -name TokenInteropMint.dll -print0 2>/dev/null \
        | xargs -0 ls -t 2>/dev/null | head -1
}

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(newest_dll)"
    if [ -z "$dll" ]; then
        echo "token-interop-mint.sh: TokenInteropMint.dll not found after build" >&2
        exit 1
    fi
    exec "$dotnet_bin" "$dll"
else
    exec docker run --rm \
        --user "$(id -u):$(id -g)" \
        -e HOME=/tmp \
        -e SW_TOKEN_INTEROP_SECRET_KEY \
        -e SW_TOKEN_INTEROP_CALL_ID \
        -e SW_TOKEN_INTEROP_FUNCTION_NAME \
        -v "$PWD:/src" -w /src \
        mcr.microsoft.com/dotnet/sdk:10.0 \
        bash -c '
            set -e
            dotnet build '"$PROJ"' -c Release -v quiet 1>&2
            dll=$(find tools/TokenInteropMint/bin -name TokenInteropMint.dll -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -1)
            if [ -z "$dll" ]; then
                echo "token-interop-mint.sh: TokenInteropMint.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll"
        '
fi
