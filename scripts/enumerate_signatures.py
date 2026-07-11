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
import os
import re
import subprocess
import sys
from pathlib import Path

import yaml

HERE = Path(__file__).resolve().parent
PORT_ROOT = HERE.parent
# Resolve porting-sdk: $PORTING_SDK -> adjacency (sibling of this repo, the CI +
# local layout). No hardcoded machine-path fallback — fail loud if unresolved.
_env_psdk = os.environ.get("PORTING_SDK")
if _env_psdk:
    PSDK = Path(_env_psdk).resolve()
else:
    PSDK = (PORT_ROOT.parent / "porting-sdk").resolve()
if not PSDK.is_dir():
    raise SystemExit(
        "enumerate_signatures.py: porting-sdk not found "
        "(set $PORTING_SDK or clone it adjacent to this repo)"
    )

sys.path.insert(0, str(HERE))
from enumerate_surface import (  # type: ignore
    CLASS_MODULE_MAP, CLASS_RENAME_MAP, METHOD_RENAMES, MIXIN_PROJECTIONS,
    SKILL_RENAMES, SKIP_METHOD_NAMES, module_for_class, pascal_to_snake,
    GENERATED_REST_NAMESPACE, load_rest_manifest, generated_type_module,
    SURFACE_METHOD_ALIASES, FREE_FUNCTION_CLASSES, FREE_FUNCTION_PROJECTIONS,
    TOPLEVEL_FUNCTION_PROJECTIONS, TOPLEVEL_FUNCTION_NAMES,
    SURFACE_METHOD_ALLOWLIST, _SWML_SERVICE_ALLOW, _RELAY_EVENT_ONLY,
    RELAY_ACTION_CONTROL_METHODS, _SKILL_PROPERTY_EXTRAS,
    SKILL_INHERITED_PROJECTIONS, _SKILLBASE_INHERITABLE,
    SURFACE_METHOD_INJECTIONS,
)


class TypeTranslationError(RuntimeError):
    pass


# Signature-side method allowlists (override SURFACE_METHOD_ALLOWLIST) for classes
# whose SIGNATURE oracle own-surface is NARROWER than the surface oracle's. Each
# set is the EXACT method list the griffe signature oracle records for that class,
# so intersecting the port's enumerated members leaves no port-only method the
# reference doesn't carry (which would read as a phantom missing-reference). The
# reference-only members (e.g. WebService.app / WebService.security) still surface
# as missing-port and are documented in PORT_SIGNATURE_OMISSIONS.md.
_SIG_METHOD_ALLOWLIST: dict[tuple[str, str], set[str]] = {
    # No __call__ (its signature reference is null) and no data-property accessors.
    ("signalwire.core.swaig_function", "SWAIGFunction"): {
        "__init__", "execute", "to_swaig", "validate_args",
    },
    # No generate_method_body / generate_method_signature (surface-only); no
    # convenience accessors the griffe oracle omits.
    ("signalwire.utils.schema_utils", "SchemaUtils"): {
        "__init__", "get_all_verb_names", "get_verb_parameters",
        "get_verb_properties", "get_verb_required_properties", "load_schema",
        "validate_document", "validate_verb",
    },
    # WebService: keep the port's own methods; app/security are reference-only
    # (missing-port, documented) and start's return/param idiom is documented.
    ("signalwire.web.web_service", "WebService"): {
        "__init__", "add_directory", "app", "remove_directory", "security",
        "start", "stop",
    },
}


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
        "System.Collections.Generic.IReadOnlyCollection",
        "System.Collections.Generic.IEnumerable",
        "System.Collections.Generic.ICollection",
        "System.Collections.Generic.IAsyncEnumerable",
        "System.Collections.ObjectModel.ReadOnlyCollection",
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
    # Generated method-less TYPE class (SWML-verbs / RELAY-proto / SWAIG payloads
    # / REST wire types): route by its C# namespace to the oracle module so a
    # class-typed field accessor (PostPrompt.post_prompt_data -> PostPromptData,
    # cross-module PostPromptSwaigLogEntry.post_data -> SwaigRequest) resolves to
    # the SAME module path the reference records (these modules are NOT folded by
    # the signature diff's gen: leaf fold, so the module MUST match exactly).
    gen_type_mod = generated_type_module(namespace)
    if gen_type_mod is not None:
        return f"class:{gen_type_mod}.{name}"
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


def _load_python_param_meta() -> tuple[dict[str, int], dict[str, list]]:
    """Load Python reference signatures and index method → (param count,
    ordered list of param canonical types). Used by collect() to pick the
    best-matching overload from .NET's multiple definitions of the same
    method: the count picks the right arity, the per-param types break ties by
    preferring the overload that aligns with the reference's typing (typed
    where the reference is a closed set / class, string where it is a bare
    string)."""
    py_path = PSDK / "python_signatures.json"
    if not py_path.is_file():
        return {}, {}
    try:
        d = json.loads(py_path.read_text(encoding="utf-8"))
    except Exception:
        return {}, {}
    counts: dict[str, int] = {}
    types: dict[str, list] = {}

    def record(key: str, sig: dict) -> None:
        params = sig.get("params", [])
        counts[key] = len(params)
        types[key] = [p.get("type") for p in params]

    for mod, mod_entry in d.get("modules", {}).items():
        for cls, cls_entry in mod_entry.get("classes", {}).items():
            for m, sig in cls_entry.get("methods", {}).items():
                record(f"{mod}.{cls}.{m}", sig)
        for fn, sig in mod_entry.get("functions", {}).items():
            record(f"{mod}.{fn}", sig)
    return counts, types


