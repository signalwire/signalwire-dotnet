#!/usr/bin/env bash
# _env.sh — shared, CWD-independent tool bootstrap for signalwire-dotnet.
#
# Sourced by scripts/run-format.sh, scripts/run-lint.sh, scripts/run-tests.sh
# (and available to run-ci.sh) so the .NET toolchain resolves the same way no
# matter which directory the caller invoked from. Per RUN_LINT_FORMAT_SPEC.md:
# the tool environment is part of the SCRIPT, not the caller's shell.
#
#   REPO           — absolute repo root (resolved from this file's own path)
#   dotnet_cmd     — echoes a `dotnet` invocation prefix: the host `dotnet` if on
#                    PATH, else the official SDK docker image. Callers run it as
#                    `$(dotnet_cmd) <args>`.
#   dotnet_restore_if_needed — `dotnet restore` the solution once if obj/ caches
#                    are absent (so a foreign-CWD first run isn't a cold miss).
#
# Fail-loud contract: if neither a host `dotnet` nor `docker` is available we
# print a one-line install hint and exit non-zero — never silently skip.

set -euo pipefail

# Resolve the repo root from THIS script's location, independent of $PWD.
# (_env.sh lives in <repo>/scripts/, so the repo is its parent's parent... no —
#  its parent is scripts/, and scripts/'s parent is the repo root.)
_ENV_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(dirname "$_ENV_DIR")"
export REPO

SLN="$REPO/SignalWire.sln"

# Echo a `dotnet` command prefix: host dotnet if present, else the SDK docker
# image mounted at /src as the host user with a writable HOME (so MSBuild/NuGet
# caches work). Mirrors run-ci.sh's historical dotnet_cmd exactly.
#
# Set DOTNET_DOCKER_NETWORK_HOST=1 to add `--network host` to the docker
# fallback (the TEST path needs the container to reach host-spawned mocks); it
# also forwards MOCK_* env vars into the container.
dotnet_cmd() {
    local bin
    bin="$(command -v dotnet || true)"
    if [ -n "$bin" ]; then
        # Callers use the result UNQUOTED ($DN ...) so the multi-word docker
        # fallback word-splits correctly. A host dotnet path containing a space
        # (Windows git-bash resolves `C:\Program Files\dotnet` to
        # `/c/Program Files/dotnet/dotnet`) would then split mid-path and fail
        # with exit 127 (`/c/Program: No such file or directory`). Since the
        # resolved bin is already on PATH, emit the bare command name in that
        # case — it invokes the same executable with no embedded space.
        case "$bin" in
            *" "*) echo "dotnet" ;;
            *) echo "$bin" ;;
        esac
        return 0
    fi
    if command -v docker >/dev/null 2>&1; then
        local net="" mockenv=""
        if [ "${DOTNET_DOCKER_NETWORK_HOST:-}" = "1" ]; then
            net="--network host"
            mockenv="-e MOCK_SIGNALWIRE_PORT=${MOCK_SIGNALWIRE_PORT:-} -e MOCK_RELAY_PORT=${MOCK_RELAY_PORT:-} -e MOCK_RELAY_HTTP_PORT=${MOCK_RELAY_HTTP_PORT:-}"
        fi
        echo "docker run --rm $net --user $(id -u):$(id -g) -e HOME=/tmp $mockenv -v $REPO:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet"
        return 0
    fi
    echo "FATAL: neither 'dotnet' nor 'docker' found on PATH." >&2
    echo "       Install the .NET SDK 10.0 (https://dotnet.microsoft.com/download)" >&2
    echo "       or Docker (the fallback uses mcr.microsoft.com/dotnet/sdk:10.0)." >&2
    exit 1
}

# Restore the solution if the NuGet/obj caches look absent. Cheap no-op once
# warm; makes a first run from a foreign CWD self-sufficient.
dotnet_restore_if_needed() {
    local dn
    dn="$(dotnet_cmd)"
    if [ ! -d "$REPO/src/SignalWire/obj" ]; then
        echo "    (restoring $SLN — first run) ..."
        # shellcheck disable=SC2086
        $dn restore "$SLN"
    fi
}

export SLN

