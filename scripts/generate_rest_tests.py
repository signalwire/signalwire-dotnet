#!/usr/bin/env python3
"""Generate the full-mock REST wire-test suite for signalwire-dotnet.

This is the .NET realisation of porting-sdk/REST_TEST_GENERATOR_RULES.md (the
portable REST *test* generator; reference:
generate_python_rest_types.py::generate_rest_tests, mirrors:
signalwire-go/cmd/generate-rest-tests + signalwire-php/scripts/
generate_rest_tests.py + signalwire-typescript/scripts/generate-rest-tests.ts).

For every REST route the GENERATED SDK client actually implements it emits, into
tests/RestMock/Generated/<Spec>GeneratedTest.cs:

  - a SUCCESS test: call the real generated SDK method (reached off a
    Namespaces.Generated.ResourceTree bound to the shared mock_signalwire
    harness) and assert the mock journaled the expected (method, matched_route);
  - an ERROR test: arm a 500 for that route, assert the SDK raises
    SignalWireRestError with StatusCode == 500 (+ journal route/status).

The assertion oracle is INDEPENDENT of the resource generator (RULES §1):
  - the (method, path) + the accessor chain / member / typed args come from the
    route-plan captured off the REAL generated client (tools/RestTestPlan, via
    scripts/rest-test-plan.sh), NOT re-walked here;
  - the matched_route to assert comes from the OpenAPI operationId
    (<spec_dir>.<operationId>) — the same value the mock derives its route
    table from. A generated test therefore catches SDK-vs-contract drift, not a
    generator self-snapshot.

Inputs joined by (METHOD, normalized-path) (RULES §2): the plan's captured
routes (path params already {id}) x the spec operationIds (spec path normalized
the SAME way before the join). Routing collisions are resolved
longest-template-wins (RULES §7) so the asserted route is the one the mock
ACTUALLY journals (e.g. GET /rooms/{id} vs GET /rooms/{name}).

Call args are type-correct BY CONSTRUCTION (RULES §4/§6): tools/RestTestPlan
reflects each generated method's REQUIRED parameter types off the live client
and emits a C# literal of the right kind (string->"x", int->1, double->1.0,
bool->false, List<object>->new(), Dictionary<..>->new()). The generated tests
build clean under the port's analyzer/warnings-as-errors gate with no edits.

GEN-FRESH: `--check` reproduces the committed *GeneratedTest.cs and exits
non-zero if any file differs. Resolves porting-sdk via $PORTING_SDK or sibling.

Usage:
    python3 scripts/generate_rest_tests.py           # (re)write the test files
    python3 scripts/generate_rest_tests.py --check   # GEN-FRESH: fail if stale
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path

try:
    import yaml
except ImportError:  # pragma: no cover
    sys.stderr.write("generate_rest_tests.py requires PyYAML (pip install pyyaml)\n")
    raise


# ---------------------------------------------------------------------------
# Resolution.
# ---------------------------------------------------------------------------

def resolve_porting_sdk() -> Path:
    env = os.environ.get("PORTING_SDK")
    if env and (Path(env) / "rest-apis").is_dir():
        return Path(env).resolve()
    here = Path(__file__).resolve()
    for parent in here.parents:
        cand = parent.parent / "porting-sdk"
        if (cand / "rest-apis").is_dir():
            return cand.resolve()
    raise SystemExit(
        "generate_rest_tests.py: porting-sdk not found (set $PORTING_SDK or clone adjacent)"
    )


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


# ---------------------------------------------------------------------------
# 1. Capture from the real client (RULES §3) — shell the committed wrapper.
#    rest-test-plan.sh: per-route call plan (chain, member, typed args) +
#    captured (method, path_template) off the GENERATED ResourceTree.
# ---------------------------------------------------------------------------

def load_plan() -> list[dict]:
    proc = subprocess.run(
        ["bash", str(repo_root() / "scripts" / "rest-test-plan.sh")],
        cwd=str(repo_root()),
        capture_output=True,
        text=True,
    )
    if proc.returncode != 0:
        sys.stderr.write(proc.stderr)
        raise SystemExit(
            "rest-test-plan.sh reported an incomplete plan "
            "(uninvokable/no-request method) — refusing to generate a partial suite"
        )
    out = proc.stdout
    i = out.find("{")
    if i > 0:
        out = out[i:]
    data = json.loads(out)
    if data.get("errors"):
        raise SystemExit(
            f"rest-test-plan.sh reported {len(data['errors'])} capture error(s) — plan incomplete"
        )
    return data["plan"]


# ---------------------------------------------------------------------------
# 2. The join — plan routes × spec operationIds by (method, normalized-path).
# ---------------------------------------------------------------------------

_BRACE = re.compile(r"\{[^}]+\}")


def norm_params(p: str) -> str:
    """Every {param} → {id} (plan already does this; do it to the spec path so
    renamed params — {token_id}, {name} — line up)."""
    return _BRACE.sub("{id}", p)


def wire_key(p: str) -> str:
    """Every {param} → X: the wire-identical key used for collision ranking."""
    return _BRACE.sub("X", p)


def spec_prefix(doc: dict) -> str:
    url = ((doc.get("servers") or [{}])[0]).get("url", "")
    i = url.find("signalwire.com")
    return url[i + len("signalwire.com"):] if i >= 0 else ""


def spec_dirs_with_openapi(psdk: Path) -> list[str]:
    root = psdk / "rest-apis"
    out = [
        d.name
        for d in root.iterdir()
        if d.is_dir() and (d / "openapi.yaml").is_file()
    ]
    return sorted(out)


def build_join(plan: list[dict], psdk: Path, spec_dirs: list[str]) -> tuple[list[dict], list[str]]:
    """Return (rows, unmatched). One row per plan entry that has a spec op.

    Row: {method, path, op_id (<spec>.<operationId>), spec, chain, member, args}.
    The op_id is the longest-template collision winner the mock actually
    journals (RULES §7).
    """
    op_by: dict[str, str] = {}          # "METHOD normPath" -> <spec>.<operationId>
    wire_winner: dict[str, tuple[int, str]] = {}   # "METHOD wireKey" -> (len, route)
    verbs = ("get", "post", "put", "patch", "delete")

    for spec in spec_dirs:
        doc = yaml.safe_load((psdk / "rest-apis" / spec / "openapi.yaml").read_text())
        prefix = spec_prefix(doc)
        for path_key, body in (doc.get("paths") or {}).items():
            orig = prefix + path_key
            full = _BRACE.sub("{id}", orig)
            wk = _BRACE.sub("X", orig)
            for verb in verbs:
                op = body.get(verb)
                if not isinstance(op, dict):
                    continue
                op_id = op.get("operationId")
                if not op_id:
                    continue
                route = f"{spec}.{op_id}"
                op_by[f"{verb.upper()} {full}"] = route
                wkey = f"{verb.upper()} {wk}"
                cur = wire_winner.get(wkey)
                if cur is None or len(orig) > cur[0]:
                    wire_winner[wkey] = (len(orig), route)

    rows: list[dict] = []
    unmatched: list[str] = []
    for r in plan:
        method = r["method"]
        np = norm_params(r["path_template"])
        chain_member = ".".join(r["chain"]) + "." + r["member"]
        if f"{method} {np}" not in op_by:
            unmatched.append(f"{chain_member} ({method} {np})")
            continue
        winner = wire_winner.get(f"{method} {wire_key(r['path_template'])}")
        if winner is None:
            unmatched.append(f"{chain_member} ({method} {np}) — no wire winner")
            continue
        op_id = winner[1]
        spec = op_id[: op_id.index(".")]
        rows.append({
            "method": method,
            "path": np,
            "op_id": op_id,
            "spec": spec,
            "chain": r["chain"],
            "member": r["member"],
            "args": r["args"],
        })
    return rows, unmatched


# ---------------------------------------------------------------------------
# 3. Emit — one tests/RestMock/Generated/<Spec>GeneratedTest.cs per spec ns.
# ---------------------------------------------------------------------------

def pascal_spec(spec: str) -> str:
    """spec dir name → PascalCase class-name fragment (relay-rest → RelayRest)."""
    return "".join(part[:1].upper() + part[1:] for part in re.split(r"[-_]", spec) if part)


def method_ident(chain: list[str], member: str) -> str:
    """A stable C# test-method identifier from the chain + member (drop the
    trailing 'Async' on the member for readability; keep the chain so two
    resources with the same member don't collide)."""
    mem = member[:-5] if member.endswith("Async") else member
    return "".join(chain) + "_" + mem


