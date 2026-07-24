#!/usr/bin/env bash
# ai-chat-dump.sh — clean-stdout wrapper around tools/AIChatDump, the AI-CHAT dump
# program for the cross-port AI-CHAT wire-behavioral gate
# (porting-sdk/scripts/diff_port_ai_chat.py). MSBuild chatter -> stderr; ONLY the
# ONE JSON object the gate parses reaches stdout by exec'ing the built DLL.
#
# Newest-by-mtime DLL selection (NOT `find | head`): never run a stale DLL from an
# earlier build — pick the most recently built one (the route-registry.sh trap).
#
# The gate exports MOCK_AI_CHAT_URL + SIGNALWIRE_PROJECT_ID / SIGNALWIRE_API_TOKEN;
# this wrapper passes the env through untouched.
#
# Usage (invoked by the gate with --dump-cmd):
#   bash scripts/ai-chat-dump.sh

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="tools/AIChatDump/AIChatDump.csproj"

cd "$PORT_ROOT"

newest_dll() {
    find tools/AIChatDump/bin -name AIChatDump.dll -print0 2>/dev/null \
        | xargs -0 ls -t 2>/dev/null | head -1
}

dotnet_bin="$(command -v dotnet || true)"

if [ -n "$dotnet_bin" ]; then
    "$dotnet_bin" build "$PROJ" -c Release -v quiet 1>&2 || exit 1
    dll="$(newest_dll)"
    if [ -z "$dll" ]; then
        echo "ai-chat-dump.sh: AIChatDump.dll not found after build" >&2
        exit 1
    fi
    exec "$dotnet_bin" "$dll"
else
    exec docker run --rm \
        --user "$(id -u):$(id -g)" \
        -e HOME=/tmp \
        -e MOCK_AI_CHAT_URL \
        -e SIGNALWIRE_PROJECT_ID \
        -e SIGNALWIRE_API_TOKEN \
        -v "$PWD:/src" -w /src \
        mcr.microsoft.com/dotnet/sdk:10.0 \
        bash -c '
            set -e
            dotnet build '"$PROJ"' -c Release -v quiet 1>&2
            dll=$(find tools/AIChatDump/bin -name AIChatDump.dll -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -1)
            if [ -z "$dll" ]; then
                echo "ai-chat-dump.sh: AIChatDump.dll not found after build" >&2
                exit 1
            fi
            dotnet "$dll"
        '
fi
