#!/usr/bin/env bash
# run-lint.sh — canonical linter for signalwire-dotnet (analyzer build).
#
# The SINGLE entry point for linting; run-ci, agents, and humans all go through
# this (RUN_LINT_FORMAT_SPEC.md). Self-bootstraps the toolchain via
# scripts/_env.sh, so it works from ANY CWD.
#
# Tool: `dotnet build` of the shipped library with the curated analyzer set on.
# Directory.Build.props sets EnableNETAnalyzers=true, AnalysisMode=All,
# TreatWarningsAsErrors=true across net8/9/10 — so a build warning IS a lint
# violation and the build fails. Builds src/SignalWire only (that is the shipped
# surface where analyzers run; tests/examples/tools are not linted here).
#
# The .NET analyzer build has no autofix flow, so --fix is accepted for
# cross-port CLI symmetry but is a report-only no-op (a clean build is the bar).

set -euo pipefail

# shellcheck source=scripts/_env.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

if [ "${1:-}" = "--fix" ]; then
    echo "    (--fix: the .NET analyzer build has no autofix; running report-only)"
    shift
elif [ -n "${1:-}" ]; then
    echo "usage: run-lint.sh [--fix]" >&2
    exit 2
fi

cd "$REPO"
DN="$(dotnet_cmd)"
dotnet_restore_if_needed

echo "==> dotnet build (analyzers, AnalysisMode=All, warnings-as-errors)"
# shellcheck disable=SC2086
exec $DN build src/SignalWire/SignalWire.csproj -c Release --no-incremental
