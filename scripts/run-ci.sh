#!/usr/bin/env bash
# run-ci.sh — canonical local-and-CI gate runner for signalwire-dotnet.
#
# Same script invoked locally (`bash scripts/run-ci.sh`) AND by the
# GitHub Actions workflow. No drift between local and CI behavior.
#
# Gates (in order, fail-fast):
#   1. dotnet test (via docker SDK image)  — language test runner
#   2. signature regen                     — python adapter + dotnet build
#   3. drift gate                          — porting-sdk diff_port_signatures.py
#   4. surface-fresh gate                  — porting-sdk check_surface_freshness.py
#                                            (regenerates port_surface.json in
#                                            place via enumerate_surface.py and
#                                            fails if the committed copy is stale
#                                            modulo the generated_from git-sha;
#                                            closes the Layer-B-not-gated hole —
#                                            DRIFT gates Layer A signatures only,
#                                            so port_surface.json could silently
#                                            rot)
#   5. no-cheat gate                       — porting-sdk audit_no_cheat_tests.py
#   6. emission gate                       — porting-sdk diff_port_emission.py
#                                            (byte-compares tools/EmitCorpus's
#                                            FunctionResult.ToDict() output vs
#                                            Python's to_dict() over the shared
#                                            81-entry corpus; no mocks / network,
#                                            pure serialisation)
#
# `dotnet` is not on host PATH. We use `docker run` with the official SDK
# image (mcr.microsoft.com/dotnet/sdk:10.0). The same pattern is used in
# scripts/enumerate_signatures.py for SignatureDump.
#
# Mock-server lifecycle: The RestMock + RelayMock tests need
# `mock_signalwire` (port 8784) and `mock_relay` (ws=8785, http=9785) to
# be reachable from the container via `--network host`. The .NET SDK
# image does NOT have python3 installed, so the in-test fallback that
# spawns `python -m mock_signalwire` cannot run inside the container —
# we MUST start the mocks on the host before the docker invocation.
# This script does that automatically (with a cleanup trap). If the
# host already has a mock listening on the slot, we leave it alone.
#
# Multi-target serialization: SignalWire.Tests targets net8.0+net9.0+net10.0.
# By default `dotnet test` runs all three target frameworks in PARALLEL,
# and they all hit the SAME shared mock server (port 8784/8785). Tests are
# now session-isolated WITHIN a framework run (RELAY scopes the journal +
# scenarios by the handshake `sessionid`; REST scopes by a per-test random
# project's Authorization header), so in-framework parallelism is enabled
# (see tests/AssemblyInfo.cs + tests/xunit.runner.json). ACROSS frameworks
# we still run SEQUENTIALLY: that isolation key is per-client, not
# per-framework, so two framework runs would still share the one mock's
# scenario buckets and connection set — a separate mock-lifecycle concern.

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
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

FAILED_GATES=""
SPAWNED_PIDS=()

# ---------------------------------------------------------------------------
# Mock-server lifecycle
# ---------------------------------------------------------------------------
#
# Probe-then-spawn: if a mock is already listening on the slot we don't
# touch it (someone is debugging). If not, we start it ourselves and
# trap-clean it on exit. We serve mock_signalwire on a single REST port
# (8784) and mock_relay on the WS+HTTP pair (8785+9785), matching the
# defaults in tests/MockTest.cs and tests/RelayMockTest.cs.

# Pick a free TCP port on 127.0.0.1 (bind :0, read the OS-assigned port,
# release). Never reuse a hardcoded port — a leftover or concurrent mock
# squatting a fixed port otherwise makes the gate hang on its health poll.
pick_free_port() {
    python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()'
}

