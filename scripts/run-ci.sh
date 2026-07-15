#!/usr/bin/env bash
# run-ci.sh — canonical local-and-CI gate runner for signalwire-dotnet.
#
# Same script invoked locally (`bash scripts/run-ci.sh`) AND by the
# GitHub Actions workflow. No drift between local and CI behavior.
#
# `dotnet` is not on host PATH in CI. We use `docker run` with the official SDK
# image (mcr.microsoft.com/dotnet/sdk:10.0) where dotnet is absent locally.
#
# FMT / LINT / TEST are the three CANONICAL wrapper scripts (they self-bootstrap
# the toolchain via scripts/_env.sh and run from ANY CWD — RUN_LINT_FORMAT_SPEC):
#   FMT  -> scripts/run-format.sh  (dotnet format; --check in CI)
#   LINT -> scripts/run-lint.sh    (dotnet build, AnalysisMode=All, warn-as-error)
#   TEST -> scripts/run-tests.sh   (dotnet test PER-TFM: net8/9/10 serialized)
#
# GATE SCHEDULING (porting-sdk/scripts/gate_scheduler.sh — CI_PERF S1 + S2):
#   Gates run CONCURRENTLY up to a cap (SW_CI_JOBS, default nproc), scheduled by
#   their DATA dependencies:
#     * S2 concurrent wave: the pure-Python side-effect-free gates (GEN-FRESH*,
#       DRIFT, NO-CHEAT, EMISSION, SKILL-CONTRACT, SWAIG-COVERAGE, SURFACE-DIFF,
#       DOC-AUDIT, SWAIG-CLI) overlap — they share no mutable state.
#     * S1 fail-fast: heavy gates (TEST, LINT, FMT, REST-COVERAGE, SPEC-PARITY) are
#       deferred behind the cheap wave, so a trivial cheap-gate failure surfaces in
#       seconds; --fail-fast aborts the run before the docker-based TEST starts.
#   HARD ordering is data-dependency ONLY:
#     * DRIFT reads port_signatures.json that SIGNATURES writes → deps=SIGNATURES.
#     * SURFACE-FRESH regenerates port_surface.json in place (and restores it);
#       DOC-AUDIT + SURFACE-DIFF read it → all three share res=surface.
#     * The five heavy gates all drive MSBuild over shared bin/obj outputs
#       (TEST+REST-COVERAGE: tests Debug; LINT+SPEC-PARITY: src Release; FMT:
#       sln restore + local source rewrite) → all five share res=msbuild
#       (see the comment at the TEST gate for the concrete race).
#   The host mock-server lifecycle (mock_signalwire + mock_relay, for the docker
#   TEST gate reached via --network host) is stood up BEFORE scheduling and torn
#   down in an EXIT trap, exactly as before.
#   Per-gate PASS/FAIL + the FAILED_GATES tally preserved exactly; each gate's output
#   captured + replayed atomically.
#
# Multi-target serialization for TEST (net8→net9→net10) is owned by run-tests.sh.
#
# Flags:
#   --fail-fast   stop launching new gates at the first failure (local dev loop).

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$PORT_ROOT/.sw-tmp"  # repo-local CI scratch (never /tmp)
PORT_NAME="signalwire-dotnet"

resolve_porting_sdk() {
    if [ -n "${PORTING_SDK:-}" ] && [ -d "$PORTING_SDK/scripts" ]; then
        echo "$PORTING_SDK"
        return 0
    fi
    if [ -d "$PORT_ROOT/../porting-sdk/scripts" ]; then
        (cd "$PORT_ROOT/../porting-sdk" && pwd)
        return 0
    fi
    return 1
}

PORTING_SDK_DIR="$(resolve_porting_sdk)" || {
    echo "FATAL: porting-sdk not found, clone it adjacent to this repo" >&2
    echo "       (expected $PORT_ROOT/../porting-sdk or \$PORTING_SDK env var)" >&2
    exit 2
}

# The signalwire-python reference SDK. The Layer-D BEHAVIORAL-* differs must be
# told where it lives via --python-sdk (unlike diff_port_emission.py they have no
# ~/src fallback). Resolve it CI-portably the same way as porting-sdk: env-var
# override, else sibling checkout (the cross-port workflow clones it adjacent).
resolve_python_sdk() {
    if [ -n "${PYTHON_SDK:-}" ] && [ -d "$PYTHON_SDK/signalwire" ]; then
        echo "$PYTHON_SDK"
        return 0
    fi
    if [ -d "$PORT_ROOT/../signalwire-python/signalwire" ]; then
        (cd "$PORT_ROOT/../signalwire-python" && pwd)
        return 0
    fi
    return 1
}

