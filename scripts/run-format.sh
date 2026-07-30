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
# Two tools, one bar:
#
#   1. C# — `dotnet format whitespace` over EVERY project in the repo
#      (whitespace/newline house style, orthogonal to the LINT gate's
#      analyzers). Scope is enumerated from disk via dotnet_all_projects (see
#      _env.sh): SignalWire.sln lists only TWO of the 19 projects, so formatting
#      the solution left examples/, tools/, scripts/ and the goldens' project
#      unformatted. Formats BOTH hand-written and generated code; the generated
#      tree is formatter-clean by construction, so --check stays green.
#
#   2. Python — `ruff format` over the repo's Python (scripts/enumerate_*.py,
#      scripts/generate_*.py, tests/dotnet_adapter_goldens/run_goldens.py).
#      Config: ruff.toml, which pins stable (non-preview) formatting so local
#      and CI cannot diverge.
#
# Nothing is directory-excluded — there is no third-party vendored source here.

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

if ! command -v ruff >/dev/null 2>&1; then
    echo "FATAL: 'ruff' not found on PATH — the Python half of this gate cannot run." >&2
    echo "       Install it: pip install ruff  (declared in requirements-dev.txt)." >&2
    exit 1
fi

RC=0
NPROJ="$(dotnet_require_projects)"

if [ "$MODE" = "check" ]; then
    echo "==> ruff format --check (whole tree; VERIFY-ONLY)"
    ruff format --check "$REPO" || RC=1

    echo "==> dotnet format whitespace --verify-no-changes x$NPROJ projects (VERIFY-ONLY)"
    while IFS= read -r proj; do
        # shellcheck disable=SC2086
        $DN format whitespace "$proj" --verify-no-changes || RC=1
    done < <(dotnet_all_projects)
else
    echo "==> ruff format (whole tree; APPLY)"
    ruff format "$REPO" || RC=1

    echo "==> dotnet format whitespace x$NPROJ projects (APPLY)"
    while IFS= read -r proj; do
        # shellcheck disable=SC2086
        $DN format whitespace "$proj" || RC=1
    done < <(dotnet_all_projects)

    if ! git -C "$REPO" diff --quiet 2>/dev/null; then
        echo "    (FMT auto-applied formatting — review & stage)"
    fi
fi

exit "$RC"
