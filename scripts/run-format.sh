#!/usr/bin/env bash
# run-format.sh — canonical formatter for signalwire-dotnet (dotnet format).
#
# The SINGLE entry point for formatting; run-ci, agents, and humans all go
# through this (RUN_LINT_FORMAT_SPEC.md). Self-bootstraps the toolchain via
# scripts/_env.sh, so it works from ANY CWD.
#
# Modes:
#   (default) APPLY    — reformat the tree in place; exit 0 even if it changed
#                        files.
#   --check            — VERIFY-ONLY (CI): do not modify; exit non-zero if any
#                        file is unformatted (`dotnet format --verify-no-changes`).
#
# Tool: dotnet format whitespace SignalWire.sln — whitespace/newline house
# style, orthogonal to the LINT gate (analyzers). Formats BOTH hand-written and
# generated code; the generated tree is formatter-clean by construction, so
# --check stays green.

set -euo pipefail

# shellcheck source=scripts/_env.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

MODE="apply"
if [ "${1:-}" = "--check" ]; then
    MODE="check"
elif [ -n "${1:-}" ]; then
    echo "usage: run-format.sh [--check]" >&2
    exit 2
fi

cd "$REPO"
DN="$(dotnet_cmd)"
dotnet_restore_if_needed

if [ "$MODE" = "check" ]; then
    echo "==> dotnet format whitespace --verify-no-changes (VERIFY-ONLY)"
    # shellcheck disable=SC2086
    exec $DN format whitespace "$SLN" --verify-no-changes
else
    echo "==> dotnet format whitespace (APPLY)"
    # shellcheck disable=SC2086
    $DN format whitespace "$SLN"
    if ! git -C "$REPO" diff --quiet 2>/dev/null; then
        echo "    (FMT auto-applied whitespace formatting — review & stage)"
    fi
fi
