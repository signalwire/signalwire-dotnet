#!/usr/bin/env python3
"""Generate the typed SWAIG read-side payload surface for signalwire-dotnet.

The .NET realization of SESSION_CHANGESET_FOR_PORTS.md item D1 — the three
``signalwire.core.*_generated`` SWAIG payload modules — mirroring python's
``generate_swaig_request`` / ``generate_post_prompt`` / ``generate_swaig_actions``
and ruby's / php's ``generate_swaig_payloads.py``.

Source: the vendored porting-sdk ``swaig-specs/*.yaml`` (from mod_openai):

  * ``swaig-request.yaml``  -> signalwire.core.swaig_request_generated  (2 classes)
        SwaigRequest (+ the inline ``argument`` object lifted to SwaigArgument).
  * ``post-prompt.yaml``    -> signalwire.core.post_prompt_generated    (14 classes)
        one class per components/schemas OBJECT schema; the oneOf alias
        ``PostPromptCallLogEntry`` is NOT surfaced (15 schemas - 1 alias = 14).
  * ``swaig-response.yaml`` -> signalwire.core.swaig_actions_generated  (4 classes)
        one ``<Verb>Action`` class per action key whose value is an object-with-
        properties (a bare object OR the object variant of a oneOf).

  2 + 14 + 4 = 20 classes == the surface oracle EXACTLY (0 missing / 0 extra).

post-prompt + swaig-request classes are in the SIGNATURE oracle WITH a zero-arg
accessor per CLASS-TYPED field (folded to gen-payload). The .NET properties are
typed (a $ref field -> the sibling generated class, so the signature enumerator
records it as a ``class:`` accessor that matches the reference; a scalar field ->
a primitive, excused by the diff as a port-side state accessor). swaig-actions
classes are NOT in the sig oracle — their properties are all scalar, so they
surface method-less on both sides.

Output: one class per file under a per-module subdir
  src/SignalWire/REST/Namespaces/Generated/GenTypes/PostPrompt/<snake>.cs
     (SignalWire.Core.PostPromptGenerated)
  src/SignalWire/REST/Namespaces/Generated/GenTypes/SwaigRequest/<snake>.cs
     (SignalWire.Core.SwaigRequestGenerated)
  src/SignalWire/REST/Namespaces/Generated/GenTypes/SwaigActions/<snake>.cs
     (SignalWire.Core.SwaigActionsGenerated)
routed to the oracle module BY the C# namespace prefix (winning over the
name-keyed lookup, so the hand-written SWAIG SDK classes are never misrouted).

Usage:
    python3 scripts/generate_swaig_payloads.py            # write into the repo tree
    python3 scripts/generate_swaig_payloads.py --check    # GEN-FRESH: fail if stale
    python3 scripts/generate_swaig_payloads.py --out DIR  # scratch: emit into DIR
"""
from __future__ import annotations

import argparse
import importlib.util
import re
import sys
from pathlib import Path