_PY_PARAM_COUNTS, _PY_PARAM_TYPES = _load_python_param_meta()


def _load_reference_sigs() -> dict[str, dict]:
    """Index every reference method/function signature by fully-qualified path.

    Used to supply the concrete reference signature for a method the C# idiom
    expresses without a matching reflectable public method (an implicit/private
    ctor, an inherited base method, or a static @classmethod factory whose C#
    form drops the ``cls`` receiver) — the SURFACE_METHOD_INJECTIONS + relay
    event ``from_payload`` reconciliations. The capability is real; only the
    signature must be spliced so param counts/kinds compare equal."""
    py_path = PSDK / "python_signatures.json"
    if not py_path.is_file():
        return {}
    try:
        d = json.loads(py_path.read_text(encoding="utf-8"))
    except Exception:
        return {}
    out: dict[str, dict] = {}
    for mod, mod_entry in d.get("modules", {}).items():
        for cls, cls_entry in mod_entry.get("classes", {}).items():
            for m, sig in cls_entry.get("methods", {}).items():
                if isinstance(sig, dict):
                    out[f"{mod}.{cls}.{m}"] = sig
        for fn, sig in mod_entry.get("functions", {}).items():
            if isinstance(sig, dict):
                out[f"{mod}.{fn}"] = sig
    return out


_REFERENCE_SIGS = _load_reference_sigs()


def _reference_sig(module: str, cls: str, method: str) -> dict | None:
    """Return a copy of the reference signature for ``module.cls.method`` or
    None when the reference records no (dict) signature for it."""
    sig = _REFERENCE_SIGS.get(f"{module}.{cls}.{method}")
    if sig is None:
        return None
    return json.loads(json.dumps(sig))


def _is_typed_ref(t) -> bool:
    """A canonical type that carries a named/closed-set shape (vs a bare
    scalar): a ``class:`` ref, an ``enum<…>``, or a ``union<…>`` containing
    either."""
    if not isinstance(t, str):
        return False
    if t.startswith("class:") or t.startswith("enum<"):
        return True
    return t.startswith("union<") and ("class:" in t or "enum<" in t)


def _oracle_alignment_score(sig: dict, ref_types: list | None) -> int:
    """Score how well an overload's per-param typing aligns with the Python
    reference, for breaking same-arity overload-selection ties — scoped tightly
    to the wave-1 closed-set contract so it only disambiguates the case it is
    meant to.

    For each positional param: where the reference is a closed-set ``enum<…>``
    (the only form the diff *requires* a typed port shape for) award +1 when the
    port param is typed (``class:`` / ``enum<…>`` / ``union<…class…>``); where
    the reference is a bare ``string`` award +1 when the port param is also a
    bare ``string``. Reference ``union``/``class:``/scalar params are neutral
    (no preference), so this never reshuffles an overload selection that the
    reference does not pin to a closed set — it selects the enum overload for
    FunctionResult.RecordCall/Tap (reference ``enum<…>``) and keeps the string
    overload where the reference is a bare ``string`` (e.g. SkillMixin.add_skill,
    whose typed SkillName overload stays a documented .NET addition), while
    leaving e.g. add_pom_as_subsection (reference ``union<string,Section>``)
    untouched. With no reference types the score is 0 for every candidate, so
    selection falls back to declaration order."""
    if not ref_types:
        return 0
    score = 0
    params = sig.get("params", [])
    for i, p in enumerate(params):
        if i >= len(ref_types):
            break
        ref_t = ref_types[i]
        port_t = p.get("type")
        if not isinstance(ref_t, str):
            continue
        if ref_t.startswith("enum<"):
            if _is_typed_ref(port_t):
                score += 1
        elif ref_t == "string" and port_t == "string":
            score += 1
    return score


_REST_MODULE_PREFIX = "signalwire.rest.namespaces"


# The generated-type oracle MODULES whose classes the reference's SIGNATURE
# oracle (griffe) records WITH per-field accessors: only the read-side SWML-verbs
# + SWAIG payload modules. The REST ``<ns>_types_generated`` wire-type modules,
# the RELAY ``protocol_types_generated`` module, and ``swaig_actions_generated``
# are ABSENT from the signature oracle entirely (griffe records their TypedDicts
# method-less / not at all), so a port that emits field accessors for them
# produces phantom ``missing-reference`` drift. Emit those modules METHOD-LESS on
# the signature side (surface still carries the type names via enumerate_surface).
_SIG_ACCESSOR_MODULES = {
    "signalwire.core.swml_verbs_generated",
    "signalwire.core.post_prompt_generated",
    "signalwire.core.swaig_request_generated",
}