def call_expr(chain: list[str], member: str, args: list[str]) -> str:
    """The literal C# call `tree.<Chain...>.<member>(args)`."""
    accessor = "tree." + ".".join(chain)
    return f"{accessor}.{member}({', '.join(args)})"


HEADER_TMPL = """/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
// <auto-generated>
// Code generated by scripts/generate_rest_tests.py; DO NOT EDIT.
//
// AUTO-GENERATED full-mock REST wire tests for the '{spec}' namespace — regenerate:
//   python3 scripts/generate_rest_tests.py
//
// Each route the GENERATED client implements (captured from the real client by
// tools/RestTestPlan via scripts/rest-test-plan.sh, joined to the spec
// operationId) gets a SUCCESS test (call it, assert Method + MatchedRoute on the
// mock journal) and an ERROR test (arm a 500, assert SignalWireRestError with
// StatusCode == 500 + journal route/status). The assertion oracle is the spec
// operationId — independent of the resource generator — so these catch
// SDK-vs-contract drift, not a generator self-snapshot. Shared full-mock harness
// fixtures (MockServerFixture); routes drive the generated ResourceTree.
// </auto-generated>
#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using SignalWire.REST;
using SignalWire.REST.Namespaces.Generated;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock.Generated;

/// <summary>Generated full-mock REST wire tests for the '{spec}' namespace.</summary>
[Trait("Category", "RestCoverage")]
public class {cls} : CoverageBase
{{
    public {cls}(MockServerFixture fixture) : base(fixture) {{ }}

    private ResourceTree NewTree() => new(NewHttp());
"""