# ---------------------------------------------------------------------------
# THE LINT/FORMAT SCOPE — every .csproj in the repo.
#
# `SignalWire.sln` lists only TWO projects (src/SignalWire + tests). Driving the
# FMT/LINT gates off it, or off `src/SignalWire/SignalWire.csproj` alone, left
# the other 17 projects (examples/, tools/, scripts/, the goldens' DumpFixtures)
# entirely unanalysed and unformatted. Owner ruling 2026-07-30: every directory
# is linted and formatted at the bar the shipped library meets — so the scope is
# ENUMERATED FROM DISK, not read off a checked-in list that silently goes stale
# when someone adds a project. A new .csproj is in scope the moment it exists.
#
# Only build output and gitignored scratch are skipped (obj/, bin/, .sw-tmp/,
# .tmp/) — those hold no source we own, and there is no third-party vendored
# code in this tree. The scratch skip is load-bearing, not tidiness: a transient
# probe project written under .sw-tmp/ WILL otherwise be enumerated and its
# findings reported as repo findings.
#
# Prints one project path per line, sorted (stable ordering across machines).
# `.claude/worktrees/` is excluded for the same reason as the scratch dirs, and
# it is not hypothetical: an agent worktree is a FULL SECOND CHECKOUT of this
# repo living under the repo root, so without this the enumeration returned 172
# projects in an 86-project tree — every project twice, once from the real tree
# and once from the worktree. That silently doubled both gates' work and made
# them report findings against a copy nobody edits.
dotnet_all_projects() {
    find "$REPO" -name '*.csproj' \
        -not -path '*/obj/*' -not -path '*/bin/*' -not -path '*/.git/*' \
        -not -path '*/.sw-tmp/*' -not -path '*/.tmp/*' \
        -not -path "$REPO/.claude/worktrees/*" \
        | LC_ALL=C sort
}

# Fail loud if the enumeration comes back empty — an empty scope makes both
# gates vacuously green, which is worse than a red. (A gate that checks nothing
# passes on anything.)
dotnet_require_projects() {
    local n
    n="$(dotnet_all_projects | grep -c . || true)"
    if [ "$n" -lt 2 ]; then
        echo "FATAL: project enumeration found $n .csproj under $REPO — refusing" >&2
        echo "       to run a vacuous gate. Check the repo checkout." >&2
        exit 1
    fi
    echo "$n"
}

# ---------------------------------------------------------------------------
# THE ALL-PROJECTS SOLUTION — the same scope as dotnet_all_projects, in ONE
# MSBuild-loadable unit.
#
# WHY: FMT and LINT both used to loop over the enumeration and spawn a SEPARATE
# `dotnet` process per project. At 86 projects that is 86 MSBuild workspace
# loads, and the workspace load — not the actual formatting or analysis — is
# what dominates. Measured on one machine, one session:
#
#     FMT   86x `dotnet format whitespace <proj>`  199.4s  ->  batched  11.7s
#     LINT  86x `dotnet build <proj>`              155.5s  ->  batched  75.0s
#
# Batching changes NOTHING about what is checked: identical project set,
# identical analyzers, identical -m:1 serialization. It only stops paying the
# per-process startup 86 times.
#
# SCOPE IS VERIFIED, NOT ASSUMED. `dotnet sln add` silently DROPS a project
# whose basename collides with one already in the solution — this tree has two
# distinct `RelayAnswerAndWelcome.csproj` (examples/ and relay/examples/), and a
# naive batch quietly linted 85 of 86. So each project is added under its own
# solution folder (disambiguating the basename), and the resulting solution is
# COUNTED against the enumeration; a mismatch is fatal. A gate that silently
# checks less than it claims is worse than a slow one.
#
# The solution is a generated build artifact under .tmp/ (gitignored scratch,
# itself excluded from the enumeration above), rebuilt from scratch on every
# run so it can never go stale relative to what is on disk.
#
# Echoes the path of the generated solution.
dotnet_all_projects_solution() {
    local dn slndir sln n_expected n_actual
    dn="$(dotnet_cmd)"
    slndir="$REPO/.tmp/allprojects-sln"
    sln="$slndir/AllProjects.slnx"

    n_expected="$(dotnet_all_projects | grep -c . || true)"

    rm -rf "$slndir"
    mkdir -p "$slndir"
    # shellcheck disable=SC2086
    $dn new sln -n AllProjects -o "$slndir" >/dev/null

    # One solution folder per project, named from the project's repo-relative
    # directory, so two same-named .csproj in different dirs cannot collide.
    local proj rel folder
    while IFS= read -r proj; do
        rel="${proj#"$REPO"/}"
        folder="$(dirname "$rel")"
        # shellcheck disable=SC2086
        $dn sln "$sln" add --solution-folder "$folder" "$proj" >/dev/null
    done < <(dotnet_all_projects)

    n_actual="$(grep -c 'Path=' "$sln" || true)"
    if [ "$n_actual" -ne "$n_expected" ]; then
        echo "FATAL: all-projects solution holds $n_actual projects but the" >&2
        echo "       enumeration found $n_expected — refusing to run a gate over" >&2
        echo "       a SILENTLY REDUCED scope (duplicate .csproj basename?)." >&2
        exit 1
    fi

    echo "$sln"
}