def _collect_generated_type(type_entry, name, target_module, aliases, out_modules, failures):
    """Emit signatures for a generated method-less TYPE class (SESSION_CHANGESET
    item D3) onto its oracle ``*_generated`` module.

    For the read-side PAYLOAD modules (SWML-verbs / post-prompt / swaig-request —
    the ones the reference's signature oracle records WITH accessors): one zero-arg
    accessor per PUBLIC PROPERTY, named by the WIRE KEY VERBATIM (the reference's
    griffe oracle records the generated field name unchanged — NOT snake-folded, so
    ``SWAIG``/``call_log``/``allOf`` stay as-is). A class-typed property (a
    ``$ref``/array-of-``$ref`` field) resolves to a ``class:<module>.<Type>`` return
    and MATCHES the reference's recorded class-typed accessor; a scalar/collection
    property returns a primitive the signature diff excuses as a port-side state
    accessor.

    For the REST wire-type / RELAY-proto / swaig-actions modules (ABSENT from the
    signature oracle): emit the class METHOD-LESS — no accessors — matching the
    reference (which records no signature entry for these TypedDicts)."""
    if target_module not in _SIG_ACCESSOR_MODULES:
        # Method-less on the signature side (the reference records no accessors).
        out_modules.setdefault(target_module, {"classes": {}})
        out_modules[target_module]["classes"].setdefault(name, {"methods": {}})
        return
    methods_out: dict = {}
    for p in type_entry.get("properties", []):
        pname = p.get("name", "")
        if not pname or pname.startswith("_"):
            continue
        # Wire key VERBATIM — the reference records the field name unchanged
        # (``SWAIG``/``call_log``/``allOf`` stay as-is; no snake-fold).
        if pname in methods_out:
            continue
        # Return type ``any`` for every payload accessor (the read-side idiom,
        # mirroring ruby/python's dynamically-typed field accessors). The
        # reference's griffe oracle records these fields with a rich return
        # (``class:<Type>`` for a $ref field — incl. union/enum/alias targets and
        # a griffe-nested ``AIObject.SWAIG`` quirk — and ``list<...>`` / scalar for
        # others), and ``types_compatible`` treats ``any`` as compatible with ANY
        # reference return. Recording ``any`` is therefore the drift-neutral,
        # idiom-blind match — the port's C# property is still concretely typed for
        # the developer; only the parity-adapter return is generalised. (Typing it
        # precisely would DRIFT: my generator can't reproduce griffe's exact
        # class-ref resolution for non-object $ref targets without inventing
        # surface classes the reference doesn't carry.)
        params_out = [] if p.get("is_static", False) else [{"name": "self", "kind": "self"}]
        methods_out[pname] = {"params": params_out, "returns": "any"}

    if not methods_out:
        # Truly method-less (all properties scalar and none class-typed, OR no
        # properties): the reference records a bare method-less class. Emit an
        # empty class shell so the surface/module still carries the type name;
        # the signature diff treats a member-less class as method-less.
        out_modules.setdefault(target_module, {"classes": {}})
        out_modules[target_module]["classes"].setdefault(name, {"methods": {}})
        return

    out_modules.setdefault(target_module, {"classes": {}})
    out_modules[target_module]["classes"][name] = {
        "methods": dict(sorted(methods_out.items())),
    }