def emit_spec_file(spec: str, rows: list[dict]) -> str:
    cls = pascal_spec(spec) + "GeneratedTest"
    body = HEADER_TMPL.format(spec=spec, cls=cls)
    for r in rows:
        ident = r["_ident"]
        call = call_expr(r["chain"], r["member"], r["args"])
        method = r["method"]
        op_id = r["op_id"]
        body += f"""
    [Fact]
    public async Task {ident}_Success()
    {{
        if (!Fixture.Available) return;
        var tree = NewTree();
        var body = await {call};
        Assert.NotNull(body);
        var j = Fixture.Harness.Journal.Last();
        Assert.Equal("{method}", j.Method);
        Assert.Equal("{op_id}", j.MatchedRoute);
    }}

    [Fact]
    public async Task {ident}_Error()
    {{
        if (!Fixture.Available) return;
        var tree = NewTree();
        var status = await AssertErrorAsync("{op_id}", 500,
            () => {call});
        Assert.Equal(500, status);
    }}
"""
    body += "}\n"
    return body


# ---------------------------------------------------------------------------
# Driver.
# ---------------------------------------------------------------------------

def build_outputs(psdk: Path) -> tuple[dict[str, str], list[str], int]:
    """Return ({filename: source}, unmatched, n_routes_covered)."""
    plan = load_plan()
    spec_dirs = spec_dirs_with_openapi(psdk)
    rows, unmatched = build_join(plan, psdk, spec_dirs)

    by_spec: dict[str, list[dict]] = {}
    for row in rows:
        by_spec.setdefault(row["spec"], []).append(row)

    outs: dict[str, str] = {}
    for spec in sorted(by_spec):
        srows = by_spec[spec]
        # Deterministic ordering: sort by (chain + member + method).
        srows.sort(key=lambda r: ".".join(r["chain"]) + "." + r["member"] + r["method"])
        used: set[str] = set()
        for r in srows:
            ident = method_ident(r["chain"], r["member"])
            base = ident
            k = 2
            while ident in used:
                ident = f"{base}{k}"
                k += 1
            used.add(ident)
            r["_ident"] = ident
        outs[f"{pascal_spec(spec)}GeneratedTest.cs"] = emit_spec_file(spec, srows)

    return outs, unmatched, len(rows)


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true", help="GEN-FRESH: exit non-zero if stale")
    ap.add_argument("--out", default="", help="scratch: emit into this dir")
    args = ap.parse_args(argv)

    psdk = resolve_porting_sdk()
    outs, unmatched, n_covered = build_outputs(psdk)

    out_dir = Path(args.out) if args.out else (repo_root() / "tests" / "RestMock" / "Generated")

    if unmatched:
        sys.stderr.write(
            f"\nUNMATCHED ({len(unmatched)} plan route(s) with no spec operationId):\n"
        )
        for u in unmatched:
            sys.stderr.write(f"  - {u}\n")

    if args.check:
        stale = []
        for fn, src in outs.items():
            p = out_dir / fn
            if not p.is_file() or p.read_text() != src:
                stale.append(str(p))
        expected = set(outs.keys())
        if out_dir.is_dir():
            for p in sorted(out_dir.glob("*.cs")):
                if p.name not in expected:
                    stale.append(f"{p} (leftover — not in generator output)")
        if stale:
            sys.stderr.write("GEN-FRESH FAIL: %d generated REST test file(s) stale:\n" % len(stale))
            for s in stale:
                sys.stderr.write(f"  - {s}\n")
            return 1
        total = sum(src.count("public async Task") for src in outs.values())
        print(
            f"GEN-FRESH: {len(outs)} generated REST test file(s) up to date "
            f"({total} tests, {n_covered} routes)."
        )
        return 0

    out_dir.mkdir(parents=True, exist_ok=True)
    expected = set(outs.keys())
    for p in sorted(out_dir.glob("*.cs")):
        if p.name not in expected:
            p.unlink()
    for fn, src in outs.items():
        (out_dir / fn).write_text(src)
    total = sum(src.count("public async Task") for src in outs.values())
    print(
        f"generated {len(outs)} REST test file(s) into {out_dir} "
        f"({total} tests across {len(outs)} namespaces, {n_covered} routes covered)"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