# Env overrides win; otherwise pick FREE ports rather than hardcoded defaults
# (mock_signalwire + mock_relay WS/HTTP, all independent).
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
    ) >/tmp/mock_signalwire_dotnet_ci.log 2>&1 &
    SPAWNED_PIDS+=("$!")
    if ! wait_for_health "$url"; then
        echo "FATAL: mock_signalwire failed to start; log /tmp/mock_signalwire_dotnet_ci.log" >&2
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
    ) >/tmp/mock_relay_dotnet_ci.log 2>&1 &
    SPAWNED_PIDS+=("$!")
    if ! wait_for_health "$url"; then
        echo "FATAL: mock_relay failed to start; log /tmp/mock_relay_dotnet_ci.log" >&2
        return 1
    fi
}

cleanup_spawned() {
    local pid
    # `${arr[@]}` on an empty array trips `set -u` on bash < 5.2; guard it so a
    # clean exit (mocks already running → nothing spawned) doesn't error out.
    [ ${#SPAWNED_PIDS[@]} -eq 0 ] && return 0
    for pid in "${SPAWNED_PIDS[@]}"; do
        if kill -0 "$pid" 2>/dev/null; then
            kill "$pid" 2>/dev/null || true
            wait "$pid" 2>/dev/null || true
        fi
    done
}

trap cleanup_spawned EXIT INT TERM

run_gate() {
    local name="$1"; shift
    local description="$1"; shift
    local logfile
    logfile="$(mktemp)"
    "$@" >"$logfile" 2>&1
    local rc=$?
    if [ "$rc" -eq 0 ]; then
        echo "[$name] $description ... PASS"
        rm -f "$logfile"
        return 0
    fi
    echo "[$name] $description ... FAIL: exit $rc"
    sed 's/^/    /' "$logfile" | tail -40
    rm -f "$logfile"
    FAILED_GATES="$FAILED_GATES $name"
    return $rc
}

# Per-framework runner that returns 0 only when ALL frameworks passed.
# Sequential to avoid the multi-target race against the shared mock.
#
# Prefers a host-installed dotnet (CI runners have it via setup-dotnet,
# and devs may have it in PATH). Falls back to docker for environments
# where dotnet isn't installed locally.
#
# Docker fallback notes:
# - `--user $(id -u):$(id -g)` makes the container write build artifacts
#   (obj/, bin/) as the host user, so the subsequent SIGNATURES gate can
#   read/write them.
# - `-e HOME=/tmp` because UID-only `--user` has no entry in the image's
#   /etc/passwd, so HOME defaults to `/` which is not writable. dotnet's
#   NuGet/MSBuild needs a writable HOME for ~/.nuget/packages caches.
dotnet_test_per_framework() {
    local rc=0
    local fw
    local dotnet_bin
    dotnet_bin="$(command -v dotnet || true)"
    local failed_fws=""
    # Per-framework logs kept until the end. The outer run_gate echoes only the
    # LAST ~400 lines of the whole gate on failure, so a FIRST-framework failure
    # (net8.0) gets buried under later frameworks' warning spew and becomes
    # undiagnosable in CI. We capture each framework to its own file and, after
    # all frameworks run, RE-PRINT every failing framework's output LAST — so the
    # real error lands inside run_gate's final-400-line window.
    local -a fwlogs=()
    for fw in net8.0 net9.0 net10.0; do
        echo "    --- dotnet test --framework $fw ---"
        local fwlog
        fwlog="$(mktemp)"
        fwlogs+=("$fw:$fwlog")
        if [ -n "$dotnet_bin" ]; then
            # Host path (the GitHub runner has dotnet on PATH). The test fixtures
            # read MOCK_RELAY_PORT (WS) / MOCK_RELAY_HTTP_PORT, but our internal WS
            # port var is MOCK_RELAY_WS_PORT — so we must RENAME it here, exactly as
            # the docker branch does via `-e MOCK_RELAY_PORT=$MOCK_RELAY_WS_PORT`.
            # Without this the fixture sees no MOCK_RELAY_PORT, self-spawns its own
            # mock_relay, and net10.0 loses the spawn race → "Connection refused".
            MOCK_SIGNALWIRE_PORT="$MOCK_SIGNALWIRE_PORT" \
            MOCK_RELAY_PORT="$MOCK_RELAY_WS_PORT" \
            MOCK_RELAY_HTTP_PORT="$MOCK_RELAY_HTTP_PORT" \
                "$dotnet_bin" test --framework "$fw" 2>&1 | tee "$fwlog"
            [ "${PIPESTATUS[0]}" -eq 0 ] || { rc=1; failed_fws="$failed_fws $fw"; }
        else
            docker run --rm --network host \
                    --user "$(id -u):$(id -g)" \
                    -e HOME=/tmp \
                    -e MOCK_SIGNALWIRE_PORT="$MOCK_SIGNALWIRE_PORT" \
                    -e MOCK_RELAY_PORT="$MOCK_RELAY_WS_PORT" \
                    -e MOCK_RELAY_HTTP_PORT="$MOCK_RELAY_HTTP_PORT" \
                    -v "$PWD:/src" -w /src \
                    mcr.microsoft.com/dotnet/sdk:10.0 \
                    dotnet test --framework "$fw" 2>&1 | tee "$fwlog"
            [ "${PIPESTATUS[0]}" -eq 0 ] || { rc=1; failed_fws="$failed_fws $fw"; }
        fi
    done
    if [ "$rc" -ne 0 ]; then
        echo "    dotnet TEST gate: failing framework(s):$failed_fws"
        local entry f log
        for entry in "${fwlogs[@]}"; do
            f="${entry%%:*}"; log="${entry#*:}"
            case " $failed_fws " in
                *" $f "*)
                    echo "    ===== $f FAILED — full output (re-printed last so it survives log truncation) ====="
                    sed 's/^/    [FAIL '"$f"'] /' "$log"
                    echo "    ===== end $f output ====="
                    ;;
            esac
        done
    fi
    local entry log
    for entry in "${fwlogs[@]}"; do log="${entry#*:}"; rm -f "$log"; done
    return $rc
}

# SURFACE-FRESH gate: prove the committed port_surface.json (Layer B) still
# matches a fresh regeneration. The DRIFT gate only polices Layer A
# (port_signatures.json), so without this a Layer-B symbol/shape change could
# land in source without the committed surface being regenerated — it silently
# rots. We:
#   1. snapshot the committed copy (HEAD, with a working-tree fallback),
#   2. regenerate port_surface.json IN PLACE via the surface enumerator
#      (pure-regex parse of src/SignalWire/**/*.cs — no docker / build needed,
#      unlike the SIGNATURES gate's SignatureDump path),
#   3. compare the two modulo the volatile generated_from git-sha,
#   4. restore the committed copy unconditionally so the tree is left clean.
surface_fresh_gate() {
    local committed="/tmp/committed_surface.json"
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

# Resolve a dotnet invocation: host dotnet if present, else the SDK docker image
# (same host-or-docker shape as dotnet_test_per_framework). Echoes a command
# prefix the caller runs as `$(dotnet_cmd) <args>`. Docker maps the repo at /src
# as the host user with a writable HOME so MSBuild/NuGet caches work.
dotnet_cmd() {
    local bin
    bin="$(command -v dotnet || true)"
    if [ -n "$bin" ]; then
        echo "$bin"
    else
        echo "docker run --rm --user $(id -u):$(id -g) -e HOME=/tmp -v $PWD:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet"
    fi
}

# FMT gate: dotnet format whitespace (the house style — whitespace/newlines).
# LOCAL ($CI unset) auto-fixes the tree; CI ($CI=true) verifies read-only and
# FAILS on any unformatted file. Whitespace-scoped so it stays orthogonal to the
# LINT gate (analyzers); a reformat is surface/emission-neutral.
fmt_gate() {
    local dn
    dn="$(dotnet_cmd)"
    if [ -n "${CI:-}" ]; then
        $dn format whitespace SignalWire.sln --verify-no-changes
    else
        $dn format whitespace SignalWire.sln
        if ! git diff --quiet 2>/dev/null; then
            echo "    (FMT auto-applied whitespace formatting — review & stage)"
        fi
    fi
}

# LINT gate: a clean analyzer build. Directory.Build.props turns the curated
# analyzer set on with TreatWarningsAsErrors across net8/9/10, so `dotnet build`
# failing == a lint violation. Builds the src library only (analyzers run there;
# tests/examples/tools are not the shipped surface).
lint_gate() {
    local dn
    dn="$(dotnet_cmd)"
    $dn build src/SignalWire/SignalWire.csproj -c Release --no-incremental
}

# SWAIG-CLI gate: lightweight shared swaig-test mini-contract (NOT python parity;
# python's in-process simulator surface is reference-only). Black-box: invokes
# `bin/swaig-test --help` + golden invocations and asserts the shared verbs are
# documented and a target-but-no-action invocation errors (the cross-port
# default). bin/swaig-test is a dotnet-script, so we run it via `dotnet script`.
# dotnet's swaig-test is an HTTP-probe model (--url, like the 7 wire ports), so
# we pass --require-url-model. It does NOT implement --simulate-serverless, so
# the no-serverless clause asserts the flag is rejected as an unknown option
# (the bin/swaig-test default: case errors on any unknown -flag).
swaig_cli_gate() {
    local dn
    dn="$(dotnet_cmd)"
    # dotnet-script is the runner for bin/swaig-test. Provision it as a tool-path
    # tool (idempotent: install is a no-op / fails-harmlessly if already present),
    # then put it on PATH for the duration of the gate.
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

cd "$PORT_ROOT"

echo "==> running CI gates for $PORT_NAME (porting-sdk at $PORTING_SDK_DIR)"

echo "==> ensuring mock servers are running on host"
ensure_mock_signalwire || exit 2
ensure_mock_relay || exit 2

# Gate 1: dotnet test via docker image, serialized per target framework.
run_gate "TEST" "docker dotnet test (net8/net9/net10 sequential)" \
    dotnet_test_per_framework

# Gate 2: signature regen — adapter shells out to docker for SignatureDump.
run_gate "SIGNATURES" "regenerate port_signatures.json" \
    python3 scripts/enumerate_signatures.py

# Gate 3: drift gate
run_gate "DRIFT" "diff_port_signatures vs python reference" \
    python3 "$PORTING_SDK_DIR/scripts/diff_port_signatures.py" \
        --reference "$PORTING_SDK_DIR/python_signatures.json" \
        --port-signatures "$PORT_ROOT/port_signatures.json" \
        --surface-omissions "$PORT_ROOT/PORT_OMISSIONS.md" \
        --surface-additions "$PORT_ROOT/PORT_ADDITIONS.md" \
        --omissions "$PORT_ROOT/PORT_SIGNATURE_OMISSIONS.md"

# Gate 4: surface-fresh — regenerate port_surface.json (Layer B) in place and
# fail if the committed copy is stale modulo the generated_from git-sha.
run_gate "SURFACE-FRESH" "check_surface_freshness vs regenerated port_surface.json" \
    surface_fresh_gate

# Gate 4b: GEN-FRESH — the code-generated REST resource layer
# (src/SignalWire/REST/Namespaces/Generated/**) must match a fresh run of
# scripts/generate_rest.py against the canonical porting-sdk specs + x-sdk-*
# markup (SESSION_CHANGESET item A/B). A stale/hand-edited generated file (or a
# leftover file no longer in the generator output) fails the gate — the .cs
# resources, the client tree, and the rest_signatures.json sidecar are all
# checked. Pure-python, no build/mock needed. Mirrors the other ports'
# GEN-FRESH.
run_gate "GEN-FRESH" "generate_rest.py --check (generated REST layer matches specs)" \
    python3 scripts/generate_rest.py --check

# Gate 4c: GEN-FRESH (tests) — the code-generated REST *wire-test* suite
# (tests/RestMock/Generated/**) must match a fresh run of
# scripts/generate_rest_tests.py: the full-mock success+error wire tests for
# every route the GENERATED client dispatches, captured off the real client
# (tools/RestTestPlan via scripts/rest-test-plan.sh) and joined to the spec
# operationId — the independent oracle (REST_TEST_GENERATOR_RULES.md, item E). A
# stale/hand-edited generated test file (or a leftover file no longer emitted)
# fails the gate. Builds RestTestPlan (net8) to capture the plan; no mock needed
# for --check. Mirrors the other ports' generated-test GEN-FRESH.
run_gate "GEN-FRESH-TESTS" "generate_rest_tests.py --check (generated REST wire tests match specs)" \
    python3 scripts/generate_rest_tests.py --check

# Gate 5: no-cheat
run_gate "NO-CHEAT" "audit_no_cheat_tests" \
    python3 "$PORTING_SDK_DIR/scripts/audit_no_cheat_tests.py" --root "$PORT_ROOT"

# Gate 5b: REST-COVERAGE — every canonical REST route the SDK implements must be
# exercised with BOTH a success (2xx) AND an error (4xx/5xx) response on the
# correct on-the-wire path (parity). Measured by replaying the mock journal of a
# REST-coverage suite run through porting-sdk's rest_coverage checker. Accepted
# gaps — routes with no SDK method, malformed canonical routes, mock-router
# collisions — are allowlisted: the shared baseline
# (porting-sdk/REST_COVERAGE_BASELINE.md) + this port's REST_COVERAGE_GAPS.md. A
# stale entry (route now covered) fails the gate. Self-contained: spins its own
# mock on a dedicated port, runs ONLY the RestCoverage-trait tests on a SINGLE
# target framework into ONE journal, then checks that journal. Same shape as
# go's/python's/java's gate.
rest_coverage_gate() {
    # REST_COVERAGE_PORT override wins; otherwise pick a FREE port (bind :0).
    # Never reuse a hardcoded port — a leftover/concurrent mock squatting it
    # otherwise makes the gate hang on its health poll.
    local port="${REST_COVERAGE_PORT:-$(pick_free_port)}"
    [ -n "$port" ] || { echo "could not allocate a free port" >&2; return 1; }
    local url="http://127.0.0.1:${port}"
    local mock_pkg_parent="$PORTING_SDK_DIR/test_harness/mock_signalwire"
    (
        cd "$mock_pkg_parent"
        PYTHONPATH="$PWD${PYTHONPATH:+:$PYTHONPATH}" \
            python3 -m mock_signalwire --host 127.0.0.1 --port "$port" --log-level error
    ) >/tmp/rest_cov_mock_dotnet.$$.log 2>&1 &
    local mock_pid=$!
    # shellcheck disable=SC2064
    trap "kill $mock_pid 2>/dev/null" RETURN
    # Fail LOUD if the mock dies mid-startup or never becomes healthy — never hang.
    local i ready=0
    for i in $(seq 1 60); do
        if ! kill -0 "$mock_pid" 2>/dev/null; then
            echo "mock_signalwire died on port $port — log:" >&2
            cat "/tmp/rest_cov_mock_dotnet.$$.log" >&2
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

    # Run ONLY the coverage-trait tests, on ONE framework (net8.0), so all
    # traffic lands in this one mock's single journal. The coverage tests are
    # journal-scoped per-test (per-test random project) but the checker reads
    # the GLOBAL journal, so a single-framework serial run is the clean way to
    # get one journal with every route's success+error pair.
    local dn rc
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
run_gate "REST-COVERAGE" "every implemented REST route covered success+error (parity + allowlist)" \
    rest_coverage_gate

# Gate 5c: SPEC-PARITY — the routes the SDK actually IMPLEMENTS must equal the
# canonical spec route set, modulo porting-sdk/SPEC_IMPLEMENTATION_GAPS.md. This
# is the spec-first guard REST-COVERAGE can't give: REST-COVERAGE only proves
# *tested* routes match the spec, so a route the SDK implements that the spec
# doesn't define (or a canonical route the SDK never implemented) would slip past
# it. Set B is built by tools/RouteRegistry — it constructs the live RestClient,
# swaps in a recording HTTP transport (records (method, path), returns a stub
# 200), and reflects over every namespace/sub-resource method, invoking each with
# sentinel args, so it sees every dispatched route whether or not it's tested (not
# an AST scrape, not the journal). scripts/route-registry.sh wraps it so only the
# JSON reaches stdout (MSBuild chatter -> stderr); the shared porting-sdk diff
# consumes that JSON via --registry-json. No mocks / no network. Same shape as
# go's Gate 5c.
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
run_gate "SPEC-PARITY" "implemented routes == canonical spec (modulo SPEC_IMPLEMENTATION_GAPS.md)" \
    spec_parity_gate

# Gate 6: emission — byte-compare FunctionResult.ToDict() vs Python to_dict()
# across the shared 81-entry corpus. scripts/emit-corpus.sh wraps
# tools/EmitCorpus so only clean JSON reaches stdout (it builds with MSBuild
# output on stderr, then runs the compiled binary). No mocks / no network —
# pure serialisation; needs only signalwire-python adjacent (already required).
run_gate "EMISSION" "diff_port_emission vs python to_dict()" \
    python3 "$PORTING_SDK_DIR/scripts/diff_port_emission.py" \
        --dump-cmd "bash $PORT_ROOT/scripts/emit-corpus.sh"

# Gate 7: FMT — dotnet format whitespace (local: auto-fix; CI: --verify).
run_gate "FMT" "dotnet format whitespace (local: auto-fix; CI: --verify)" \
    fmt_gate

# Gate 8: LINT — clean analyzer build (Directory.Build.props: curated CA set,
# TreatWarningsAsErrors). A build warning == a lint violation.
run_gate "LINT" "dotnet build (analyzers, warnings-as-errors)" \
    lint_gate

# Gate 9: DOC-AUDIT — every symbol referenced in docs/examples resolves to a
# real entry in port_surface.json (or is excused in DOC_AUDIT_IGNORE.md).
run_gate "DOC-AUDIT" "audit_docs vs port_surface.json" \
    python3 "$PORTING_SDK_DIR/scripts/audit_docs.py" \
        --root "$PORT_ROOT" \
        --surface "$PORT_ROOT/port_surface.json" \
        --ignore "$PORT_ROOT/DOC_AUDIT_IGNORE.md"

# Gate 10: SURFACE-DIFF — the public symbol set matches the Python reference
# surface (modulo documented omissions/additions). DRIFT polices signatures;
# this polices the symbol set.
run_gate "SURFACE-DIFF" "diff_port_surface vs python_surface.json" \
    python3 "$PORTING_SDK_DIR/scripts/diff_port_surface.py" \
        --reference "$PORTING_SDK_DIR/python_surface.json" \
        --port-surface "$PORT_ROOT/port_surface.json" \
        --omissions "$PORT_ROOT/PORT_OMISSIONS.md" \
        --additions "$PORT_ROOT/PORT_ADDITIONS.md"

# Gate 11: SKILL-CONTRACT — each built-in skill's SWAIG tool contract
# (name/parameters/required/enum) matches the Python reference. Sibling of
# EMISSION for skills; scripts/emit-skills.sh wraps tools/EmitSkills.
run_gate "SKILL-CONTRACT" "diff_skill_contracts vs python reference" \
    python3 "$PORTING_SDK_DIR/scripts/diff_skill_contracts.py" \
        --dump-cmd "bash $PORT_ROOT/scripts/emit-skills.sh" \
        --port-repo "$PORT_ROOT"

# Gate 12: SWAIG-CLI — lightweight shared swaig-test mini-contract (verbs are
# documented in --help, a target-but-no-action invocation errors, and an
# unimplemented --simulate-serverless is rejected as an unknown option).
run_gate "SWAIG-CLI" "swaig-test shared mini-contract (verbs/serverless-reject/default-action)" \
    swaig_cli_gate

if [ -z "$FAILED_GATES" ]; then
    echo "==> CI PASS"
    exit 0
else
    echo "==> CI FAIL (gates:$FAILED_GATES )"
    exit 1
fi