def _load_rest_generator():
    here = Path(__file__).resolve().parent
    spec = importlib.util.spec_from_file_location("generate_rest", here / "generate_rest.py")
    if spec is None or spec.loader is None:  # pragma: no cover
        raise SystemExit("generate_swaig_payloads.py: cannot load generate_rest.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


GR = _load_rest_generator()

# (C# namespace, on-disk subdir) per SWAIG payload module.
PP_NS = "SignalWire.Core.PostPromptGenerated"
PP_SUBDIR = ["GenTypes", "PostPrompt"]
SR_NS = "SignalWire.Core.SwaigRequestGenerated"
SR_SUBDIR = ["GenTypes", "SwaigRequest"]
SA_NS = "SignalWire.Core.SwaigActionsGenerated"
SA_SUBDIR = ["GenTypes", "SwaigActions"]


def resolve_porting_sdk() -> Path:
    return GR.resolve_porting_sdk()


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def _load_yaml(path: Path) -> dict:
    import yaml  # type: ignore[import-untyped]

    return yaml.safe_load(path.read_text())


def _emit(cs_ns, subdir, cs_name, props, desc, ref_names, pascal_props=False):
    # pascal_props: only the swaig-ACTIONS module (SA_NS) is method-less-on-surface
    # and ABSENT from the sig-accessor set, so its DTO properties may be PascalCased
    # (DOTNET-2). The swaig-REQUEST + post-PROMPT modules ARE accessor-bearing
    # (their property names the oracle records wire-key-verbatim), so they stay
    # verbatim (pascal_props=False).
    fn = "/".join(subdir) + f"/{GR.snake(cs_name)}.cs"
    src = GR.emit_methodless_class(cs_ns, cs_name, props, desc, ref_names=ref_names,
                                   pascal_props=pascal_props)
    return fn, src


def _pascal_verb(verb: str) -> str:
    parts = [p for p in re.split(r"[._\-\s]", verb) if p]
    return "".join(w[:1].upper() + w[1:] for w in parts)


def _build_swaig_request(psdk: Path) -> dict:
    spec = _load_yaml(psdk / "swaig-specs" / "swaig-request.yaml")
    schema = spec["components"]["schemas"]["SwaigRequest"]
    props = schema.get("properties", {})
    outs: dict = {}
    # SwaigArgument is a sibling class-typed target in this module — the reference
    # LIFTS the inline `argument` object to a named SwaigArgument and re-points the
    # field at it (generate_swaig_request), so `SwaigRequest.argument` becomes a
    # class-typed accessor. Realize the same lift here by injecting a synthetic
    # $ref so _wire_field_cs_type types the property SwaigArgument.
    ref_names = {"SwaigArgument": f"{SR_NS}.SwaigArgument"}
    arg = props.get("argument")
    props = dict(props)
    if isinstance(arg, dict) and arg.get("properties"):
        fn, src = _emit(SR_NS, SR_SUBDIR, "SwaigArgument", arg["properties"],
                        "inline swaig-request `argument` object (lifted)", {})
        outs[fn] = src
        props["argument"] = {"$ref": "#/components/schemas/SwaigArgument"}
    fn, src = _emit(SR_NS, SR_SUBDIR, "SwaigRequest", props,
                    "swaig-request `SwaigRequest` schema", ref_names)
    outs[fn] = src
    return outs


def _build_post_prompt(psdk: Path) -> dict:
    spec = _load_yaml(psdk / "swaig-specs" / "post-prompt.yaml")
    schemas = spec["components"]["schemas"]
    outs: dict = {}
    emitted: set = set()
    # Surfaced-object leaf -> fully-qualified C# type. Includes the cross-module
    # SwaigRequest (PostPromptSwaigLogEntry.post_data -> swaig_request module) so
    # that class-typed accessor resolves to the right oracle module.
    ref_names = {
        GR.type_name(n): f"{PP_NS}.{GR.type_name(n)}"
        for n, node in schemas.items()
        if isinstance(node, dict) and GR.is_object_schema(node)
    }
    ref_names["SwaigRequest"] = f"{SR_NS}.SwaigRequest"
    for raw_name, node in schemas.items():
        if not isinstance(node, dict) or not GR.is_object_schema(node):
            continue
        cs_name = GR.type_name(raw_name)
        if cs_name in emitted:
            continue
        emitted.add(cs_name)
        fn, src = _emit(PP_NS, PP_SUBDIR, cs_name, node.get("properties") or {},
                        f"post-prompt components/schemas {raw_name!r}", ref_names)
        outs[fn] = src
    return outs


def _build_swaig_actions(psdk: Path) -> dict:
    spec = _load_yaml(psdk / "swaig-specs" / "swaig-response.yaml")
    actions = spec["components"]["schemas"]["SwaigAction"]["properties"]

    def _is_obj(s) -> bool:
        return isinstance(s, dict) and s.get("type") == "object" and bool(s.get("properties"))

    outs: dict = {}
    emitted: set = set()
    for verb in sorted(actions):
        schema = actions[verb]
        if not isinstance(schema, dict):
            continue
        branches = schema.get("oneOf") or ([schema] if _is_obj(schema) else [])
        obj_i = 0
        for b in branches:
            if not _is_obj(b):
                continue
            obj_i += 1
            cs_name = GR.type_name(_pascal_verb(verb) + "Action" + ("" if obj_i == 1 else str(obj_i)))
            if cs_name in emitted:
                continue
            emitted.add(cs_name)
            # swaig-actions are NOT in the sig oracle -> method-less both sides;
            # PascalCase props (DOTNET-2) — wire preserved by [JsonPropertyName].
            fn, src = _emit(SA_NS, SA_SUBDIR, cs_name, b.get("properties") or {},
                            f"swaig-response action {verb!r} value object", {},
                            pascal_props=True)
            outs[fn] = src
    return outs


def build_outputs(psdk: Path) -> dict:
    specs_dir = psdk / "swaig-specs"
    if not specs_dir.is_dir():
        raise SystemExit(
            f"generate_swaig_payloads.py: {specs_dir} not found (need porting-sdk adjacency)"
        )
    outs: dict = {}
    outs.update(_build_post_prompt(psdk))
    outs.update(_build_swaig_request(psdk))
    outs.update(_build_swaig_actions(psdk))
    return outs


_SUBDIRS = (PP_SUBDIR, SR_SUBDIR, SA_SUBDIR)


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
        if not args.out:
            for subdir in _SUBDIRS:
                gen_root = out_dir / "/".join(subdir)
                if gen_root.is_dir():
                    for p in sorted(gen_root.rglob("*.cs")):
                        rel = p.relative_to(out_dir).as_posix()
                        if rel not in expected:
                            stale.append(f"{p} (leftover — not in generator output)")
        if stale:
            sys.stderr.write("GEN-FRESH FAIL: %d generated SWAIG-payload file(s) stale:\n" % len(stale))
            for s in stale:
                sys.stderr.write("  - %s\n" % s)
            return 1
        print("GEN-FRESH: generated SWAIG-payload files match porting-sdk/swaig-specs/.")
        return 0

    for fn, src in outs.items():
        p = out_dir / fn
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(src)
    print(f"generated {len(outs)} SWAIG-payload file(s) into {out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
