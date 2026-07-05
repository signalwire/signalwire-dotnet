#!/usr/bin/env bash
# run-tests.sh — canonical test runner for signalwire-dotnet (dotnet test PER-TFM).
#
# The SINGLE entry point for testing; run-ci, agents, and humans all go through
# this (RUN_LINT_FORMAT_SPEC.md). Self-bootstraps the toolchain via
# scripts/_env.sh, so it works from ANY CWD.
#
# IMPORTANT — PER-TFM SERIALIZATION (do NOT change to all-TFM-at-once):
# SignalWire.Tests targets net8.0 + net9.0 + net10.0. `dotnet test
# SignalWire.sln` runs all three target frameworks CONCURRENTLY, and a known
# TLS-listener test deadlocks under that cross-TFM contention (they share one
# mock/listener slot). We therefore loop and run EACH framework SEPARATELY
# (`dotnet test -f net8.0`, then net9.0, then net10.0), which is the serialized
# method that avoids the contention. Exit non-zero if ANY framework fails; every
# framework runs even if an earlier one failed (full signal).
#
# Optional filter: `run-tests.sh <filter>` passes `--filter <filter>` through to
# each per-TFM `dotnet test` (e.g. a test name or `Category=RestCoverage`) so a
# caller can run a subset.
#
# Mock hygiene: the per-test harness self-spawns/reuses the shared mocks and the
# mocks self-terminate on parent death (porting-sdk f1cd024); run-ci owns the
# gate-level mock lifecycle. This script does not pre-spawn mocks.

set -euo pipefail

# shellcheck source=scripts/_env.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

FILTER="${1:-}"

cd "$REPO"
# The TEST path needs the docker fallback (if used) to reach host-spawned mocks.
export DOTNET_DOCKER_NETWORK_HOST=1
DN="$(dotnet_cmd)"

dotnet_restore_if_needed

FRAMEWORKS=(net8.0 net9.0 net10.0)
FAILED=""

for fw in "${FRAMEWORKS[@]}"; do
    echo "==> dotnet test --framework $fw${FILTER:+ --filter $FILTER}"
    args=(test --framework "$fw")
    if [ -n "$FILTER" ]; then
        args+=(--filter "$FILTER")
    fi
    # shellcheck disable=SC2086
    if $DN "${args[@]}"; then
        echo "    $fw ... PASS"
    else
        echo "    $fw ... FAIL"
        FAILED="$FAILED $fw"
    fi
done

if [ -n "$FAILED" ]; then
    echo "==> TESTS FAILED (framework(s):$FAILED )" >&2
    exit 1
fi
echo "==> TESTS PASS (all frameworks:${FRAMEWORKS[*]})"