PYTHON_SDK_DIR="$(resolve_python_sdk)" || {
    echo "FATAL: signalwire-python not found, clone it adjacent to this repo" >&2
    echo "       (expected $PORT_ROOT/../signalwire-python or \$PYTHON_SDK env var)" >&2
    exit 2
}

SPAWNED_PIDS=()

# ---------------------------------------------------------------------------
# Mock-server lifecycle (probe-then-spawn, trap-cleaned on exit)
# ---------------------------------------------------------------------------
pick_free_port() {
    python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()'
}

MOCK_SIGNALWIRE_PORT="${MOCK_SIGNALWIRE_PORT:-$(pick_free_port)}"
MOCK_RELAY_WS_PORT="${MOCK_RELAY_PORT:-$(pick_free_port)}"
MOCK_RELAY_HTTP_PORT="${MOCK_RELAY_HTTP_PORT:-$(pick_free_port)}"
export MOCK_SIGNALWIRE_PORT MOCK_RELAY_WS_PORT MOCK_RELAY_HTTP_PORT

probe_health() {
    local url="$1"
    curl -fsS --max-time 2 "$url/__mock__/health" >/dev/null 2>&1
}

wait_for_health() {
    local url="$1"
    local deadline=$(( $(date +%s) + 30 ))
    while [ "$(date +%s)" -lt "$deadline" ]; do
        if probe_health "$url"; then return 0; fi
        sleep 0.25
    done
    return 1
}

ensure_mock_signalwire() {
    local url="http://127.0.0.1:${MOCK_SIGNALWIRE_PORT}"
    if probe_health "$url"; then
        echo "    mock_signalwire: already running on $url"
        return 0
    fi
    echo "    mock_signalwire: spawning on $url ..."
    (
        cd "$PORTING_SDK_DIR/test_harness/mock_signalwire"
        PYTHONPATH="$PWD${PYTHONPATH:+:$PYTHONPATH}" \
            python3 -m mock_signalwire --host 127.0.0.1 \
                --port "$MOCK_SIGNALWIRE_PORT" --log-level error
    ) >"$PORT_ROOT/.sw-tmp/mock_signalwire_dotnet_ci.log" 2>&1 &
    SPAWNED_PIDS+=("$!")
    if ! wait_for_health "$url"; then
        echo "FATAL: mock_signalwire failed to start; log $PORT_ROOT/.sw-tmp/mock_signalwire_dotnet_ci.log" >&2
        return 1
    fi
}

ensure_mock_relay() {
    local url="http://127.0.0.1:${MOCK_RELAY_HTTP_PORT}"
    if probe_health "$url"; then
        echo "    mock_relay: already running on $url"
        return 0
    fi
    echo "    mock_relay: spawning on $url (ws=${MOCK_RELAY_WS_PORT}) ..."
    (
        cd "$PORTING_SDK_DIR/test_harness/mock_relay"
        PYTHONPATH="$PWD${PYTHONPATH:+:$PYTHONPATH}" \
            python3 -m mock_relay --host 127.0.0.1 \
                --ws-port "$MOCK_RELAY_WS_PORT" \
                --http-port "$MOCK_RELAY_HTTP_PORT" --log-level error
    ) >"$PORT_ROOT/.sw-tmp/mock_relay_dotnet_ci.log" 2>&1 &
    SPAWNED_PIDS+=("$!")
    if ! wait_for_health "$url"; then
        echo "FATAL: mock_relay failed to start; log $PORT_ROOT/.sw-tmp/mock_relay_dotnet_ci.log" >&2
        return 1
    fi
}

