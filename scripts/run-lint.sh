#!/usr/bin/env bash
# run-lint.sh — canonical linter for signalwire-dotnet (analyzer build).
#
# The SINGLE entry point for linting; run-ci, agents, and humans all go through
# this (RUN_LINT_FORMAT_SPEC.md). Self-bootstraps the toolchain via
# scripts/_env.sh, so it works from ANY CWD.
#
# Two tools, one bar:
#
#   1. C# — `dotnet build` of EVERY project in the repo with the curated
#      analyzer set on. Directory.Build.props sets EnableNETAnalyzers=true,
#      AnalysisLevel=latest, AnalysisMode=All, TreatWarningsAsErrors=true
#      UNCONDITIONALLY, so a build warning IS a lint violation and the build
#      fails. Scope is the WHOLE tree — src/, tests/, examples/, tools/ and
#      scripts/ (19 projects), enumerated from the .csproj files on disk rather
#      than a solution file, because SignalWire.sln only lists two of them.
#      Building src/SignalWire alone would leave the other 17 unanalysed even
#      with the analyzers on, so the scope is enumerated, not assumed.
#
#   2. Python — `ruff check` over the repo's Python (scripts/enumerate_*.py,
#      scripts/generate_*.py, tests/dotnet_adapter_goldens/run_goldens.py). That
#      code is load-bearing build/audit infrastructure: a defect in it corrupts
#      the artifacts the parity gates compare. Config: ruff.toml (rule selection
#      mirrors the python reference).
#
# Nothing is directory-excluded — there is no third-party vendored source here.
#
# --fix runs ruff's autofixer over the Python; the .NET analyzer build has no
# autofix flow, so for C# it stays report-only (a clean build is the bar).

set -euo pipefail

# shellcheck source=scripts/_env.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

FIX=0
if [ "${1:-}" = "--fix" ]; then
    FIX=1
    shift
elif [ -n "${1:-}" ]; then
    echo "usage: run-lint.sh [--fix]" >&2
    exit 2
fi

cd "$REPO"
DN="$(dotnet_cmd)"
dotnet_restore_if_needed

RC=0

# ── 1. Python ──────────────────────────────────────────────────────────────
if command -v ruff >/dev/null 2>&1; then
    if [ "$FIX" = "1" ]; then
        echo "==> ruff check --fix (whole tree; config ruff.toml)"
        ruff check --fix "$REPO" || RC=1
    else
        echo "==> ruff check (whole tree; config ruff.toml)"
        ruff check "$REPO" || RC=1
    fi
else
    echo "FATAL: 'ruff' not found on PATH — the Python half of this gate cannot run." >&2
    echo "       Install it: pip install ruff  (declared in requirements-dev.txt)." >&2
    exit 1
fi

# ── 2. C# ──────────────────────────────────────────────────────────────────
if [ "$FIX" = "1" ]; then
    echo "    (--fix: the .NET analyzer build has no autofix; running report-only)"
fi
NPROJ="$(dotnet_require_projects)"
echo "==> dotnet build x$NPROJ projects (analyzers, AnalysisMode=All, warnings-as-errors)"

# EVERY other project ProjectReferences src/SignalWire, so they all share its
# obj/ and bin/. Passing --no-incremental to each one in turn therefore WIPES the
# shared library output that the projects built earlier in this loop are still
# referencing, and the build dies with MSB3030 ("could not copy SignalWire.dll,
# it was not found") / CS0006 ("metadata file ... ref/SignalWire.dll could not be
# found"). That is a build-ordering fault in this script, not a code finding — it
# reported ZERO analyzer errors alongside it.
#
# So: force the clean rebuild ONCE, on the shared library, and let the dependent
# projects build against that settled output. They are still fully analyzed —
# --no-incremental controls whether prior outputs are reused, not whether the
# analyzers run.
# `-m:1` is load-bearing: with the default parallel node count MSBuild rebuilds
# the shared SignalWire reference concurrently from several dependent projects and
# they clobber each other's copy of src/SignalWire/bin, failing with MSB3030
# ("could not copy SignalWire.dll, it was not found"). Serial nodes make the
# dependency order deterministic. It costs wall time and buys a gate that reports
# CODE findings instead of build races.
#
# BATCHED (2026-08-05): the above is why this must not be N independent
# invocations, and it is also why ONE solution build is the correct shape rather
# than a compromise. The MSB3030/CS0006 failures came from separate `dotnet
# build` processes each deciding independently to rebuild the shared reference
# and clobbering each other's output. Building the whole solution resolves the
# project graph ONCE and orders the dependencies from it, so the shared library
# is built exactly once, before its dependents, by construction — `-m:1` is
# retained on top of that so the node count cannot reintroduce the race.
#
# Scope is unchanged and asserted: the solution is generated from the same
# dotnet_all_projects enumeration and its project count is checked against it
# (dotnet_all_projects_solution fails loud on a mismatch — `dotnet sln add`
# silently drops duplicate basenames, and this tree has two distinct
# RelayAnswerAndWelcome.csproj). Every project is still built with the same
# analyzers at AnalysisMode=All, warnings-as-errors.
#
# The shared library still gets its --no-incremental clean rebuild first, so the
# analyzers cannot report a stale prior result.
# Measured: 155.5s looped -> 75.0s batched, 0 errors both ways.
SHARED="$REPO/src/SignalWire/SignalWire.csproj"
echo "    -- ${SHARED#"$REPO"/} (clean rebuild; shared by every other project)"
# shellcheck disable=SC2086
$DN build "$SHARED" -c Release --no-incremental -m:1 || RC=1

SLN_ALL="$(dotnet_all_projects_solution)"
echo "    -- all $NPROJ projects (single solution build; graph-ordered, -m:1)"
# shellcheck disable=SC2086
$DN build "$SLN_ALL" -c Release -m:1 || RC=1

exit "$RC"
