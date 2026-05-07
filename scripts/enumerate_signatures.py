#!/usr/bin/env python3
"""enumerate_signatures.py — emit port_signatures.json for the .NET SDK.

Phase 2 of the cross-language signature audit. The pipeline is:

    1. Build SignalWire.dll (via ``dotnet build``).
    2. Run SignatureDump (a small C# program that uses System.Reflection
       to dump the assembly's public surface as raw JSON).
    3. This wrapper reads that raw JSON, applies the existing class→module
       mapping from enumerate_surface.py (CLASS_MODULE_MAP, CLASS_RENAME_MAP,
       SKILL_RENAMES, METHOD_RENAMES, SKIP_METHOD_NAMES), translates .NET
       types to canonical via porting-sdk/type_aliases.yaml (dotnet section),
       and emits port_signatures.json conforming to
       porting-sdk/surface_schema_v2.json.

Why split: the existing enumerate_surface.py owns the .NET → Python name
translation tables (~600 LOC). Reusing them via ``import`` is one line;
porting them to C# would duplicate everything and drift.

Usage:
    python3 scripts/enumerate_signatures.py
    python3 scripts/enumerate_signatures.py --strict
    python3 scripts/enumerate_signatures.py --raw raw_dump.json   # use existing dump
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

import yaml

HERE = Path(__file__).resolve().parent
PORT_ROOT = HERE.parent
PSDK = (PORT_ROOT.parent / "porting-sdk").resolve()
if not PSDK.is_dir():
    PSDK = Path("/usr/local/home/devuser/src/porting-sdk")

sys.path.insert(0, str(HERE))
from enumerate_surface import (  # type: ignore
    CLASS_MODULE_MAP, CLASS_RENAME_MAP, METHOD_RENAMES, MIXIN_PROJECTIONS,
    SKILL_RENAMES, SKIP_METHOD_NAMES, module_for_class, pascal_to_snake,
)


class TypeTranslationError(RuntimeError):
    pass


def load_aliases() -> dict[str, str]:
    data = yaml.safe_load((PSDK / "type_aliases.yaml").read_text(encoding="utf-8"))
    return {str(k): str(v) for k, v in data.get("aliases", {}).get("dotnet", {}).items()}


# ---------------------------------------------------------------------------
# .NET type translation
# ---------------------------------------------------------------------------

def split_generic(name: str) -> tuple[str, list[str]]:
    """Split ``Foo.Bar<A,B>`` into (``Foo.Bar``, [``A``, ``B``]).

    Returns (name, []) when not generic. Handles nested angle brackets
    correctly (depth-aware comma split inside the outermost <>)."""
    if not (name.endswith(">") and "<" in name):
        return name, []
    head, _, tail = name.partition("<")
    inner = tail[:-1]  # strip trailing >
    parts = []
    depth = 0
    buf = []
    for ch in inner:
        if ch == "<":
            depth += 1
        elif ch == ">":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append("".join(buf))
            buf.clear()
            continue
        buf.append(ch)
    if buf:
        parts.append("".join(buf))
    return head, [p.strip() for p in parts]


def translate_dotnet_type(t: str, aliases: dict[str, str], context: str) -> str:
    """Translate a .NET FullName into a canonical type string."""
    if t is None or t == "":
        return "any"
    t = t.strip()

    # Array suffix
    if t.endswith("[]"):
        inner = translate_dotnet_type(t[:-2], aliases, context)
        return f"list<{inner}>"

    # Generic type-variable placeholder emitted by SignatureDump
    if t.startswith("T:"):
        return "any"

    # Direct alias hit (covers System.String, System.Int32, etc.)
    if t in aliases:
        return aliases[t]

    head, generics = split_generic(t)
    if not generics:
        # Bare class. Look up in CLASS_MODULE_MAP via the short name; otherwise
        # synthesise a class:<...> reference using the SignalWire native
        # namespace → module translation.
        if head in aliases:
            return aliases[head]
        if head.startswith("SignalWire."):
            return _translate_sdk_class_ref(head)
        # Bare reference type from .NET stdlib that isn't in aliases — treat
        # as `any` for object/dynamic-y things, fail loud for everything else
        last = head.rsplit(".", 1)[-1]
        if last in aliases:
            return aliases[last]
        if last in ("Object",):
            return "any"
        raise TypeTranslationError(
            f"unknown bare type {head!r} at {context}; "
            f"add to porting-sdk/type_aliases.yaml under aliases.dotnet"
        )

    # Generic types — handle the ones with non-trivial canonical forms first
    canon_args = [translate_dotnet_type(a, aliases, context) for a in generics]

    # System.Threading.Tasks.Task<T>  →  unwrap to T (Python async returns T)
    if head in (
        "System.Threading.Tasks.Task",
        "System.Threading.Tasks.ValueTask",
    ):
        return canon_args[0] if canon_args else "void"

    # System.Func<...>: last arg is return type, others are arg types
    if head == "System.Func":
        if len(canon_args) >= 1:
            ret = canon_args[-1]
            args = canon_args[:-1]
            arg_list = ",".join(args) if args else ""
            return f"callable<list<{arg_list}>,{ret}>"
        return "callable<list<>,any>"

    # System.Action<...>: all args are inputs, returns void
    if head == "System.Action":
        arg_list = ",".join(canon_args)
        return f"callable<list<{arg_list}>,void>"

    # Predicate<T>
    if head == "System.Predicate":
        return f"callable<list<{canon_args[0]}>,bool>"

    # Generic collections
    if head in (
        "System.Collections.Generic.List",
        "System.Collections.Generic.IList",
        "System.Collections.Generic.IReadOnlyList",
        "System.Collections.Generic.IEnumerable",
        "System.Collections.Generic.ICollection",
        "System.Collections.Generic.IAsyncEnumerable",
        "System.Collections.Concurrent.ConcurrentQueue",
        "System.Collections.Concurrent.ConcurrentBag",
    ):
        return f"list<{canon_args[0]}>"
    if head in (
        "System.Collections.Generic.Dictionary",
        "System.Collections.Generic.IDictionary",
        "System.Collections.Generic.IReadOnlyDictionary",
        "System.Collections.Concurrent.ConcurrentDictionary",
    ):
        return f"dict<{canon_args[0]},{canon_args[1]}>"
    if head in ("System.Collections.Generic.HashSet",):
        return f"list<{canon_args[0]}>"
    # Generic future / async wrapper that carries no Python equivalent;
    # treat as the wrapped type.
    if head in (
        "System.Threading.Tasks.TaskCompletionSource",
    ):
        return canon_args[0] if canon_args else "any"

    # Tuples
    if head.startswith("System.Tuple") or head.startswith("System.ValueTuple"):
        return f"tuple<{','.join(canon_args)}>"

    # Generic SDK class — emit class:<canonical> dropping the type args (the
    # canonical inventory matches Python which generally doesn't use
    # parameterized class refs in signatures)
    if head.startswith("SignalWire."):
        return _translate_sdk_class_ref(head)

    if head in aliases:
        # Allow the alias table to define a mapping for parameterized heads
        # (rare); fall back to using the alias as-is.
        return aliases[head]

    raise TypeTranslationError(
        f"unknown generic type {head!r}<{','.join(generics)}> at {context}; "
        f"add to porting-sdk/type_aliases.yaml under aliases.dotnet "
        f"or extend translate_dotnet_type"
    )


def _translate_sdk_class_ref(full_name: str) -> str:
    """Translate ``SignalWire.Foo.Bar`` to ``class:<canonical>``."""
    namespace, _, name = full_name.rpartition(".")
    rename = CLASS_RENAME_MAP.get((namespace, name))
    if rename is not None:
        target_module, target_class = rename
        return f"class:{target_module}.{target_class}"
    canonical_name = SKILL_RENAMES.get(name, name)
    if canonical_name in CLASS_MODULE_MAP:
        target_module = CLASS_MODULE_MAP[canonical_name]
        return f"class:{target_module}.{canonical_name}"
    target_module = module_for_class(canonical_name, namespace)
    return f"class:{target_module}.{canonical_name}"


# ---------------------------------------------------------------------------
# Default-value canonicalisation
# ---------------------------------------------------------------------------

def canonical_default(raw, has_default: bool):
    """Return (default_present, default_value)."""
    if not has_default:
        return False, None
    return True, raw


# ---------------------------------------------------------------------------
# Method-name translation
# ---------------------------------------------------------------------------

ASYNC_SUFFIX = re.compile(r"Async$")


def canonical_method_name(name: str) -> str | None:
    if name in SKIP_METHOD_NAMES:
        return None
    # Compiler-generated method names (e.g. record types' ``<Clone>$``)
    if name.startswith("<") or "$" in name:
        return None
    # Strip Async suffix (matches Python convention)
    if ASYNC_SUFFIX.search(name) and name != "Async":
        name = ASYNC_SUFFIX.sub("", name)
    snake = pascal_to_snake(name)
    return METHOD_RENAMES.get(snake, snake)


# ---------------------------------------------------------------------------
# Building canonical inventory
# ---------------------------------------------------------------------------

def kind_for_param(p: dict) -> str | None:
    """Return canonical kind, or None to use default 'positional'."""
    k = p.get("kind", "normal")
    if k == "params":
        return "var_positional"
    if k in ("ref", "out", "in"):
        # .NET ref/out/in have no clean Python equivalent; treat as positional
        return None
    return None


def build_signature(method: dict, aliases: dict, context: str, is_static: bool) -> dict:
    params_out: list = []
    # Both instance methods AND constructors get self in canonical form
    # (Python __init__(self, ...)). Static methods don't.
    if not is_static:
        params_out.append({"name": "self", "kind": "self"})

    for p in method.get("parameters", []):
        ctx = f"{context}[{p.get('name')}]"
        # Normalize parameter names from PascalCase/camelCase to snake_case so
        # diffs against Python line up. C# parameter names are usually
        # camelCase; properties exposed as constructor args may be PascalCase.
        native_name = p.get("name", "")
        canonical_name = pascal_to_snake(native_name) if native_name else native_name
        param: dict = {"name": canonical_name}

        kind = kind_for_param(p)
        if kind is not None:
            param["kind"] = kind

        # Type
        canon = translate_dotnet_type(p.get("type", ""), aliases, ctx)
        if p.get("nullable") and not canon.startswith("optional<"):
            canon = f"optional<{canon}>"
        param["type"] = canon

        # Required / default
        has_default, default = canonical_default(p.get("default"), p.get("has_default", False))
        if has_default:
            param["required"] = False
            param["default"] = default
        else:
            param["required"] = True
        params_out.append(param)

    if method.get("is_constructor"):
        return_canonical = "void"
    else:
        return_canonical = translate_dotnet_type(
            method.get("return_type", "System.Void"), aliases, context + "[->]"
        )
        if method.get("return_nullable") and not return_canonical.startswith("optional<"):
            return_canonical = f"optional<{return_canonical}>"
    return {"params": params_out, "returns": return_canonical}


def _load_python_param_counts() -> dict[str, int]:
    """Load Python reference signatures and index method → param count.
    Used by collect() to pick the best-matching overload from .NET's
    multiple definitions of the same method."""
    py_path = PSDK / "python_signatures.json"
    if not py_path.is_file():
        return {}
    try:
        d = json.loads(py_path.read_text(encoding="utf-8"))
    except Exception:
        return {}
    out: dict[str, int] = {}
    for mod, mod_entry in d.get("modules", {}).items():
        for cls, cls_entry in mod_entry.get("classes", {}).items():
            for m, sig in cls_entry.get("methods", {}).items():
                out[f"{mod}.{cls}.{m}"] = len(sig.get("params", []))
        for fn, sig in mod_entry.get("functions", {}).items():
            out[f"{mod}.{fn}"] = len(sig.get("params", []))
    return out


_PY_PARAM_COUNTS = _load_python_param_counts()


def collect(raw: dict, aliases: dict) -> tuple[dict, list]:
    out_modules: dict = {}
    failures: list = []

    for type_entry in raw.get("types", []):
        ns = type_entry.get("namespace", "")
        name = type_entry.get("name", "")
        if name.startswith("<") or "AnonymousType" in name:
            continue
        kind = type_entry.get("kind", "class")
        if kind == "enum":
            continue  # not part of the signature inventory in v1

        # Resolve canonical (module, class) for this type
        rename = CLASS_RENAME_MAP.get((ns, name))
        if rename is not None:
            target_module, target_class = rename
        else:
            canonical_name = SKILL_RENAMES.get(name, name)
            # CLASS_MODULE_MAP is keyed by the .NET native (pre-rename) name
            # for skills (DatasphereSkill, McpGatewaySkill, etc.). Try the
            # native key first, fall back to the canonical key for cases
            # like AgentBase where the names already match.
            if name in CLASS_MODULE_MAP:
                target_module = CLASS_MODULE_MAP[name]
                target_class = canonical_name
            elif canonical_name in CLASS_MODULE_MAP:
                target_module = CLASS_MODULE_MAP[canonical_name]
                target_class = canonical_name
            else:
                target_module = module_for_class(canonical_name, ns)
                target_class = canonical_name

        methods_out: dict = {}

        for m in type_entry.get("methods", []):
            method_native = m.get("name", "")
            if method_native == "__init__":
                method_canonical = "__init__"
            else:
                method_canonical = canonical_method_name(method_native)
                if method_canonical is None:
                    continue
            ctx = f"{target_module}.{target_class}.{method_canonical}"
            try:
                sig = build_signature(
                    m, aliases, ctx,
                    is_static=m.get("is_static", False),
                )
            except TypeTranslationError as e:
                failures.append(str(e))
                continue
            # If a method already exists at this name (overload), prefer
            # the overload whose parameter count best matches the Python
            # reference if we know it; otherwise keep the longest. This
            # avoids picking a 1-arg convenience overload when Python's
            # canonical signature is multi-param.
            if method_canonical in methods_out:
                existing = methods_out[method_canonical]
                py_count = _PY_PARAM_COUNTS.get(f"{target_module}.{target_class}.{method_canonical}")
                if py_count is not None:
                    new_diff = abs(len(sig["params"]) - py_count)
                    old_diff = abs(len(existing["params"]) - py_count)
                    if new_diff >= old_diff:
                        continue
                else:
                    if len(sig["params"]) <= len(existing["params"]):
                        continue
            methods_out[method_canonical] = sig

        # Properties → emit as zero-arg methods on the same class (matches
        # Python @property convention: name + (self), returning the type).
        for p in type_entry.get("properties", []):
            pname = p.get("name", "")
            method_canonical = canonical_method_name(pname)
            if method_canonical is None or method_canonical in methods_out:
                continue
            ctx = f"{target_module}.{target_class}.{method_canonical}"
            try:
                ret = translate_dotnet_type(p.get("type", ""), aliases, ctx + "[->]")
            except TypeTranslationError as e:
                failures.append(str(e))
                continue
            params_out = []
            if not p.get("is_static", False):
                params_out.append({"name": "self", "kind": "self"})
            methods_out[method_canonical] = {"params": params_out, "returns": ret}

        if not methods_out:
            continue

        out_modules.setdefault(target_module, {"classes": {}})
        out_modules[target_module]["classes"][target_class] = {
            "methods": dict(sorted(methods_out.items())),
        }

    # Mixin projection: replicate methods present on AgentBase under each
    # Python mixin module, then remove them from AgentBase so the diff
    # against python_signatures.json doesn't flag them as extras (Python
    # keeps them only on the mixin class). Mirrors enumerate_surface.py.
    ab_entry = out_modules.get("signalwire.core.agent_base", {}).get("classes", {}).get("AgentBase")
    svc_entry = out_modules.get("signalwire.core.swml_service", {}).get("classes", {}).get("SWMLService")
    if ab_entry is not None or svc_entry is not None:
        ab_methods = ab_entry["methods"] if ab_entry else {}
        svc_methods = svc_entry["methods"] if svc_entry else {}
        # Methods inherited via Service -> AgentBase chain are present on
        # the parent class; check both when projecting. AgentBase wins
        # when both define the same name (override).
        combined = {**svc_methods, **ab_methods}
        projected_names: set[str] = set()
        for (mod, cls), expected_methods in MIXIN_PROJECTIONS.items():
            present = {m: combined[m] for m in expected_methods if m in combined}
            if not present:
                continue
            out_modules.setdefault(mod, {"classes": {}})
            out_modules[mod]["classes"].setdefault(cls, {"methods": {}})
            out_modules[mod]["classes"][cls]["methods"].update(present)
            projected_names.update(present)
        # Pop the projected names from AgentBase only (Service methods
        # remain on Service since SWMLService is itself a Python class
        # with its own method set).
        for n in projected_names:
            ab_methods.pop(n, None)
        if ab_entry and not ab_methods:
            out_modules["signalwire.core.agent_base"]["classes"].pop("AgentBase", None)
            if not out_modules["signalwire.core.agent_base"]["classes"]:
                out_modules.pop("signalwire.core.agent_base")

    # Static-helper-class -> free-function projection. C# has no free
    # functions; Python module-level helpers (validate_url,
    # is_serverless_mode, etc.) live on a static class in C# but the
    # cross-language audit needs them at the module's free-function path.
    # When a class in this list lives at ``mod.ClassName``, its public
    # methods are also emitted as ``mod.method_name`` free functions.
    # ``mod -> ClassName -> only-these-methods`` projection. None means
    # all public methods (except __init__).
    STATIC_TO_FREE_FN: dict[tuple[str, str], list[str] | None] = {
        # Project only ``validate_url`` — the ``with_resolved_addresses``
        # overload is .NET-test-only scaffolding.
        ("signalwire.utils.url_validator", "UrlValidator"): ["validate_url"],
        # WebhookValidator's static methods mirror Python's module-level
        # ``signalwire.core.security.webhook_validator`` functions
        # (``validate_webhook_signature`` and ``validate_request``).
        ("signalwire.core.security.webhook_validator", "WebhookValidator"): None,
    }
    for (mod, cls), allowed in STATIC_TO_FREE_FN.items():
        cls_entry = out_modules.get(mod, {}).get("classes", {}).get(cls)
        if not cls_entry:
            continue
        out_modules[mod].setdefault("functions", {})
        for mname, msig in cls_entry["methods"].items():
            if mname == "__init__":
                continue
            if allowed is not None and mname not in allowed:
                continue
            free_sig = {
                "params": [p for p in msig["params"] if p.get("kind") not in ("self", "cls")],
                "returns": msig["returns"],
            }
            out_modules[mod]["functions"].setdefault(mname, free_sig)
        # Drop the class entry entirely — these helpers are .NET-only
        # scaffolding (C# has no free functions). Keeping the static class
        # in the inventory creates phantom missing-reference entries.
        del out_modules[mod]["classes"][cls]
        if not out_modules[mod]["classes"]:
            del out_modules[mod]["classes"]

    # Per-method free-function routing for static helpers whose methods
    # land at DIFFERENT Python modules. .NET groups several helpers on
    # one static class for ergonomics; Python scatters them.
    # Map: (source_mod, ClassName, source_method) ->
    #      (target_mod, target_function_name).
    STATIC_METHOD_FREE_FN_PROJECTIONS: dict[
        tuple[str, str, str], tuple[str, str]
    ] = {
        ("signalwire.utils.execution_mode", "ExecutionMode", "is_serverless_mode"):
            ("signalwire.utils", "is_serverless_mode"),
        ("signalwire.utils.execution_mode", "ExecutionMode", "get_execution_mode"):
            ("signalwire.core.logging_config", "get_execution_mode"),
    }
    cls_to_drop: set[tuple[str, str]] = set()
    for (src_mod, src_cls, src_method), (tgt_mod, tgt_fn) in STATIC_METHOD_FREE_FN_PROJECTIONS.items():
        cls_entry = out_modules.get(src_mod, {}).get("classes", {}).get(src_cls)
        if not cls_entry:
            continue
        msig = cls_entry["methods"].get(src_method)
        if msig is None:
            continue
        out_modules.setdefault(tgt_mod, {}).setdefault("functions", {})
        free_sig = {
            "params": [p for p in msig["params"] if p.get("kind") not in ("self", "cls")],
            "returns": msig["returns"],
        }
        out_modules[tgt_mod]["functions"].setdefault(tgt_fn, free_sig)
        cls_to_drop.add((src_mod, src_cls))
    # Drop source classes once all methods have been projected.
    for (src_mod, src_cls) in cls_to_drop:
        if src_mod in out_modules and "classes" in out_modules[src_mod]:
            out_modules[src_mod]["classes"].pop(src_cls, None)
            if not out_modules[src_mod]["classes"]:
                del out_modules[src_mod]["classes"]
        # If module is now empty, drop it
        if src_mod in out_modules and not out_modules[src_mod]:
            del out_modules[src_mod]

    # Sort modules + classes deterministically
    sorted_modules = {}
    for mod in sorted(out_modules):
        entry = out_modules[mod]
        sorted_modules[mod] = {}
        if entry.get("classes"):
            sorted_modules[mod]["classes"] = {
                cls: {"methods": dict(sorted(entry["classes"][cls]["methods"].items()))}
                for cls in sorted(entry["classes"])
            }
        if entry.get("functions"):
            sorted_modules[mod]["functions"] = dict(sorted(entry["functions"].items()))

    return {
        "version": "2",
        "generated_from": "SignalWire.dll via SignatureDump (System.Reflection)",
        "modules": sorted_modules,
    }, failures


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def run_dump() -> dict:
    """Build + run SignatureDump and parse its stdout.

    Notes:
      * Drop ``--no-restore`` — the SignalWire SDK is multi-target
        (net8.0/net9.0/net10.0) and the targeting packs for the deps must
        be present at run time. Forcing ``--no-restore`` causes
        ``NETSDK1127: targeting pack Microsoft.NETCore.App is not installed``
        on a fresh checkout (or after a ``dotnet clean``).
      * stderr is folded into stdout so a build error is visible in the
        exception message even when the dotnet runner only prints to stderr.
    """
    # Resolve `dotnet` from $PATH (CI / fresh checkouts), with a fallback
    # to the developer-machine path that pre-dates the PATH-aware variant.
    import shutil
    dotnet = shutil.which("dotnet") or "/home/devuser/.local/bin/dotnet"
    cmd = [
        dotnet, "run", "--project",
        str(HERE / "SignatureDump" / "SignatureDump.csproj"),
    ]
    cp = subprocess.run(
        cmd, cwd=PORT_ROOT, capture_output=True, text=True, timeout=600,
    )
    if cp.returncode != 0:
        raise RuntimeError(
            f"SignatureDump failed (exit {cp.returncode}):\n"
            f"--- stdout ---\n{cp.stdout}\n--- stderr ---\n{cp.stderr}"
        )
    # SignatureDump prints the JSON document to stdout. dotnet run prepends
    # build messages; the JSON starts at the first ``{`` line.
    out = cp.stdout
    brace = out.find("{")
    if brace < 0:
        raise RuntimeError(f"SignatureDump produced no JSON; stdout was:\n{out}")
    return json.loads(out[brace:])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--raw", type=Path, default=None,
        help="Path to a pre-dumped SignatureDump JSON; skips the dotnet run.",
    )
    parser.add_argument(
        "--out", type=Path,
        default=PORT_ROOT / "port_signatures.json",
    )
    parser.add_argument("--strict", action="store_true",
                        help="Exit non-zero if any type fails to translate.")
    args = parser.parse_args()

    if args.raw and args.raw.is_file():
        text = args.raw.read_text(encoding="utf-8")
        brace = text.find("{")
        if brace < 0:
            raise SystemExit(f"--raw {args.raw} contains no JSON document")
        raw = json.loads(text[brace:])
    else:
        raw = run_dump()

    aliases = load_aliases()
    canonical, failures = collect(raw, aliases)

    if failures:
        print(
            f"enumerate_signatures: {len(failures)} translation failure(s)",
            file=sys.stderr,
        )
        for f in failures[:30]:
            print(f"  - {f}", file=sys.stderr)
        if len(failures) > 30:
            print(f"  ... ({len(failures) - 30} more)", file=sys.stderr)
        if args.strict:
            return 1

    args.out.write_text(
        json.dumps(canonical, indent=2, sort_keys=False) + "\n",
        encoding="utf-8",
    )
    n_mods = len(canonical["modules"])
    n_classes = sum(len(m.get("classes", {})) for m in canonical["modules"].values())
    n_methods = sum(
        sum(len(c["methods"]) for c in m.get("classes", {}).values())
        for m in canonical["modules"].values()
    )
    print(
        f"enumerate_signatures: wrote {args.out} "
        f"({n_mods} modules, {n_classes} classes, {n_methods} methods)"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