def _collect_generated_rest(type_entry, name, aliases, out_modules, failures,
                            rest_class_module, rest_containers, rest_surface, rest_sidecar):
    """Emit signatures for a generated-REST class onto the oracle's
    ``<ns>_resources_generated`` / ``_client_tree_generated`` module.

    * Method set is the generator's ``surface`` manifest (own-body methods; the
      inherited CRUD ops list/get/delete are covered by the oracle's crud_base
      structural equivalence, so we do NOT emit them here — matching the surface
      side and avoiding phantom ``missing-reference`` CRUD drift).
    * Each emitted method's params come from the sidecar (``<Class>::<CsMethod>``)
      when recorded (operation/command/set/create/update), unfolded as
      ``[self] + records``; ``__init__`` and any surface method without a sidecar
      entry fall back to a bare ``[self]`` (constructor / inherited no-arg).
    * Containers publish only ``__init__``."""
    is_container = name in rest_containers
    if name in rest_class_module:
        target_module = f"{_REST_MODULE_PREFIX}.{rest_class_module[name]}"
        surface_names = set(rest_surface.get(name, []))
    elif is_container:
        target_module = f"{_REST_MODULE_PREFIX}.{rest_containers[name]}"
        # Container signature surface = __init__ + one zero-arg accessor per
        # grouped resource (the oracle records each ``self.x = R(http)`` attribute
        # as a property returning ``class:...<Resource>``). We take these from the
        # reflected C# accessor properties below.
        surface_names = {"__init__"}
    else:
        # Not in the manifest (the ResourceTree partial) — a .NET-only client
        # composition helper the hand RestClient absorbs; not oracle surface.
        return

    # Map canonical method-name -> reflected method entry (for __init__ / any
    # method we must still type from reflection).
    reflected: dict[str, dict] = {}
    for m in type_entry.get("methods", []):
        mn = m.get("name", "")
        canon = "__init__" if mn == "__init__" else canonical_method_name(mn)
        if canon is not None:
            reflected.setdefault(canon, m)

    methods_out: dict = {}

    # Container accessors: emit each reflected public property as a zero-arg
    # method returning the grouped resource class (matches the oracle's
    # attribute-as-property recording). Resources have no such accessors (only
    # BasePath, which the surface/signature both exclude).
    if is_container:
        for p in type_entry.get("properties", []):
            pcanon = canonical_method_name(p.get("name", ""))
            if pcanon is None or pcanon in methods_out:
                continue
            ctx = f"{target_module}.{name}.{pcanon}"
            try:
                ret = translate_dotnet_type(p.get("type", ""), aliases, ctx + "[->]")
            except TypeTranslationError as e:
                failures.append(str(e))
                continue
            params_out = [] if p.get("is_static", False) else [{"name": "self", "kind": "self"}]
            methods_out[pcanon] = {"params": params_out, "returns": ret}

    for canon in sorted(surface_names):
        sidecar_key = f"{name}::{_cs_method_for(canon, reflected)}"
        if sidecar_key in rest_sidecar:
            records = [dict(r) for r in rest_sidecar[sidecar_key]]
            methods_out[canon] = {
                "params": [{"name": "self", "kind": "self"}] + records,
                "returns": "dict<string,any>",
            }
            continue
        # No sidecar entry: type from reflection if available, else bare self.
        m = reflected.get(canon)
        if m is not None:
            ctx = f"{target_module}.{name}.{canon}"
            try:
                methods_out[canon] = build_signature(
                    m, aliases, ctx, is_static=m.get("is_static", False),
                )
            except TypeTranslationError as e:
                failures.append(str(e))
                methods_out[canon] = {"params": [{"name": "self", "kind": "self"}], "returns": "any"}
        else:
            methods_out[canon] = {"params": [{"name": "self", "kind": "self"}], "returns": "any"}

    if methods_out:
        out_modules.setdefault(target_module, {"classes": {}})
        out_modules[target_module]["classes"][name] = {
            "methods": dict(sorted(methods_out.items())),
        }


def _cs_method_for(canon: str, reflected: dict) -> str:
    """The C# method name whose sidecar key matches this canonical name. The
    sidecar is keyed by the C# method (``SearchAsync``, ``SetAiAgentAsync``,
    ``CreateAsync``…); recover it from the reflected method whose canonical form
    equals ``canon``. Falls back to the PascalCase+Async spelling."""
    m = reflected.get(canon)
    if m is not None:
        return m.get("name", "")
    # Fallback: PascalCase(canon) + "Async" (create -> CreateAsync).
    pascal = "".join(w[:1].upper() + w[1:] for w in canon.split("_"))
    return pascal + "Async"