cleanup_spawned() {
    local pid
    [ ${#SPAWNED_PIDS[@]} -eq 0 ] && return 0
    for pid in "${SPAWNED_PIDS[@]}"; do
        if kill -0 "$pid" 2>/dev/null; then
            kill "$pid" 2>/dev/null || true
            wait "$pid" 2>/dev/null || true
        fi
    done
}
trap cleanup_spawned EXIT INT TERM

# shellcheck source=/dev/null
source "$PORTING_SDK_DIR/scripts/gate_scheduler.sh"

# ---- gate helper functions ---------------------------------------------------

# TEST — the canonical per-TFM (net8→net9→net10 serial) runner. run-ci owns the
# mock lifecycle (ports picked + exported, mocks spawned before sched_run). The
# fixtures read MOCK_RELAY_PORT (WS) — our internal WS var is MOCK_RELAY_WS_PORT —
# so rename it into MOCK_RELAY_PORT before invoking the script.
dotnet_test_per_framework() {
    MOCK_SIGNALWIRE_PORT="$MOCK_SIGNALWIRE_PORT" \
    MOCK_RELAY_PORT="$MOCK_RELAY_WS_PORT" \
    MOCK_RELAY_HTTP_PORT="$MOCK_RELAY_HTTP_PORT" \
        bash "$PORT_ROOT/scripts/run-tests.sh"
}

# SURFACE-FRESH — regenerate port_surface.json in place (pure-regex parse of
# src/**/*.cs; no docker/build), compare modulo the generated_from git-sha, restore.
surface_fresh_gate() {
    local committed="$PORT_ROOT/.sw-tmp/committed_surface.json"
    git show HEAD:port_surface.json > "$committed" 2>/dev/null \
        || cp port_surface.json "$committed"
    python3 scripts/enumerate_surface.py
    local rc=$?
    if [ "$rc" -eq 0 ]; then
        python3 "$PORTING_SDK_DIR/scripts/check_surface_freshness.py" \
            --committed "$committed" --fresh port_surface.json
        rc=$?
    fi
    git checkout -- port_surface.json
    return $rc
}

# Resolve a dotnet invocation: host dotnet if present, else the SDK docker image.
dotnet_cmd() {
    local bin
    bin="$(command -v dotnet || true)"
    if [ -n "$bin" ]; then
        echo "$bin"
    else
        echo "docker run --rm --user $(id -u):$(id -g) -e HOME=/tmp -v $PWD:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet"
    fi
}

fmt_gate() {
    bash "$PORT_ROOT/scripts/run-format.sh" ${CI:+--check}
}

lint_gate() {
    bash "$PORT_ROOT/scripts/run-lint.sh"
}

# REST-COVERAGE — spins its own dedicated mock, runs ONLY the RestCoverage-trait
# tests on net8.0 into one journal, then checks it.
rest_coverage_gate() {
    local port="${REST_COVERAGE_PORT:-$(pick_free_port)}"
    [ -n "$port" ] || { echo "could not allocate a free port" >&2; return 1; }
    local url="http://127.0.0.1:${port}"
    local mock_pkg_parent="$PORTING_SDK_DIR/test_harness/mock_signalwire"
    (
        cd "$mock_pkg_parent"
        PYTHONPATH="$PWD${PYTHONPATH:+:$PYTHONPATH}" \
            python3 -m mock_signalwire --host 127.0.0.1 --port "$port" --log-level error
    ) >"$PORT_ROOT/.sw-tmp/rest_cov_mock_dotnet.$$.log" 2>&1 &
    local mock_pid=$!
    # shellcheck disable=SC2064
    trap "kill $mock_pid 2>/dev/null" RETURN
    local i ready=0
    for i in $(seq 1 60); do
        if ! kill -0 "$mock_pid" 2>/dev/null; then
            echo "mock_signalwire died on port $port — log:" >&2
            cat "$PORT_ROOT/.sw-tmp/rest_cov_mock_dotnet.$$.log" >&2
            return 1
        fi
        if curl -fsS --max-time 1 "$url/__mock__/health" >/dev/null 2>&1; then ready=1; break; fi
        sleep 0.5
    done
    if [ "$ready" -ne 1 ]; then
        echo "mock_signalwire on port $port not healthy within 30s" >&2
        return 1
    fi
    curl -fsS --max-time 5 -X POST "$url/__mock__/journal/reset" >/dev/null 2>&1

    local dn
    dn="$(command -v dotnet || true)"
    if [ -n "$dn" ]; then
        MOCK_SIGNALWIRE_PORT="$port" "$dn" test --framework net8.0 \
            --filter "Category=RestCoverage" || return 1
    else
        MOCK_SIGNALWIRE_PORT="$port" docker run --rm --network host \
            --user "$(id -u):$(id -g)" -e HOME=/tmp \
            -e MOCK_SIGNALWIRE_PORT="$port" \
            -v "$PWD:/src" -w /src \
            mcr.microsoft.com/dotnet/sdk:10.0 \
            dotnet test --framework net8.0 --filter "Category=RestCoverage" || return 1
    fi

    python3 -m mock_signalwire.rest_coverage \
        --mock-url "$url" \
        --spec-root "$PORTING_SDK_DIR/rest-apis" \
        --allowlist "$PORTING_SDK_DIR/REST_COVERAGE_BASELINE.md" \
        --allowlist "$PORT_ROOT/REST_COVERAGE_GAPS.md" \
        --gap-baseline "$PORTING_SDK_DIR/REST_COVERAGE_GAP_BASELINE.md"
}

# SPEC-PARITY — implemented routes == canonical spec. tools/RouteRegistry drives the
# live RestClient through a recording transport and captures every dispatched route.
spec_parity_gate() {
    local registry
    registry="$(mktemp)"
    if ! bash "$PORT_ROOT/scripts/route-registry.sh" >"$registry" 2>/dev/null; then
        echo "route-registry emitted an incomplete Set B (uninvokable/no-request method)" >&2
        rm -f "$registry"
        return 1
    fi
    python3 "$PORTING_SDK_DIR/scripts/diff_spec_implementation.py" \
        --registry-json "$registry" \
        --gaps "$PORTING_SDK_DIR/SPEC_IMPLEMENTATION_GAPS.md"
    local rc=$?
    rm -f "$registry"
    return $rc
}

# ROUTE-COLLISION — cross-references the port's route-registry (operation ->
# (method, path)) with its surface enumeration to find route-split / crud-dup /
# orphan-dto latent defects. dotnet HAS a registry (the SAME tools/RouteRegistry
# program SPEC-PARITY drives), so the gate runs with --registry-json; the surface
# defaults to <repo>/port_surface.json. Any approved ROUTE_COLLISION_ALLOW.md
# entries are honored by the gate (dotnet currently has zero splits, so none).
route_collision_gate() {
    local registry
    registry="$(mktemp)"
    if ! bash "$PORT_ROOT/scripts/route-registry.sh" >"$registry" 2>/dev/null; then
        echo "route-registry emitted an incomplete Set B (uninvokable/no-request method)" >&2
        rm -f "$registry"
        return 1
    fi
    python3 "$PORTING_SDK_DIR/scripts/route_collision.py" \
        --port dotnet --repo "$PORT_ROOT" \
        --registry-json "$registry"
    local rc=$?
    rm -f "$registry"
    return $rc
}

# SWAIG-CLI — the lightweight shared swaig-test mini-contract. bin/swaig-test is a
# dotnet-script; provision dotnet-script as a tool-path tool, then run it.
swaig_cli_gate() {
    local dn
    dn="$(dotnet_cmd)"
    local toolroot="$PORT_ROOT/.dotnet-tools"
    if [ ! -x "$toolroot/dotnet-script" ]; then
        $dn tool install dotnet-script --tool-path "$toolroot" >/dev/null 2>&1 || true
    fi
    PATH="$toolroot:$PATH" \
    python3 "$PORTING_SDK_DIR/scripts/audit_swaig_cli_contract.py" \
        --port dotnet \
        --cmd "dotnet script $PORT_ROOT/bin/swaig-test --" \
        --require-url-model \
        --default-action-argv='--url|http://user:pass@127.0.0.1:1/' \
        --no-serverless-argv='--url|http://user:pass@127.0.0.1:1/|--simulate-serverless|lambda|--list-tools'
}

# ARTIFACT-DENY — authoritative --listing mode. Pack the real NuGet package into
# a repo-local scratch dir, list its contents with `unzip -l`, and feed that file
# listing to artifact_deny.py. This is the PUBLISHED set (respects .csproj
# pack/include rules), not the git-ls-files proxy which over-reports repo files
# excluded from the package.
dayone_artifact_deny() {
    local pkgdir="$PORT_ROOT/.sw-tmp/artifact-deny-pkg"
    rm -rf "$pkgdir"
    mkdir -p "$pkgdir"
    local dn
    dn="$(dotnet_cmd)"
    if ! $dn pack "$PORT_ROOT/src/SignalWire/SignalWire.csproj" -c Release \
            -o "$pkgdir" >"$PORT_ROOT/.sw-tmp/artifact-deny-pack.log" 2>&1; then
        echo "dotnet pack failed — log:" >&2
        cat "$PORT_ROOT/.sw-tmp/artifact-deny-pack.log" >&2
        return 1
    fi
    local nupkg
    nupkg="$(ls "$pkgdir"/*.nupkg 2>/dev/null | head -1)"
    if [ -z "$nupkg" ]; then
        echo "dotnet pack produced no .nupkg in $pkgdir" >&2
        return 1
    fi
    # `unzip -Z1` prints ONE clean archive path per line (no header/footer/size
    # columns), which is exactly what artifact_deny's --listing parser wants.
    # (Plain `unzip -l` prefixes each line with Length/Date/Time columns, so a
    # root-level artifact like `port_signatures.json` would fail the split("/")
    # basename match and be silently missed — `-Z1` is the authoritative form.)
    unzip -Z1 "$nupkg" \
        | python3 "$PORTING_SDK_DIR/scripts/artifact_deny.py" \
            --port dotnet --repo "$PORT_ROOT" --listing -
}

cd "$PORT_ROOT"

echo "==> running CI gates for $PORT_NAME (porting-sdk at $PORTING_SDK_DIR)"

echo "==> ensuring mock servers are running on host"
ensure_mock_signalwire || exit 2
ensure_mock_relay || exit 2

# Pre-build the Layer-D DumpCorpus tool ONCE before scheduling. dump-corpus.sh
# also builds on each call, but the 5 BEHAVIORAL-* gates share res=behavioral so
# they serialize; building here first makes each gate's build a no-op incremental
# and guarantees the tool exists before any gate runs (no concurrent build race
# on tools/DumpCorpus/bin). Route all MSBuild output to stderr; if dotnet is
# absent locally the gates fall back to docker in the wrapper, so skip the
# host pre-build in that case.
echo "==> pre-building Layer-D DumpCorpus tool"
if command -v dotnet >/dev/null 2>&1; then
    dotnet build "$PORT_ROOT/tools/DumpCorpus/DumpCorpus.csproj" -c Release -v quiet 1>&2 \
        || { echo "FATAL: DumpCorpus pre-build failed" >&2; exit 2; }
fi

# ---- register gates ----------------------------------------------------------
sched_init "$@"

# res=msbuild — the five deferred heavy gates each drive MSBuild over the SAME
# project outputs, and two concurrent MSBuild processes race on shared bin/obj
# files (a shared MUTABLE FILE is a data dependency, per the scheduler doctrine):
#   * TEST + REST-COVERAGE both `dotnet test` the sln → both build
#     SignalWire.Tests Debug/net8.0 into the same tests/bin+obj;
#     GenerateRuntimeConfigurationFiles writes runtimeconfig.json with NO retry,
#     so the overlap intermittently dies with IOException "file is being used by
#     another process" (seen in the cross-port matrix 2026-07-08).
#   * LINT (src Release, --no-incremental forced rewrite) + SPEC-PARITY (builds
#     tools/RouteRegistry Release, whose ProjectReference rebuilds SignalWire
#     Release) share src/SignalWire/bin+obj/Release.
#   * FMT restores the sln (cold tests/obj writes) and locally runs in APPLY
#     mode, rewriting source files while the other gates compile them.
# One shared resource label serializes all five, making the collision
# structurally impossible; REST-COVERAGE then builds warm after TEST, so the
# added wall time is mostly just its own test execution.
sched_gate TEST defer=1 res=msbuild desc="docker dotnet test (net8/net9/net10 sequential)" \
    --fn dotnet_test_per_framework

sched_gate SIGNATURES desc="regenerate port_signatures.json" \
    -- python3 scripts/enumerate_signatures.py

sched_gate DRIFT deps=SIGNATURES desc="diff_port_signatures vs python reference" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_signatures.py" \
        --reference "$PORTING_SDK_DIR/python_signatures.json" \
        --port-signatures "$PORT_ROOT/port_signatures.json" \
        --surface-omissions "$PORT_ROOT/PORT_OMISSIONS.md" \
        --surface-additions "$PORT_ROOT/PORT_ADDITIONS.md" \
        --omissions "$PORT_ROOT/PORT_SIGNATURE_OMISSIONS.md"

sched_gate SURFACE-FRESH res=surface desc="check_surface_freshness vs regenerated port_surface.json" \
    --fn surface_fresh_gate

sched_gate GEN-FRESH desc="generate_rest.py --check (generated REST layer matches specs)" \
    -- python3 scripts/generate_rest.py --check

sched_gate GEN-FRESH-TESTS desc="generate_rest_tests.py --check (generated REST wire tests match specs)" \
    -- python3 scripts/generate_rest_tests.py --check

sched_gate GEN-FRESH-RELAY desc="generate_relay_protocol.py --check (generated RELAY types match relay-protocol)" \
    -- python3 scripts/generate_relay_protocol.py --check

sched_gate GEN-FRESH-SWAIG desc="generate_swaig_payloads.py --check (generated SWAIG payloads match swaig-specs)" \
    -- python3 scripts/generate_swaig_payloads.py --check

sched_gate GEN-FRESH-SWML desc="generate_swml_verbs.py --check (generated SWML-verb types match schema.json)" \
    -- python3 scripts/generate_swml_verbs.py --check

sched_gate NO-CHEAT desc="audit_no_cheat_tests" \
    -- python3 "$PORTING_SDK_DIR/scripts/audit_no_cheat_tests.py" --root "$PORT_ROOT"

sched_gate REST-COVERAGE defer=1 res=msbuild desc="every implemented REST route covered success+error (parity + allowlist)" \
    --fn rest_coverage_gate

sched_gate SPEC-PARITY defer=1 res=msbuild desc="implemented routes == canonical spec (modulo SPEC_IMPLEMENTATION_GAPS.md)" \
    --fn spec_parity_gate

sched_gate EMISSION desc="diff_port_emission vs python to_dict()" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_emission.py" \
        --dump-cmd "bash $PORT_ROOT/scripts/emit-corpus.sh"

# ---- Layer D: BEHAVIORAL coverage --------------------------------------------
# Diff the .NET port's runtime behavior (tools/DumpCorpus, one surface each) vs
# the python oracle. The dump wrapper routes all MSBuild chatter to stderr and
# writes ONLY the corpus JSON to stdout, so the differ parses a clean object.
sched_gate BEHAVIORAL-WIRE res=behavioral desc="diff_port_wire vs python oracle (Layer D)" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_wire.py" \
        --port dotnet --python-sdk "$PYTHON_SDK_DIR" \
        --dump-cmd "bash $PORT_ROOT/scripts/dump-corpus.sh wire"

sched_gate BEHAVIORAL-SWML res=behavioral desc="diff_port_swml vs python oracle (Layer D)" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_swml.py" \
        --port dotnet --python-sdk "$PYTHON_SDK_DIR" \
        --dump-cmd "bash $PORT_ROOT/scripts/dump-corpus.sh swml"

sched_gate BEHAVIORAL-STATE res=behavioral desc="diff_port_state vs python oracle (Layer D)" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_state.py" \
        --port dotnet --python-sdk "$PYTHON_SDK_DIR" \
        --dump-cmd "bash $PORT_ROOT/scripts/dump-corpus.sh state"

sched_gate BEHAVIORAL-HTTP res=behavioral desc="diff_port_http vs python oracle (Layer D)" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_http.py" \
        --port dotnet --python-sdk "$PYTHON_SDK_DIR" \
        --dump-cmd "bash $PORT_ROOT/scripts/dump-corpus.sh http"

sched_gate BEHAVIORAL-WIRE-RELAY res=behavioral desc="diff_port_wire_relay vs python oracle (Layer D)" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_wire_relay.py" \
        --port dotnet --python-sdk "$PYTHON_SDK_DIR" \
        --dump-cmd "bash $PORT_ROOT/scripts/dump-corpus.sh wire-relay"

sched_gate FMT defer=1 res=msbuild desc="dotnet format whitespace (local: auto-fix; CI: --verify)" \
    --fn fmt_gate

sched_gate LINT defer=1 res=msbuild desc="dotnet build (analyzers, warnings-as-errors)" \
    --fn lint_gate

# DOC-AUDIT resolves doc/example refs against the CANONICAL surface AND the native
# sidecar (port_surface_native.json — the real C# member names, Async suffix intact).
# The sidecar lets a genuinely-present async member (call.AnswerAsync()) resolve while
# a phantom (Action StopAsync, only sync Stop exists) stays unresolved. Idiom via the
# enumerator, not a doc omission (RULES §2).
sched_gate DOC-AUDIT res=surface desc="audit_docs vs port_surface.json (+native sidecar)" \
    -- python3 "$PORTING_SDK_DIR/scripts/audit_docs.py" \
        --root "$PORT_ROOT" \
        --surface "$PORT_ROOT/port_surface.json" \
        --native-names "$PORT_ROOT/port_surface_native.json" \
        --ignore "$PORT_ROOT/DOC_AUDIT_IGNORE.md"

sched_gate SURFACE-DIFF res=surface desc="diff_port_surface vs python_surface.json" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_surface.py" \
        --reference "$PORTING_SDK_DIR/python_surface.json" \
        --port-surface "$PORT_ROOT/port_surface.json" \
        --omissions "$PORT_ROOT/PORT_OMISSIONS.md" \
        --additions "$PORT_ROOT/PORT_ADDITIONS.md"

sched_gate SKILL-CONTRACT desc="diff_skill_contracts vs python reference" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_skill_contracts.py" \
        --dump-cmd "bash $PORT_ROOT/scripts/emit-skills.sh" \
        --port-repo "$PORT_ROOT"

sched_gate SWAIG-CLI desc="swaig-test shared mini-contract (verbs/serverless-reject/default-action)" \
    --fn swaig_cli_gate

sched_gate SWAIG-COVERAGE desc="FunctionResult emits every engine action (or allowlisted)" \
    -- python3 "$PORTING_SDK_DIR/scripts/swaig_coverage.py" \
        --check \
        --emission "$PORT_ROOT/src/SignalWire/SWAIG/FunctionResult.cs"

# ---- §C1 doc/example/CLI execution gates -------------------------------------
# SNIPPET-COMPILE: every documented C# snippet compiles against the built SDK
# assembly (deleted/renamed SDK symbols fail). Each doc page carries a
# `<!-- snippet-setup -->` preamble declaring its shared context (client/agent/
# result/dm/call…); genuine non-compilable fragments (data-literals, signature-
# only, external-platform-deps, xUnit test-illustration) carry
# `<!-- snippet: no-compile … -->` markers. Heavy (per-snippet MSBuild) →
# defer=1 res=msbuild.
sched_gate SNIPPET-COMPILE tier=nightly defer=1 res=msbuild desc="documented C# snippets compile against the built SDK" \
    -- python3 "$PORTING_SDK_DIR/scripts/snippet_compile.py" --port dotnet --repo "$PORT_ROOT"

sched_gate DOC-CLI desc="documented swaig-test invocations parse (line-detected; dotnet CLI not built here)" \
    -- python3 "$PORTING_SDK_DIR/scripts/doc_cli.py" --port dotnet --repo "$PORT_ROOT"

# Wave-3 doc/API-truth gates — deterministic source/doc analysis (no build, no
# mock, ~1.3s for all six). Per-PR tier: cheap enough to catch doc/API drift at
# PR time rather than a day later in nightly.
sched_gate ERROR-ENVELOPE desc="REST error carries the full (status,body,url,method) envelope + raised on >=400" \
    -- python3 "$PORTING_SDK_DIR/scripts/error_envelope.py" --port dotnet --repo "$PORT_ROOT"
sched_gate DEAD-PUBLIC-ERROR desc="exported error types are raised/caught/user-signalled (no dead error surface)" \
    -- python3 "$PORTING_SDK_DIR/scripts/dead_public_error.py" --port dotnet --repo "$PORT_ROOT"
sched_gate PAGINATION-WIRED desc="shipped iterator-protocol paginator is wired into list()" \
    -- python3 "$PORTING_SDK_DIR/scripts/pagination_wired.py" --port dotnet --repo "$PORT_ROOT"
sched_gate DOC-ENV desc="documented SIGNALWIRE_*/SWML_* env vars <=> code-read vars agree" \
    -- python3 "$PORTING_SDK_DIR/scripts/doc_env.py" --port dotnet --repo "$PORT_ROOT"
sched_gate COUNT-CLAIM desc="numeric doc claims (skills/namespaces) match reality" \
    -- python3 "$PORTING_SDK_DIR/scripts/count_claim.py" --port dotnet --repo "$PORT_ROOT"
sched_gate ACCESSOR-TRUTH desc="documented backtick method() refs exist in source" \
    -- python3 "$PORTING_SDK_DIR/scripts/accessor_truth.py" --port dotnet --repo "$PORT_ROOT"

# EXAMPLES-RUN + SNIPPET-RUN self-skip for dotnet (compiled port; examples have no
# dotnet-run target, and snippet_run is dynamic-ports only) — they exit 0 with a
# note. Wired for parity so the tier graduates automatically if a run target is added.
sched_gate EXAMPLES-RUN tier=nightly defer=1 desc="shipped examples load/start (dotnet: SKIPPED-WITH-NOTE, no run target)" \
    -- python3 "$PORTING_SDK_DIR/scripts/examples_run.py" --port dotnet --repo "$PORT_ROOT"

sched_gate SNIPPET-RUN tier=nightly defer=1 desc="dynamic-port doc snippets run to zero exit (dotnet: self-skips, compiled port)" \
    -- python3 "$PORTING_SDK_DIR/scripts/snippet_run.py" --port dotnet --repo "$PORT_ROOT" --report-only

# ---- §G anti-laundering ledger gate ------------------------------------------
sched_gate SUPPRESSION-LEDGER res=dayone desc="no un-ledgered analyzer suppressions (SUPPRESSIONS_LEDGER.md)" \
    -- python3 "$PORTING_SDK_DIR/scripts/suppression_ledger.py" --port dotnet --repo "$PORT_ROOT"

# ---- §D1 packaging -----------------------------------------------------------
# PACKAGE-SMOKE: the published nupkg (SignalWire.Sdk) must build, install into a
# clean consumer, and import+construct a RestClient. Heavy (dotnet pack + a
# consumer build) → defer=1 res=msbuild.
sched_gate PACKAGE-SMOKE defer=1 res=msbuild desc="published nupkg imports + constructs a client from a clean install" \
    -- python3 "$PORTING_SDK_DIR/scripts/package_smoke.py" --port dotnet --repo "$PORT_ROOT"

# ---- Day-one deterministic gates (enforced, non-report-only) -----------------
sched_gate DOC-LANG-PURITY res=dayone desc="no python-verbatim docs in a non-python port" \
    -- python3 "$PORTING_SDK_DIR/scripts/doc_lang_purity.py" --port dotnet --repo "$PORT_ROOT"

sched_gate DOC-LINKS res=dayone desc="every relative markdown link resolves to a tracked file" \
    -- python3 "$PORTING_SDK_DIR/scripts/doc_links.py" --port dotnet --repo "$PORT_ROOT"

sched_gate README-INCLUDE res=dayone desc="doc code blocks are byte-identical to their gate-compiled fixture regions" \
    -- python3 "$PORTING_SDK_DIR/scripts/readme_include.py" --port dotnet --repo "$PORT_ROOT"

sched_gate ROOT-HYGIENE res=dayone desc="no audit/scratch clutter tracked at repo root (allowlist ROOT_HYGIENE_ALLOW.md)" \
    -- python3 "$PORTING_SDK_DIR/scripts/root_hygiene.py" --port dotnet --repo "$PORT_ROOT"

sched_gate IGNORE-LEDGER-VERIFY res=dayone desc="no laundered false-absence entries in DOC_AUDIT_IGNORE.md (strict: reason/approver/date required)" \
    -- python3 "$PORTING_SDK_DIR/scripts/ignore_ledger_verify.py" --port dotnet --repo "$PORT_ROOT" --require-fields

sched_gate META-CONSISTENT res=dayone desc="package metadata consistency" \
    -- python3 "$PORTING_SDK_DIR/scripts/meta_consistent.py" --port dotnet --repo "$PORT_ROOT"

sched_gate ARTIFACT-DENY res=dayone desc="no porting artifacts in the PUBLISHED package (authoritative listing)" \
    --fn dayone_artifact_deny

# ---- Tier-5 expansion gates (enforced, non-report-only) ----------------------
# Backlog burned to zero for dotnet; these enforce so it can't re-rot.
# ROUTE-COLLISION consumes tools/RouteRegistry (dotnet HAS a registry, same source
# SPEC-PARITY uses) → res=dayone via route_collision_gate. RELEASE-FRESH enforces
# because dotnet has publish.yml with gates-before-publish. SEMVER-DIFF is wired
# and blocking: the release floor is the committed port_signatures.baseline.json
# (baseline_version 3.0.0); the version in SignalWire.csproj must reflect any
# surface change vs that floor.
sched_gate SEMVER-DIFF res=dayone desc="version bump matches the API surface change vs the release-floor baseline" \
    -- python3 "$PORTING_SDK_DIR/scripts/semver_diff.py" --port dotnet --repo "$PORT_ROOT"
sched_gate GEN-TYPE-DEGENERACY res=dayone desc="generated types aren't degenerate loose aliases (modulo GEN_TYPE_DEGENERACY_ALLOW.md)" \
    -- python3 "$PORTING_SDK_DIR/scripts/gen_type_degeneracy.py" --port dotnet --repo "$PORT_ROOT"

sched_gate PUBLIC-JARGON res=dayone desc="no internal porting jargon leaked into public doc comments" \
    -- python3 "$PORTING_SDK_DIR/scripts/public_jargon.py" --port dotnet --repo "$PORT_ROOT"

sched_gate ROUTE-COLLISION res=dayone desc="no route-split/crud-dup between registry + surface (ROUTE_COLLISION_ALLOW.md honored)" \
    --fn route_collision_gate

sched_gate GEN-IDIOM res=dayone desc="generated code is not lint-excluded from the idiom linter" \
    -- python3 "$PORTING_SDK_DIR/scripts/gen_idiom.py" --port dotnet --repo "$PORT_ROOT"

sched_gate RELEASE-FRESH res=dayone desc="publish workflow runs gates BEFORE publishing" \
    -- python3 "$PORTING_SDK_DIR/scripts/release_fresh.py" --port dotnet --repo "$PORT_ROOT"

sched_run
rc=$?
if [ "$rc" -eq 0 ]; then
    echo "==> CI PASS"
else
    echo "==> CI FAIL (gates:$FAILED_GATES )"
fi
exit "$rc"
