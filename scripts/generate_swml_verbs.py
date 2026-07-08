#!/usr/bin/env python3
"""Generate the typed SWML-verbs CONFIG surface for signalwire-dotnet.

The .NET realization of SESSION_CHANGESET_FOR_PORTS.md item D2 — the
``signalwire.core.swml_verbs_generated`` module — mirroring python's
``swml_verbs_generated.py`` and ruby's / php's ``generate_swml_verbs.py``.

Source: the CANONICAL porting-sdk ``schema.json`` ``$defs``. Emits the 155
method-less SWML config types the Python SURFACE oracle records (the reference's
``_SwmlVerbs`` verb-METHOD protocol is ``_``-prefixed and NOT part of the
cross-port surface oracle, so only the CONFIG type surface is emitted):

  1. One method-less C# data class per ``$defs`` OBJECT schema (133) — a public
     property per snake wire key, no methods. Same emit/drop rule as
     generate_rest.py's wire-type emitter: object schema -> data class;
     scalar/array/oneOf/anyOf/allOf alias -> NOT surfaced (matches the reference,
     whose enumerator drops module-level scalar TypeAlias / inline union).

  2. One ``<Verb>Config`` data class per SWMLMethod.anyOf verb whose inner schema
     is an inline object / oneOf union (22) — the flattened UNION of the verb's
     variant properties (mirrors go's flattenUnion / the reference _flatten_union).
     Hand-written verbs (answer/hangup/ai/play/say) are excluded.

  133 + 22 = 155 == the oracle exactly (0 missing / 0 extra).

Output: one class per file under
  src/SignalWire/REST/Namespaces/Generated/GenTypes/SwmlVerbs/<snake>.cs
in namespace ``SignalWire.Core.SwmlVerbsGenerated``. The surface / signature
enumerators route every file under that C# namespace prefix to the oracle module
``signalwire.core.swml_verbs_generated`` BY the namespace (not class name), so a
type name that also exists as a REST wire type lands in the right module; the
SURFACE-DIFF gen-type leaf fold collapses the cross-module duplicates.

Usage:
    python3 scripts/generate_swml_verbs.py            # write into the repo tree
    python3 scripts/generate_swml_verbs.py --check    # GEN-FRESH: fail if stale
    python3 scripts/generate_swml_verbs.py --out DIR  # scratch: emit into DIR
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import sys
from pathlib import Path


def _load_rest_generator():
    here = Path(__file__).resolve().parent
    spec = importlib.util.spec_from_file_location("generate_rest", here / "generate_rest.py")
    if spec is None or spec.loader is None:  # pragma: no cover
        raise SystemExit("generate_swml_verbs.py: cannot load generate_rest.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


GR = _load_rest_generator()

SWML_VERBS_CS_NS = "SignalWire.Core.SwmlVerbsGenerated"
SWML_VERBS_SUBDIR = ["GenTypes", "SwmlVerbs"]

HAND_WRITTEN_VERBS = {"answer", "hangup", "ai", "play", "say"}


def resolve_porting_sdk() -> Path:
    return GR.resolve_porting_sdk()


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def _load_defs(psdk: Path) -> dict:
    doc = json.loads((psdk / "schema.json").read_text())
    defs = doc.get("$defs")
    if not defs:
        raise SystemExit("generate_swml_verbs.py: schema.json has no $defs")
    return defs


def _ref_leaf(ref: str) -> str:
    return ref.rsplit("/", 1)[-1] if ref else ref


def _type_str(node: dict):
    t = node.get("type")
    if isinstance(t, list):
        return next((x for x in t if x != "null"), None)
    return t


def _pascal(s: str) -> str:
    parts = re.split(r"[_\-\s.]", s)
    return "".join(w[:1].upper() + w[1:] for w in parts if w)


def _flatten_union(defs: dict, node) -> dict:
    """Return the UNION of properties across allOf/oneOf/anyOf, following $ref
    (mirrors go's flattenUnion / the reference _flatten_union). First-seen wins."""
    out: dict = {}

    def walk(n) -> None:
        if not n:
            return
        ref = n.get("$ref")
        if ref:
            walk(defs.get(_ref_leaf(ref)))
            return
        for sub in n.get("allOf") or []:
            walk(sub)
        for name, psc in (n.get("properties") or {}).items():
            out.setdefault(name, psc)
        for sub in n.get("oneOf") or []:
            walk(sub)
        for sub in n.get("anyOf") or []:
            walk(sub)

    walk(node)
    return out


def build_outputs(psdk: Path) -> dict:
    defs = _load_defs(psdk)
    outs: dict = {}
    emitted_names: set = set()
    # Surfaced-object leaf -> fully-qualified C# type — a $ref field to one
    # becomes a class-typed property matching the reference's recorded accessor.
    ref_names = {
        GR.type_name(n): f"{SWML_VERBS_CS_NS}.{GR.type_name(n)}"
        for n, node in defs.items()
        if isinstance(node, dict) and GR.is_object_schema(node)
    }

    def emit(cs_name: str, props: dict, desc: str, schema_name: str | None = None) -> None:
        if cs_name in emitted_names:
            return
        emitted_names.add(cs_name)
        fn = "/".join(SWML_VERBS_SUBDIR) + f"/{GR.snake(cs_name)}.cs"
        # schema_name = the SPEC $defs key (NOT cs_name) so the SDK-surface overlay
        # matches by the spec schema name, e.g. `scope: AIParams`.
        outs[fn] = GR.emit_methodless_class(SWML_VERBS_CS_NS, cs_name, props, desc,
                                            ref_names=ref_names, schema_name=schema_name)

    # 1. One data class per OBJECT $defs schema.
    for raw_name, node in defs.items():
        if not isinstance(node, dict) or not GR.is_object_schema(node):
            continue
        emit(GR.type_name(raw_name), node.get("properties") or {},
             f"schema.json $defs schema {raw_name!r}", schema_name=raw_name)

    # 2. One <Verb>Config class per flattenable SWMLMethod.anyOf verb.
    sm = defs.get("SWMLMethod")
    if sm:
        for ref in sm.get("anyOf") or []:
            wrapper = _ref_leaf(ref.get("$ref", ""))
            wdef = defs.get(wrapper)
            if not wdef or not (wdef.get("properties") or {}):
                continue
            verb = next(iter(wdef["properties"].keys()))
            if verb in HAND_WRITTEN_VERBS:
                continue
            inner = wdef["properties"][verb]
            if _type_str(inner) == "string" or inner.get("$ref"):
                continue
            has_inline = _type_str(inner) == "object" and bool(inner.get("properties"))
            if not inner.get("oneOf") and not has_inline:
                continue
            props = _flatten_union(defs, inner)
            if not props:
                continue
            emit(GR.type_name(_pascal(verb) + "Config"), props,
                 f"flattened SWMLMethod verb {verb!r} config")

    return outs


def main(argv: list) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true", help="GEN-FRESH: exit non-zero if stale")
    ap.add_argument("--out", default="", help="scratch: emit into this dir")
    args = ap.parse_args(argv)

    psdk = resolve_porting_sdk()
    outs = build_outputs(psdk)

    if args.out:
        out_dir = Path(args.out)
    else:
        out_dir = repo_root() / "src" / "SignalWire" / "REST" / "Namespaces" / "Generated"

    if args.check:
        stale: list = []
        for fn, src in outs.items():
            p = out_dir / fn
            if not p.is_file() or p.read_text() != src:
                stale.append(str(p))
        expected = set(outs.keys())
        gen_root = out_dir / "/".join(SWML_VERBS_SUBDIR) if not args.out else out_dir
        if gen_root.is_dir():
            for p in sorted(gen_root.rglob("*.cs")):
                rel = p.relative_to(out_dir).as_posix()
                if rel not in expected:
                    stale.append(f"{p} (leftover — not in generator output)")
        if stale:
            sys.stderr.write("GEN-FRESH FAIL: %d generated SWML-verb file(s) stale:\n" % len(stale))
            for s in stale:
                sys.stderr.write("  - %s\n" % s)
            return 1
        print("GEN-FRESH: generated SWML-verb files match porting-sdk/schema.json ($defs).")
        return 0

    for fn, src in outs.items():
        p = out_dir / fn
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(src)
    print(f"generated {len(outs)} SWML-verb file(s) into {out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