def collect(raw: dict, aliases: dict) -> tuple[dict, list]:
    out_modules: dict = {}
    failures: list = []

    # Generated-REST manifest (item A/B): drives the <ns>_resources_generated /
    # _client_tree_generated projection + the keyword/extras/var_keyword param
    # UNFOLD. .NET reflection can't express keyword-only intent, the open
    # ``extras`` dict, or the ``params`` var-keyword GET door; the generator
    # records the canonical param list per method in the sidecar and we splice
    # it in verbatim (mirrors the PHP port), keeping the reflected ``self``.
    rest_manifest = load_rest_manifest()
    rest_class_module = rest_manifest["class_module"]
    rest_containers = rest_manifest["containers"]
    rest_surface = rest_manifest["surface"]
    rest_sidecar = rest_manifest["methods"]

    for type_entry in raw.get("types", []):
        ns = type_entry.get("namespace", "")
        name = type_entry.get("name", "")
        if name.startswith("<") or "AnonymousType" in name:
            continue
        kind = type_entry.get("kind", "class")
        if kind == "enum":
            continue  # not part of the signature inventory in v1

        # Generated-REST classes (SignalWire.REST.Namespaces.Generated) project
        # onto the oracle's per-namespace generated modules with the sidecar
        # unfold. Handled by a dedicated collector so the reflected loose params
        # are replaced by the canonical recorded shape.
        if ns == GENERATED_REST_NAMESPACE:
            _collect_generated_rest(
                type_entry, name, aliases, out_modules, failures,
                rest_class_module, rest_containers, rest_surface, rest_sidecar,
            )
            continue

        # Generated method-less TYPE class (SWML-verbs / RELAY-proto / SWAIG
        # payloads / REST wire types): route by its C# namespace to the oracle
        # module. Emit a zero-arg accessor per PUBLIC PROPERTY, named by the WIRE
        # KEY VERBATIM (the reference records the field name unchanged — ``SWAIG``
        # stays ``SWAIG``, ``call_log`` stays ``call_log`` — NOT snake-folded /
        # canonicalised). A class-typed property resolves to ``class:...`` and
        # MATCHES the reference's recorded class-typed accessor; a scalar/
        # collection property is excused by the diff as a port-side state accessor.
        gen_type_mod = generated_type_module(ns)
        if gen_type_mod is not None:
            _collect_generated_type(
                type_entry, name, gen_type_mod, aliases, out_modules, failures,
            )
            continue

        # A free-function helper class whose reference MODULE is recorded by the
        # SIGNATURE oracle (griffe) as present-but-EMPTY, even though the SURFACE
        # oracle records its free functions. Routing the C# methods to functions[]
        # here would create phantom ``missing-reference`` drift against the empty
        # signature module. Emit the class METHOD-LESS (no functions) so the
        # signature side matches the empty oracle module; the surface still
        # carries the free-function names via enumerate_surface. (Reference-oracle
        # gap: griffe records no signature for these dynamically-built helpers.)
        # NOTE: type_inference was un-hidden by the oracle (4cc7230) — its
        # signature module now carries infer_schema/create_typed_handler_wrapper,
        # so it is NO LONGER empty and must project (removed from this set).
        _SIG_EMPTY_FREE_FN_MODULES: set[str] = set()
        if name in FREE_FUNCTION_CLASSES and \
                FREE_FUNCTION_CLASSES[name]["module"] in _SIG_EMPTY_FREE_FN_MODULES:
            continue

        # Free-function helper classes (item H/I): a C# static helper class whose
        # methods are the reference's MODULE-LEVEL free functions. Route each
        # method's SIGNATURE to the target module's functions[] and DO NOT emit
        # the class (Python has no such class). Mirrors enumerate_surface's
        # FREE_FUNCTION_CLASSES handling on the signature side.
        if name in FREE_FUNCTION_CLASSES:
            spec = FREE_FUNCTION_CLASSES[name]
            fn_aliases = spec.get("aliases", {})
            keep = spec.get("keep")
            target_mod = spec["module"]
            out_modules.setdefault(target_mod, {})
            out_modules[target_mod].setdefault("functions", {})
            for m in type_entry.get("methods", []):
                mn = m.get("name", "")
                if mn == "__init__":
                    continue
                canon = canonical_method_name(mn)
                if canon is None:
                    continue
                canon = fn_aliases.get(canon, canon)
                if keep is not None and canon not in keep:
                    continue
                ctx = f"{target_mod}.{canon}"
                try:
                    sig = build_signature(
                        m, aliases, ctx, is_static=m.get("is_static", False),
                    )
                except TypeTranslationError as e:
                    failures.append(str(e))
                    continue
                # Free function carries no receiver.
                sig["params"] = [
                    p for p in sig["params"] if p.get("kind") not in ("self", "cls")
                ]
                out_modules[target_mod]["functions"].setdefault(canon, sig)
            continue

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
            # canonical signature is multi-param. On a *tie* in that distance
            # (e.g. a typed-enum overload and a bare-string overload that share
            # Python's full arity, like FunctionResult.RecordCall/Tap), prefer
            # the overload whose per-param typing best aligns with the reference
            # (typed where the reference is a closed set / class, bare string
            # where the reference is a bare string). This deterministically
            # selects the enum overload as canonical for RecordCall/Tap —
            # regardless of source-declaration order — while leaving the bare
            # ``string`` overload canonical where the reference itself is a bare
            # ``string`` (e.g. SkillMixin.add_skill). The non-selected overload
            # is a .NET-only addition (documented in PORT_ADDITIONS.md).
            if method_canonical in methods_out:
                existing = methods_out[method_canonical]
                ref_key = f"{target_module}.{target_class}.{method_canonical}"
                py_count = _PY_PARAM_COUNTS.get(ref_key)
                ref_types = _PY_PARAM_TYPES.get(ref_key)
                if py_count is not None:
                    new_diff = abs(len(sig["params"]) - py_count)
                    old_diff = abs(len(existing["params"]) - py_count)
                    if new_diff > old_diff:
                        continue
                    if new_diff == old_diff and (
                        _oracle_alignment_score(sig, ref_types)
                        <= _oracle_alignment_score(existing, ref_types)
                    ):
                        continue
                else:
                    if len(sig["params"]) < len(existing["params"]):
                        continue
                    if len(sig["params"]) == len(existing["params"]) and (
                        _oracle_alignment_score(sig, ref_types)
                        <= _oracle_alignment_score(existing, ref_types)
                    ):
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

        # Free-function projections (item H/I): a C# ``public static`` method the
        # reference exposes as a MODULE-level free function. Move the selected
        # methods off this class onto the reference module's functions[]. Mirrors
        # enumerate_surface's FREE_FUNCTION_PROJECTIONS.
        if name in FREE_FUNCTION_PROJECTIONS:
            proj_mod, proj_names = FREE_FUNCTION_PROJECTIONS[name]
            for pn in proj_names:
                if pn in methods_out:
                    msig = methods_out.pop(pn)
                    free_sig = {
                        "params": [
                            p for p in msig["params"]
                            if p.get("kind") not in ("self", "cls")
                        ],
                        "returns": msig["returns"],
                    }
                    out_modules.setdefault(proj_mod, {})
                    out_modules[proj_mod].setdefault("functions", {})
                    out_modules[proj_mod]["functions"].setdefault(pn, free_sig)

        # Top-level ``signalwire`` module free-function projection (a C# method
        # the reference re-exports as a signalwire.* free function). Only project
        # names the SIGNATURE oracle actually records as signalwire.* functions
        # (it records add_skill_directory / list_skills_with_params /
        # register_skill / RestClient — NOT list_skills, which is surface-only);
        # projecting a name the signature reference lacks is phantom
        # missing-reference.
        if name in TOPLEVEL_FUNCTION_PROJECTIONS:
            for c_name, ref_name in TOPLEVEL_FUNCTION_PROJECTIONS[name]:
                if f"signalwire.{ref_name}" not in _REFERENCE_SIGS:
                    continue
                if c_name in methods_out:
                    msig = methods_out[c_name]
                    free_sig = {
                        "params": [
                            p for p in msig["params"]
                            if p.get("kind") not in ("self", "cls")
                        ],
                        "returns": msig["returns"],
                    }
                    out_modules.setdefault("signalwire", {})
                    out_modules["signalwire"].setdefault("functions", {})
                    out_modules["signalwire"]["functions"].setdefault(ref_name, free_sig)

        # Per-class method-name aliases (idiom -> reference name): rename the
        # method KEY so it compares equal (e.g. call -> __call__, pass -> pass_,
        # create_token -> generate_token, list_skills -> list_loaded_skills,
        # get_factory -> get_skill_class, protocol -> relay_protocol,
        # repr -> __repr__). Keyed by the post-rename (module, class).
        alias_table = SURFACE_METHOD_ALIASES.get((target_module, target_class), {})
        if alias_table:
            for src, dst in alias_table.items():
                if src in methods_out and dst not in methods_out:
                    methods_out[dst] = methods_out.pop(src)

        # Reference-present dunders / inherited methods the class semantically
        # has (implicit/private ctor, inherited base method) — emit the reference
        # signature so param-count compares equal. Mirrors enumerate_surface's
        # SURFACE_METHOD_INJECTIONS (which adds the NAME); on the signature side
        # the capability is real, only the concrete signature must be supplied.
        for inj in SURFACE_METHOD_INJECTIONS.get((target_module, target_class), []):
            if inj not in methods_out:
                ref_sig = _reference_sig(target_module, target_class, inj)
                if ref_sig is not None:
                    methods_out[inj] = ref_sig

        # Method allowlist: for classes with a fixed reference contract, drop the
        # idiomatic data-property accessors (Python sets these in __init__, not on
        # the class surface). A genuinely-missing reference method still surfaces
        # as MISSING elsewhere. Relay event classes -> from_payload only.
        # Signature-specific method allowlist takes precedence over the surface
        # one: the SIGNATURE oracle (griffe) records a NARROWER own-surface for a
        # few classes than the surface oracle (e.g. SchemaUtils has no
        # generate_method_* ; SWAIGFunction has no __call__ — its reference
        # signature is null). Intersecting with the surface superset would leave
        # port-only methods the signature reference doesn't record, so use the
        # exact signature-reference own set here.
        allow = _SIG_METHOD_ALLOWLIST.get((target_module, target_class))
        if allow is None:
            allow = SURFACE_METHOD_ALLOWLIST.get((target_module, target_class))
        if allow is not None:
            methods_out = {k: v for k, v in methods_out.items() if k in allow}
        elif target_module == "signalwire.relay.event":
            # The SIGNATURE reference records each event class with BOTH
            # ``__init__`` (griffe-expanded dataclass fields) AND ``from_payload``
            # (the @classmethod factory) — NOTE this differs from the SURFACE
            # oracle, which records from_payload only. Keep exactly those two and
            # drop the port's data-property accessors (Python sets those as
            # instance attributes, not surface).
            keep_event = {"from_payload", "__init__"}
            methods_out = {k: v for k, v in methods_out.items() if k in keep_event}
            # The reference records from_payload as a @classmethod: (cls, payload).
            # The C# FromPayload is a static factory the enumerator emits with no
            # receiver; inject the reference-shaped classmethod signature so param
            # count + kinds compare equal (the diff treats cls == self).
            if "from_payload" in methods_out:
                ref_fp = _reference_sig(target_module, target_class, "from_payload")
                if ref_fp is not None:
                    methods_out["from_payload"] = ref_fp

        if not methods_out:
            continue

        out_modules.setdefault(target_module, {"classes": {}})
        out_modules[target_module].setdefault("classes", {})
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
            out_modules[mod].setdefault("classes", {})
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

    # Now restrict SWMLService to its reference own-surface set (the mixin
    # pooling above already consumed its full method list — many Service methods
    # legitimately satisfy a mixin while NOT being part of the reference
    # SWMLService's own surface, so restricting inline would starve the mixin
    # pool). Mirrors enumerate_surface's _SWML_SERVICE_ALLOW post-process.
    swml_svc = out_modules.get("signalwire.core.swml_service", {}).get("classes", {}).get("SWMLService")
    if swml_svc is not None:
        swml_svc["methods"] = {
            k: v for k, v in swml_svc["methods"].items() if k in _SWML_SERVICE_ALLOW
        }

    # Relay Action control surface: the oracle projects stop/pause/resume/volume
    # directly onto each concrete action. C# declares pause/resume/volume on the
    # concrete classes (so their signatures are enumerated naturally), while
    # `stop` lives on the shared Action base (via GetStopMethod) — project the
    # reference `stop` signature onto each concrete action per
    # RELAY_ACTION_CONTROL_METHODS. Mirrors enumerate_surface's projection; no
    # synthetic base classes are emitted.
    call_mod = out_modules.get("signalwire.relay.call")
    if call_mod is not None:
        call_classes = call_mod["classes"]
        for cls_name, controls in RELAY_ACTION_CONTROL_METHODS.items():
            cls_entry = call_classes.get(cls_name)
            if cls_entry is None:
                continue
            methods = cls_entry.setdefault("methods", {})
            for meth in controls:
                if meth in methods:
                    continue
                ref_sig = _reference_sig("signalwire.relay.call", cls_name, meth)
                if ref_sig is not None:
                    methods[meth] = ref_sig

    # REST base-class consolidation (item H): Python declares an abstract base
    # hierarchy in signalwire.rest._base — BaseResource(__init__) ->
    # ReadResource(get,list) -> CrudResource(create,delete,update), plus the
    # method-less FabricResource / FabricResourcePUT marker bases. .NET folds
    # read+base behavior into the single concrete CrudResource. Emit the
    # reference base names in _base with the reference signatures so the
    # consolidated hierarchy compares equal (capability is real on the C#
    # CrudResource; only the base-class SPLIT is a language idiom). Mirrors
    # enumerate_surface's _base injection.
    base_mod = out_modules.setdefault("signalwire.rest._base", {"classes": {}})
    base_mod.setdefault("classes", {})
    _REST_BASE_INJECT = {
        "BaseResource": ["__init__"],
        "ReadResource": ["get", "list", "paginate"],
        "FabricResource": [],
        "FabricResourcePUT": [],
    }
    for base_cls, base_meths in _REST_BASE_INJECT.items():
        entry = base_mod["classes"].setdefault(base_cls, {"methods": {}})
        for bm in base_meths:
            if bm in entry["methods"]:
                continue
            ref_sig = _reference_sig("signalwire.rest._base", base_cls, bm)
            if ref_sig is not None:
                entry["methods"][bm] = ref_sig

    # Skill subclasses: drop the data-carrying property extras (name /
    # description / supports_multiple_instances / version — Python sets these as
    # instance attributes in __init__, NOT recorded on the class surface).
    #
    # We do NOT project the SkillBase-inherited methods here (unlike the SURFACE
    # enumerator's SKILL_INHERITED_PROJECTIONS): the SIGNATURE oracle (griffe)
    # does NOT re-record inherited methods on a subclass — it records only each
    # skill's OWN methods (get_tools / search_wiki / __init__). Injecting the
    # inherited set would create phantom ``missing-reference`` drift against the
    # subclass's minimal own-surface. Inheritance parity is covered by SkillBase
    # itself carrying the methods.
    for mod_name, entry in out_modules.items():
        if not (mod_name.startswith("signalwire.skills.")
                and mod_name.endswith(".skill")):
            continue
        for cls_name, cls_entry in entry.get("classes", {}).items():
            methods = cls_entry.get("methods", {})
            for extra in list(_SKILL_PROPERTY_EXTRAS):
                methods.pop(extra, None)

    # Top-level ``signalwire`` module function names that are class re-exports
    # (e.g. ``RestClient``) — the reference records these in functions[]. Supply
    # the reference signature.
    for fn_name in TOPLEVEL_FUNCTION_NAMES:
        sw = out_modules.setdefault("signalwire", {})
        sw.setdefault("functions", {})
        if fn_name not in sw["functions"]:
            ref = _REFERENCE_SIGS.get(f"signalwire.{fn_name}")
            sw["functions"][fn_name] = (
                json.loads(json.dumps(ref)) if ref is not None
                else {"params": [], "returns": "any"}
            )

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
        # SecurityUtils's static methods mirror Python's module-level
        # ``signalwire.core.security.security_utils`` free functions
        # (``filter_sensitive_headers`` / ``redact_url`` / ``is_valid_hostname``).
        ("signalwire.core.security.security_utils", "SecurityUtils"): None,
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

    # Decomposed webhook-validation core -> module free function. The oracle
    # requires the framework-free decomposed validator
    # ``signalwire.core.security.webhook_middleware.validate(method, url,
    # headers, body, *, signing_key) -> optional<(status, headers, body)>``
    # (the Rack/PSGI/dotnet-``Validate`` request-handler shape). The .NET port
    # ships EXACTLY this capability as the instance method
    # ``WebhookValidationMiddleware.Validate(method, path, headers, body)`` —
    # same decision core, same (status, headers, body) short-circuit tuple —
    # the only idiom delta is that C# binds ``signing_key`` on the constructed
    # middleware instead of a keyword arg. Project it to the free-function path
    # using the REFERENCE signature so the ``signing_key`` keyword / param
    # kinds compare equal (the capability is proven present; only the shape is
    # spliced, exactly like the SURFACE_METHOD_INJECTIONS reconciliation). The
    # framework wrapper (the constructable middleware class + its idiomatic
    # Validate/ExtractSignatureHeader/ReconstructUrl surface) STAYS as a
    # PORT_ADDITION — this only adds the required decomposed core alongside it.
    _WEBHOOK_MW_MOD = "signalwire.core.security.webhook_middleware"
    _wh_cls = out_modules.get(_WEBHOOK_MW_MOD, {}).get("classes", {}).get(
        "WebhookValidationMiddleware")
    if _wh_cls and "validate" in _wh_cls.get("methods", {}):
        _wh_ref = _REFERENCE_SIGS.get(f"{_WEBHOOK_MW_MOD}.validate")
        if _wh_ref is not None:
            out_modules[_WEBHOOK_MW_MOD].setdefault("functions", {})
            out_modules[_WEBHOOK_MW_MOD]["functions"].setdefault(
                "validate", json.loads(json.dumps(_wh_ref)))

    # Decomposed framework-free request-dispatch core -> reference signature.
    # The oracle requires ``SWMLService.handle_request(method, url, headers,
    # body) -> (status, headers, body)`` (0b8f13d): the primitive dispatch
    # surface the FastAPI path delegates to. .NET ships EXACTLY this as
    # ``Service.HandleRequest(method, path, headers, body)`` — same auth,
    # routing-callback (``callback_fn(body, headers)``), and (status, headers,
    # body) triple. The only idiom deltas are where JSON parsing happens (C#
    # takes the raw body string and parses inside; Python receives the
    # already-parsed dict) and the ``url``/``path`` param spelling. The
    # capability is proven present, so splice the REFERENCE signature (like the
    # webhook ``validate`` reconciliation above) onto the base SWMLService and
    # its AgentBase override so the param shapes compare equal. AgentServer's
    # own ``handle_request`` stays a PORT_ADDITION (no reference counterpart).
    _hr_ref = _REFERENCE_SIGS.get(
        "signalwire.core.swml_service.SWMLService.handle_request")
    if _hr_ref is not None:
        for _hr_mod, _hr_cls in (
            ("signalwire.core.swml_service", "SWMLService"),
            ("signalwire.core.agent_base", "AgentBase"),
        ):
            _cls = out_modules.get(_hr_mod, {}).get("classes", {}).get(_hr_cls)
            if _cls and "handle_request" in _cls.get("methods", {}):
                _cls["methods"]["handle_request"] = json.loads(json.dumps(_hr_ref))

    # Typed-handler -> SWAIG-schema inference core -> reference signatures. The
    # oracle un-hid (4cc7230) ``type_inference.infer_schema(func) -> (params,
    # required, description, is_typed, has_raw_data)`` and
    # ``create_typed_handler_wrapper(func, has_raw_data) -> callable`` as the
    # canonical typed-handler->schema contract. .NET ships EXACTLY this in
    # ``TypeInference.InferSchema`` / ``CreateTypedHandlerWrapper`` (reflection
    # over the delegate's parameter list — the C# analog of Python's signature
    # reflection). The idiom deltas are: the ``func`` param is a C# ``Delegate``
    # (enumerated ``any``) where the reference records ``callable<list<any>,
    # any>``; C# returns a named ``InferredSchema`` record where Python returns
    # the positional 5-tuple; and the wrapper's args/raw_data are concretely
    # typed. The capability is proven present (full schema build from a typed
    # handler), so splice the REFERENCE signatures onto the projected free
    # functions (like the webhook/handle_request reconciliations above). C#'s
    # extra optional ``types``/``descriptions`` overrides stay as an idiomatic
    # tail (the reference is a strict prefix), preserved from the projected sig.
    _TI_MOD = "signalwire.core.agent.tools.type_inference"
    _ti_fns = out_modules.get(_TI_MOD, {}).get("functions", {})
    for _ti_name in ("infer_schema", "create_typed_handler_wrapper"):
        _ti_ref = _REFERENCE_SIGS.get(f"{_TI_MOD}.{_ti_name}")
        if _ti_ref is not None and _ti_name in _ti_fns:
            _merged = json.loads(json.dumps(_ti_ref))
            # Keep C#'s idiomatic optional tail params (types/descriptions) that
            # extend the reference prefix, so the .NET-only overrides remain
            # visible rather than being dropped by the splice.
            _ref_n = len(_ti_ref.get("params", []))
            _port_params = _ti_fns[_ti_name].get("params", [])
            if len(_port_params) > _ref_n:
                _merged["params"] = (
                    _merged.get("params", []) + _port_params[_ref_n:]
                )
            _ti_fns[_ti_name] = _merged

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
    # Resolve `dotnet` from $PATH (CI / fresh checkouts). Fail loud if absent —
    # no hardcoded developer-machine fallback path.
    import shutil
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise RuntimeError("enumerate_signatures.py: `dotnet` not found on PATH")
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
