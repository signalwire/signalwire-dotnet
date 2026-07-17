#!/usr/bin/env python3
"""Generate the SignalWire REST namespace resource layer for signalwire-dotnet.

This is the C#/.NET realization of porting-sdk/REST_GENERATOR_RULES.md — the
language-neutral contract of the REST resource generator (bases,
x-sdk-resource markup, path composition, command-dispatch, set_methods,
cross-spec client-tree placement, fail-loud invariants). It mirrors the PHP
generator (signalwire-php/scripts/generate_rest.py) structure emitter-for-
emitter; only the emitted language differs.

Inputs (resolved from $PORTING_SDK or the adjacent ../porting-sdk):
    rest-apis/<ns>/openapi.yaml       (+ x-sdk-* markup)
    rest-apis/x-sdk-bases.yaml        (shared base method-sets)
    rest-apis/fabric/x-sdk-bases.yaml (FabricResource)

Outputs: C# files under src/SignalWire/REST/Namespaces/Generated/ — one file
per generated resource class, one client-tree container file per namespace
group, and ResourceTree.cs the hand RestClient composes.

The hand BASES stay hand-written (src/SignalWire/REST/{CrudResource,
CrudWithAddresses,HttpClient,RestClient}.cs). .NET has no ReadResource /
BaseResource CLASS (Python/PHP do); a ReadResource resource emits its own
list/get inline, and a BaseResource resource is a standalone class whose whole
surface is its declared methods — matching how the .NET hand code realizes
these today (VideoRoomSessions et al. are standalone classes with their own
Path()/List/Get). The oracle records only the CLASS NAME + declared-method set
per resource, so the base-composition idiom is a free .NET choice (RULES §11).

Idiom (PORT_PHILOSOPHY_DOTNET.md, SESSION L13): the generated operation /
command / set methods take a NAMED SET of typed C# parameters (one per spec
field, required-first then ``T? x = null``) plus a trailing forward-compat
``Dictionary<string, object?>? extras = null`` door — the .NET named idiom
(options-object / named-args, PORT_PHILOSOPHY "Construction" row). .NET has
DISTINCT numeric types (int/long/double) so there is NO numeric-monotype flag.
Classes are named by x-sdk-resource.name VERBATIM (already PascalCase — the
Python oracle canonical names), so the .NET adapter can project each generated
class onto the same signalwire.rest.namespaces.<ns>_resources_generated.<Name>
module the python oracle produces.

Usage:
    python3 scripts/generate_rest.py                 # write into the repo tree
    python3 scripts/generate_rest.py --check         # GEN-FRESH: fail if stale
    python3 scripts/generate_rest.py --out DIR       # scratch: emit flat into DIR
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path

try:
    import yaml
except ImportError:  # pragma: no cover
    sys.stderr.write("generate_rest.py requires PyYAML (pip install pyyaml)\n")
    raise


# ---------------------------------------------------------------------------
# REST namespace DISCOVERY (mirrors the Python reference's spec-dir scan in
# porting-sdk/scripts/generate_python_rest_types.py — no hardcoded membership
# list, matching the ruby/java/php generators).
#
# The two namespace SETS are DERIVED by scanning rest-apis/<ns>/openapi.yaml:
#   * RESOURCE namespaces (the former SPEC_DIRS): a spec dir with at least one
#     non-excluded, named ``x-sdk-resource`` block — the specs that emit
#     generated resource classes / containers. (registry has no own dir — its
#     resources live inside relay-rest via ``namespace: registry``.) ``projects``
#     — the /api/projects project-management API — carries an ``x-sdk-resource``
#     block and so IS a resource namespace, emitting the flat ``Projects``
#     CrudResource; distinct from the singular ``project`` token namespace.
#     ``swml-webhooks`` is a types-only webhook-payload spec with no resources.
#   * TYPE namespaces (the former TYPE_NS): every RESOURCE namespace PLUS the
#     types-only specs — a spec with components.schemas but NO ``servers`` block
#     (the webhook-payload specs, e.g. ``swml-webhooks``). BROADER than the
#     resource set: swml-webhooks has no x-sdk-resource yet still emits DTOs.
#
# The membership of both sets is fully discovered; the only facts the scan cannot
# derive from the spec dir + markup are (a) the curated cross-namespace ORDER
# (affects the ResourceTree accessor order + the `logs` container's cross-spec
# member order — not alphabetical, not derivable from any spec field) and (b) the
# PascalCase Types sub-namespace for ``pubsub`` (→ ``PubSub``, not the mechanical
# ``Pubsub``). Both are kept as small explicit tables (like ATTR_OVERRIDE); the
# discovery fails LOUD if a scanned namespace is missing from the order table, so
# a new resource spec dir is picked up automatically and only needs an order
# placement.
_NS_ORDER = (
    "relay-rest", "fabric", "calling", "video", "datasphere",
    "logs", "message", "messages", "voice", "fax", "project", "projects", "chat",
    "pubsub", "swml-webhooks",
)

# PascalCase Types sub-namespace overrides where the mechanical PascalCase of the
# spec dir (snake_to_pascal) is not the canonical reference name (like
# ATTR_OVERRIDE for accessors). Only ``pubsub`` diverges (PubSub, not Pubsub).
_TYPE_SUB_OVERRIDE = {"pubsub": "PubSub"}


def _spec_docs(psdk: Path) -> dict[str, dict]:
    """Scan rest-apis/ once: {spec_dir: parsed openapi doc} for every dir with an
    openapi.yaml. Cached on the function for the process lifetime."""
    cache = getattr(_spec_docs, "_cache", None)
    if cache is None:
        cache = {}
        for d in sorted((psdk / "rest-apis").iterdir()):
            y = d / "openapi.yaml"
            if y.is_file():
                cache[d.name] = yaml.safe_load(y.read_text()) or {}
        _spec_docs._cache = cache  # type: ignore[attr-defined]
    return cache


def _has_resource(doc: dict) -> bool:
    for _path, item in (doc.get("paths") or {}).items():
        if not isinstance(item, dict):
            continue
        r = item.get("x-sdk-resource")
        if r and not r.get("exclude") and r.get("name"):
            return True
    return False


def _order_key(ns: str) -> int:
    if ns not in _NS_ORDER:
        raise SystemExit(
            f"generate_rest.py: spec dir {ns!r} has no entry in _NS_ORDER — add it "
            f"to the curated cross-namespace order (the scan cannot derive order)"
        )
    return _NS_ORDER.index(ns)


def discover_spec_dirs(psdk: Path) -> list[str]:
    """RESOURCE namespaces (former SPEC_DIRS): spec dirs carrying x-sdk-resource
    markup, in the curated cross-namespace order."""
    dirs = [ns for ns, doc in _spec_docs(psdk).items() if _has_resource(doc)]
    return sorted(dirs, key=_order_key)


def discover_type_ns(psdk: Path) -> list[tuple[str, str, str]]:
    """TYPE namespaces (former TYPE_NS): RESOURCE namespaces PLUS types-only specs
    (components.schemas but no servers block). Returns (spec_dir, PascalSub,
    ns_key) in the curated order — PascalSub via snake_to_pascal (override table
    for pubsub), ns_key = spec dir with '-' -> '_'."""
    out: list[tuple[str, str, str]] = []
    for ns, doc in _spec_docs(psdk).items():
        has_schemas = bool(((doc.get("components") or {}).get("schemas")) or {})
        if not has_schemas:
            continue
        is_types_only = not doc.get("servers")
        if _has_resource(doc) or is_types_only:
            sub = _TYPE_SUB_OVERRIDE.get(ns) or snake_to_pascal(ns)
            ns_key = ns.replace("-", "_")
            out.append((ns, sub, ns_key))
    return sorted(out, key=lambda t: _order_key(t[0]))

# C# reserved keywords (contextual keywords like `value`/`async` are legal as
# identifiers and are NOT escaped — only true reserved words). A field whose
# param identifier collides is @-escaped (a verbatim identifier) — the .NET
# analog of PHP's `_` suffix / Python's `from` -> `from_`. The wire key is
# unchanged; only the C# identifier gets `@`.
CSHARP_KEYWORDS = {
    "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
    "char", "checked", "class", "const", "continue", "decimal", "default",
    "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
    "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
    "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
    "namespace", "new", "null", "object", "operator", "out", "override",
    "params", "private", "protected", "public", "readonly", "ref", "return",
    "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
    "struct", "switch", "this", "throw", "true", "try", "typeof", "uint",
    "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
    "volatile", "while",
}


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
    raise SystemExit("generate_rest.py: porting-sdk not found (set $PORTING_SDK or clone adjacent)")


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


# ---------------------------------------------------------------------------
# SDK-surface policy overlay (the single source; NOT wire truth).
# ---------------------------------------------------------------------------
# rest-apis/x-sdk-overlay.yaml is the ONE authoritative place that says which spec
# fields the SDKs hide (dropped from the surface) or deprecate (emitted-but-flagged).
# It is a policy overlay, not markup in the (often vendored) specs, so the same field
# is governed once and applied wherever it surfaces (schema.json AIParams + the
# calling/fabric REST projections + the swml-verbs config type). Every property-
# emission site consults it by (field name, containing SPEC SCHEMA NAME) — the
# $defs / components.schemas key AS IT APPEARS IN THE SPEC, NOT the C# type name we
# later emit — so one `scope` value works cross-port (mirrors the Python reference
# generate_python_rest_types.py).
_overlay_cache: dict[str, set[tuple[str, str | None]]] | None = None


def _load_overlay() -> dict[str, set[tuple[str, str | None]]]:
    global _overlay_cache
    if _overlay_cache is None:
        path = resolve_porting_sdk() / "rest-apis" / "x-sdk-overlay.yaml"

        def rules(key: str, data: dict) -> set[tuple[str, str | None]]:
            out: set[tuple[str, str | None]] = set()
            for entry in data.get(key) or []:
                if isinstance(entry, dict) and entry.get("field"):
                    out.add((entry["field"], entry.get("scope")))
            return out

        data = {}
        if path.is_file():
            data = yaml.safe_load(path.read_text()) or {}
        _overlay_cache = {"hidden": rules("hidden", data), "deprecated": rules("deprecated", data)}
    return _overlay_cache


def _overlay_match(rules: set[tuple[str, str | None]], field: str, schema_name: str | None) -> bool:
    # A rule matches when its field equals `field` AND (it is unscoped OR its scope
    # equals the containing SPEC schema name). An unscoped rule matches everywhere;
    # a scoped rule only inside the schema whose spec name equals its scope.
    for rf, scope in rules:
        if rf == field and (scope is None or scope == schema_name):
            return True
    return False


def _overlay_hidden(field: str, schema_name: str | None = None) -> bool:
    return _overlay_match(_load_overlay()["hidden"], field, schema_name)


def _overlay_deprecated(field: str, schema_name: str | None = None) -> bool:
    return _overlay_match(_load_overlay()["deprecated"], field, schema_name)


# ---------------------------------------------------------------------------
# Base loading (x-sdk-bases; §2).
# ---------------------------------------------------------------------------

def load_bases(psdk: Path) -> dict[str, list[str]]:
    raw = yaml.safe_load((psdk / "rest-apis" / "x-sdk-bases.yaml").read_text())
    bases = dict(raw.get("x-sdk-bases") or {})
    fab = psdk / "rest-apis" / "fabric" / "x-sdk-bases.yaml"
    if fab.is_file():
        bases.update((yaml.safe_load(fab.read_text()).get("x-sdk-bases") or {}))

    def resolve(name: str, seen: set[str]) -> list[str]:
        if name in seen:
            raise SystemExit(f"x-sdk-bases: cyclic extends at {name}")
        if name not in bases:
            raise SystemExit(f"x-sdk-bases: undefined base {name!r}")
        seen = seen | {name}
        methods: list[str] = []
        ext = bases[name].get("extends")
        if ext:
            methods.extend(resolve(ext, seen))
        methods.extend(list((bases[name].get("methods") or {}).keys()))
        return methods

    return {name: resolve(name, set()) for name in bases}


# ---------------------------------------------------------------------------
# Spec model.
# ---------------------------------------------------------------------------

class Spec:
    def __init__(self, name: str, doc: dict):
        self.name = name
        self.doc = doc
        self.server_path = _url_path(doc["servers"][0]["url"])
        if self.server_path != "/" and self.server_path.endswith("/"):
            raise SystemExit(f"{name}: servers[0].url path {self.server_path!r} has a trailing slash")
        self.namespace_attr = (doc.get("x-sdk-namespace") or {}).get("attr") or ""
        self.ops: dict[str, tuple[str, str, bool]] = {}
        self.op_body: dict[str, dict] = {}  # operationId -> requestBody JSON schema (or {})
        for path, item in (doc.get("paths") or {}).items():
            for verb in ("get", "post", "put", "patch", "delete"):
                o = item.get(verb)
                if o and o.get("operationId"):
                    self.ops[o["operationId"]] = (verb, path, bool(o.get("requestBody")))
                    body = o.get("requestBody") or {}
                    content = body.get("content") or {}
                    media = content.get("application/json") or (next(iter(content.values())) if content else {})
                    self.op_body[o["operationId"]] = (media or {}).get("schema") or {}
        self.schemas = ((doc.get("components") or {}).get("schemas")) or {}

    def resources(self) -> list[tuple[str, dict]]:
        out = []
        for path, item in (self.doc.get("paths") or {}).items():
            r = item.get("x-sdk-resource")
            if r and not r.get("exclude") and r.get("name"):
                out.append((path, r))
        return out


def _url_path(url: str) -> str:
    if "://" in url:
        url = url.split("://", 1)[1]
    i = url.find("/")
    return url[i:] if i >= 0 else "/"


def load_spec(psdk: Path, ns: str) -> Spec:
    return Spec(ns, yaml.safe_load((psdk / "rest-apis" / ns / "openapi.yaml").read_text()))


# ---------------------------------------------------------------------------
# Path composition (§4).
# ---------------------------------------------------------------------------

def join_path(a: str, b: str) -> str:
    if not b:
        return a
    return a.rstrip("/") + "/" + b.lstrip("/")


def collection_segment(anchor: str, markup: dict) -> str:
    if "collection" in markup:
        return markup["collection"]
    p = anchor
    i = p.find("/{")
    if i >= 0:
        p = p[:i]
    return p


def base_path(spec: Spec, anchor: str, markup: dict) -> str:
    return join_path(spec.server_path, collection_segment(anchor, markup))


def relative_tail(spec: Spec, anchor: str, markup: dict, op_path: str):
    coll = collection_segment(anchor, markup)
    full = join_path(spec.server_path, coll)
    absp = join_path(spec.server_path, op_path)
    if coll and absp.startswith(full + "/"):
        return ([s for s in absp[len(full) + 1:].split("/") if s], False)
    if coll and absp == full:
        return ([], False)
    return ([s for s in absp.lstrip("/").split("/") if s], True)


# ---------------------------------------------------------------------------
# Naming.
# ---------------------------------------------------------------------------

def snake_to_pascal(snake: str) -> str:
    parts = [p for p in snake.replace("-", "_").replace(".", "_").split("_") if p]
    return "".join(w[:1].upper() + w[1:] for w in parts) if parts else snake


def snake_to_camel(snake: str) -> str:
    p = snake_to_pascal(snake)
    return p[:1].lower() + p[1:] if p else snake


def escape_param(field: str) -> str:
    """A body-field wire key -> a legal camelCase C# parameter identifier. A
    collision with a C# reserved word is @-escaped (verbatim identifier), the
    .NET analog of PHP's `_` suffix — the WIRE key is unchanged, only the
    identifier is escaped."""
    ident = snake_to_camel(field)
    return "@" + ident if ident in CSHARP_KEYWORDS else ident


PARAM_ARG_NAME = {
    "id": "id", "queue_id": "queueId", "NumberGroupId": "groupId",
    "documentId": "documentId", "chunkId": "chunkId", "mfa_request_id": "requestId",
    "e164_number": "e164", "fabric_subscriber_id": "subscriberId",
    "ai_agent_id": "id", "cxml_webhook_id": "id", "swml_webhook_id": "id",
    "token_id": "tokenId", "room_id": "roomId", "resource_id": "resourceId",
    "sip_endpoint_id": "sipEndpointId",
}


def arg_for(brace: str) -> str:
    name = PARAM_ARG_NAME.get(brace) or snake_to_camel(brace) or "id"
    return "@" + name if name in CSHARP_KEYWORDS else name


def cs_str(s: str) -> str:
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


# ---------------------------------------------------------------------------
# Base mapping (§2).
# ---------------------------------------------------------------------------

BASE_PROVIDES = {
    "CrudResource": {"list", "create", "get", "update", "delete"},
    "FabricResource": {"list", "create", "get", "update", "delete", "list_addresses"},
    "ReadResource": {"list", "get"},
    "BaseResource": set(),
}


# ---------------------------------------------------------------------------
# Command-dispatch (§6).
# ---------------------------------------------------------------------------

def command_method_name(cmd: str) -> str:
    s = cmd
    if s.startswith("calling."):
        s = s.split(".", 1)[1]
    s = s.replace(".", "_")
    return snake_to_pascal(s)


def discriminator_mapping(spec: Spec, schema_name: str) -> list[str]:
    sch = spec.schemas.get(schema_name)
    if sch is None:
        raise SystemExit(f"command-dispatch request {schema_name!r} not in components.schemas")
    mapping = (sch.get("discriminator") or {}).get("mapping")
    if not mapping:
        raise SystemExit(f"command-dispatch request {schema_name!r} has no discriminator.mapping")
    return list(mapping.keys())


# ---------------------------------------------------------------------------
# Typed inputs (§5) — schema → C# native type + canonical audit type.
# ---------------------------------------------------------------------------
#
# The generated operation/command/set methods take ONE named C# parameter per
# spec field (the .NET realization of Go's options struct / TS's options
# object / PHP's named params): required fields → typed params, optional fields
# → ``T? x = null``, plus a trailing forward-compat
# ``Dictionary<string, object?>? extras = null`` door.
#
# The .NET reflection the signature enumerator reads (later item) cannot express
# keyword-only intent or the open dict semantics of ``extras``, so the generator
# ALSO emits a machine-readable sidecar (Generated/rest_signatures.json)
# recording each method's canonical param list (name, kind, type, required); the
# enumerator UNFOLDS the reflected C# params to that recorded shape (mirrors
# Go/PHP). Both the C# signature and the sidecar derive from the SAME computed
# param list here, so they never diverge (GEN-FRESH covers the sidecar).
#
# Canonical-type rule (proven drift-neutral against the oracle, mirrors PHP):
#   * required JSON scalar  string/integer/number/boolean → string/int/float/bool
#   * required array                                       → list<any>
#   * required object / $ref / oneOf / anyOf               → dict<string,any>
#   * optional (any JSON type)                             → optional<any>


def resolve_schema(spec: Spec, schema: dict | None, seen=None) -> dict:
    if not schema:
        return {}
    if seen is None:
        seen = set()
    ref = schema.get("$ref")
    if ref:
        leaf = ref.rsplit("/", 1)[-1]
        if leaf in seen:
            return {}
        seen.add(leaf)
        return resolve_schema(spec, spec.schemas.get(leaf), seen)
    allof = schema.get("allOf")
    if allof and len(allof) == 1 and not schema.get("properties") and not schema.get("type"):
        return resolve_schema(spec, allof[0], seen)
    return schema


def _is_named_ref(schema: dict) -> bool:
    if not schema:
        return False
    if schema.get("$ref"):
        return True
    allof = schema.get("allOf")
    if allof and len(allof) == 1 and not schema.get("properties") and not schema.get("type"):
        return _is_named_ref(allof[0])
    return False


def _json_type(schema: dict) -> str | None:
    t = schema.get("type")
    if isinstance(t, list):
        non_null = [x for x in t if x != "null"]
        return non_null[0] if non_null else None
    return t


_SCALAR_CS = {"string": "string", "integer": "int", "number": "double", "boolean": "bool"}
_SCALAR_CANON = {"string": "string", "integer": "int", "number": "float", "boolean": "bool"}


def cs_param_type(spec: Spec, schema: dict, required: bool) -> str:
    """The C# native type for a body field. Optionals are nullable ``T?``.
    A scalar → string/int/double/bool; array/object/union → the open
    ``Dictionary<string, object?>`` (arrays as ``List<object?>``)."""
    resolved = resolve_schema(spec, schema)
    jt = _json_type(resolved)
    if jt in _SCALAR_CS:
        base = _SCALAR_CS[jt]
    elif jt == "array":
        base = "List<object?>"
    else:
        # object / $ref-to-object / oneOf / anyOf / unknown → open dict.
        base = "Dictionary<string, object?>"
    # Reference types (string, List, Dictionary) are already nullable-annotated
    # with `?` for optionals; value types (int/double/bool) need `?` too.
    return base if required else base + "?"


def canonical_type(spec: Spec, schema: dict, required: bool) -> str:
    """The canonical audit type the sidecar records for a body field. Optionals
    → ``optional<any>``; required NAMED-$ref → ``dict<string,any>`` (folds onto
    the oracle's ``gen:<Name>``); required inline scalar/array/object →
    concrete open form. Mirrors PHP exactly (drift-neutral)."""
    if not required:
        return "optional<any>"
    if _is_named_ref(schema):
        return "dict<string,any>"
    resolved = resolve_schema(spec, schema)
    jt = _json_type(resolved)
    if jt in _SCALAR_CANON:
        return _SCALAR_CANON[jt]
    if jt == "array":
        return "list<any>"
    return "dict<string,any>"


def object_body_fields(spec: Spec, body_schema: dict) -> list[tuple[str, dict, bool]]:
    resolved = resolve_schema(spec, body_schema)
    props: dict[str, dict] = {}
    required: set[str] = set(resolved.get("required") or [])
    for name, psc in (resolved.get("properties") or {}).items():
        props.setdefault(name, psc)
    for br in resolved.get("allOf") or []:
        rb = resolve_schema(spec, br)
        required |= set(rb.get("required") or [])
        for name, psc in (rb.get("properties") or {}).items():
            props.setdefault(name, psc)
    return [(name, psc, name in required) for name, psc in props.items()]


def command_param_fields(spec: Spec, command_schema: dict) -> tuple[list[tuple[str, dict, bool]], bool]:
    """§6 union-flatten: return ([(wire_name, schema, required)], has_id)."""
    cs = resolve_schema(spec, command_schema)
    has_id = "id" in (cs.get("properties") or {})
    params_schema = (cs.get("properties") or {}).get("params")
    if params_schema is None:
        return [], has_id
    ps = resolve_schema(spec, params_schema)
    variants: list[dict] = []
    for comb in ("anyOf", "oneOf"):
        if comb in ps:
            variants = [resolve_schema(spec, v) for v in ps[comb]]
            break
    if not variants:
        variants = [ps]
    all_props: dict[str, dict] = {}
    req_sets: list[set[str]] = []
    for v in variants:
        req_sets.append(set(v.get("required") or []))
        for name, psc in (v.get("properties") or {}).items():
            all_props.setdefault(name, psc)
    req_all = set.intersection(*req_sets) if req_sets else set()
    return [(name, psc, name in req_all) for name, psc in all_props.items()], has_id


def is_object_body(spec: Spec, body_schema: dict) -> bool:
    if not body_schema:
        return False
    if "anyOf" in body_schema or "oneOf" in body_schema:
        return False
    resolved = resolve_schema(spec, body_schema)
    if "anyOf" in resolved or "oneOf" in resolved:
        return False
    if resolved.get("properties") or resolved.get("allOf"):
        return True
    return _json_type(resolved) == "object"


def ordered_fields(fields: list[tuple[str, dict, bool]]) -> list[tuple[str, dict, bool]]:
    req = [f for f in fields if f[2]]
    opt = [f for f in fields if not f[2]]
    return req + opt


# Sidecar accumulator: (ClassName, csMethodName) -> [param records].
# Each record: {"name", "kind", "type", "required", ["default"]}.
_SIDECAR: dict[tuple[str, str], list[dict]] = {}


def _register_sidecar(cls: str, cs_method: str, records: list[dict]) -> None:
    _SIDECAR[(cls, cs_method)] = records


def _dedupe_param(ident: str, used: set[str]) -> str:
    """Guarantee a unique C# parameter identifier (a body field named `extras`
    or `queryParams` would clash with the trailing door — rename by suffix)."""
    base = ident
    n = 2
    while ident in used:
        ident = base + str(n)
        n += 1
    used.add(ident)
    return ident


def body_params(spec: Spec, cls: str, cs_method: str,
                fields: list[tuple[str, dict, bool]],
                leading: list[dict], used: set[str]) -> tuple[list[str], list[str], list[str]]:
    """Build named C# params + body-assembly C# + xml-doc lines for a set of
    body fields. ``leading`` is the already-built sidecar records for positional
    id/call_id args. Returns (cs_params_for_fields, body_build_lines, doc_lines)."""
    cs_params: list[str] = []
    build: list[str] = []
    doc: list[str] = []
    records: list[dict] = list(leading)
    # Local dict identifier uses a leading underscore so it can NEVER collide
    # with a body-field param name: snake_to_camel never emits a leading
    # underscore, so a wire field literally named ``body`` (messages spec) maps
    # to the C# param ``body`` while the assembly dict stays ``_reqBody``.
    build.append("        var _reqBody = new Dictionary<string, object?>();")
    for wire_name, schema, required in ordered_fields(fields):
        ident = _dedupe_param(escape_param(wire_name), used)
        pt = cs_param_type(spec, schema, required)
        ct = canonical_type(spec, schema, required)
        raw = ident[1:] if ident.startswith("@") else ident
        doc.append(f"    /// <param name=\"{raw}\">Wire field <c>{wire_name}</c>.</param>")
        rec: dict = {"name": wire_name, "kind": "keyword", "type": ct, "required": required}
        if required:
            cs_params.append(f"{pt} {ident}")
            build.append(f"        _reqBody[{cs_str(wire_name)}] = {ident};")
        else:
            cs_params.append(f"{pt} {ident} = null")
            rec["default"] = None
            build.append(f"        if ({ident} is not null)")
            build.append("        {")
            build.append(f"            _reqBody[{cs_str(wire_name)}] = {ident};")
            build.append("        }")
        records.append(rec)
    # trailing forward-compat door — the oracle's keyword ``extras``.
    extras_id = _dedupe_param("extras", used)
    cs_params.append(f"Dictionary<string, object?>? {extras_id} = null")
    doc.append(f"    /// <param name=\"{extras_id}\">Forward-compat body fields merged onto the request.</param>")
    records.append({
        "name": "extras", "kind": "keyword",
        "type": "optional<dict<string,any>>", "required": False, "default": None,
    })
    build.append(f"        if ({extras_id} is not null)")
    build.append("        {")
    build.append(f"            foreach (var kv in {extras_id})")
    build.append("            {")
    build.append("                _reqBody[kv.Key] = kv.Value;")
    build.append("            }")
    build.append("        }")
    _register_sidecar(cls, cs_method, records)
    return cs_params, build, doc


# ---------------------------------------------------------------------------
# Emitters.
# ---------------------------------------------------------------------------

GEN_HEADER = """// <auto-generated>
// Code generated by scripts/generate_rest.py; DO NOT EDIT.
//
// AUTO-GENERATED from porting-sdk/rest-apis/ (x-sdk-* markup) — regenerate with:
//   python3 scripts/generate_rest.py
//
// {desc}
// </auto-generated>
#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SignalWire.REST.Namespaces.Generated;
"""


# ---------------------------------------------------------------------------
# Generated method-less TYPE surface (SESSION_CHANGESET item D / H).
#
# Shared by the REST wire-type emitter (emit_types, below) AND the standalone
# SWML-verbs / relay-protocol / SWAIG-payload generators (generate_swml_verbs.py
# / generate_relay_protocol.py / generate_swaig_payloads.py) so the four never
# diverge on the emit rule — the .NET analog of ruby's shared
# emit_methodless_class. Each generated type is a method-less C# data class: a
# public property per wire field carrying the snake wire key via
# ``[JsonPropertyName]``. NO methods, NO constructor — the reference records
# these as method-less type definitions (bare class name on the surface).
#
# The property C# TYPE mirrors PHP's typed-property realization: a ``$ref`` /
# nested-object / combinator field is a class-ref (the .NET signature enumerator
# translates it to ``class:...`` so a class-typed field MATCHES the reference's
# recorded accessor); a scalar/array/dict field is a primitive/collection (the
# signature diff excuses it as a port-side state accessor). Only OBJECT schemas
# with properties are surfaced; scalar/array/union/enum aliases are NOT (the
# reference's griffe enumerator drops module-level scalar TypeAliases / inline
# unions — verified emit-set == oracle-set per module, 0 missing / 0 extra).
#
# Enum classes (x-sdk-enum) become a method-less class carrying public const
# string members (value == wire string) — the .NET closed-set idiom, method-less
# so it records the bare class name like the reference.
# ---------------------------------------------------------------------------

TYPES_HEADER = """// <auto-generated>
// Code generated; DO NOT EDIT. Regenerate with the matching scripts/generate_*.py.
//
// {desc}
// </auto-generated>
#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace {ns};
"""


def type_name(raw: str) -> str:
    """Sanitise a components/schemas key to a valid C# type name, folding every
    non-identifier rune to ``_`` — matching the Go/TS/PHP/python/ruby ref_name so
    the LEAF the surface diff compares is the identical token across ports
    (``Types.StatusCodes.StatusCode400`` -> ``Types_StatusCodes_StatusCode400``).
    A leading digit is prefixed; the first letter is upper-cased so it is a legal
    public type name."""
    s = re.sub(r"[^A-Za-z0-9_]", "_", raw).lstrip("_")
    if not s:
        return "Schema"
    if s[0].isdigit():
        return "Schema_" + s
    if not s[0].isupper():
        s = s[0].upper() + s[1:]
    return s


def _type_schema_type(node: dict):
    t = node.get("type")
    if isinstance(t, list):
        return next((x for x in t if x != "null"), None)
    return t


def is_object_schema(node: dict) -> bool:
    """Mirror the reference is_object test: type:object (or no type but non-empty
    properties) AND not a oneOf/anyOf/allOf combinator AND properties non-empty."""
    if any(k in node for k in ("oneOf", "anyOf", "allOf")):
        return False
    props = node.get("properties")
    t = _type_schema_type(node)
    return (t == "object" or (t is None and props)) and isinstance(props, dict) and len(props) > 0


# JSON scalar -> C# nullable primitive for a method-less TYPE property. A field
# the enumerator must NOT record as a class-typed accessor (so the diff excuses it
# as a port-side state accessor). Distinct name from the resource emitter's
# ``_SCALAR_CS`` (line ~353, non-nullable bases) — DO NOT collide/shadow it.
_TYPE_SCALAR_CS = {
    "string": "string?",
    "integer": "long?",
    "number": "double?",
    "boolean": "bool?",
}


def _ref_leaf_cs(ref: str, ref_names: dict) -> str | None:
    """A ``$ref`` -> the fully-qualified C# sibling type name IFF the reference's
    generator surfaces that leaf as a class-typed accessor (i.e. the leaf is a
    key in ``ref_names`` mapping leaf -> fully-qualified C# name). Cross-spec /
    scalar-alias refs return None (open shape)."""
    leaf = type_name(ref.rsplit("/", 1)[-1])
    return ref_names.get(leaf)


def _wire_field_cs_type(psc: dict, ref_names: dict) -> str:
    """The C# property type for a wire field, mirroring the reference's ``py_type``
    field-typing rule (generate_python_rest_types.py) so the .NET signature
    enumerator records the SAME class-typed accessors the oracle does:

      * ``$ref`` to a surfaced object schema  -> the sibling generated class
        (class-typed accessor — MATCHES the reference).
      * ``array`` whose ``items`` is such a ``$ref``  -> ``List<Sibling>?``
        (``list<class:...>`` — MATCHES the reference).
      * everything else (scalar, inline object, combinator, array-of-scalar,
        cross-spec/scalar-alias ref)  -> a primitive / open Dictionary / List
        (the reference does NOT record these as class-typed accessors; the diff
        excuses the port property as a port-side state accessor).

    ``ref_names`` maps a surfaced-object LEAF name -> its fully-qualified C# type
    name (so a cross-namespace ref like PostPromptSwaigLogEntry.post_data ->
    SignalWire.Core.SwaigRequestGenerated.SwaigRequest resolves)."""
    if not isinstance(psc, dict):
        return "object?"
    ref = psc.get("$ref")
    if ref:
        sib = _ref_leaf_cs(ref, ref_names)
        return f"{sib}?" if sib else "Dictionary<string, object?>?"
    if any(k in psc for k in ("allOf", "oneOf", "anyOf")):
        return "Dictionary<string, object?>?"
    t = _type_schema_type(psc)
    if t in _TYPE_SCALAR_CS:
        return _TYPE_SCALAR_CS[t]
    if t == "array":
        items = psc.get("items")
        if isinstance(items, dict) and items.get("$ref"):
            sib = _ref_leaf_cs(items["$ref"], ref_names)
            if sib:
                return f"List<{sib}>?"
        return "List<object?>?"
    if t == "object":
        return "Dictionary<string, object?>?"
    return "object?"


def _cs_property_name(wire_key: str) -> str:
    """A legal PascalCase C# property identifier for a wire key, kept close to the
    wire key so the reference-recorded accessor name matches. The .NET signature
    enumerator records the property name VERBATIM for these generated type files
    (like PHP's ``$SWAIG``); to match the reference's recorded field name (which
    is the WIRE KEY verbatim — ``SWAIG`` stays ``SWAIG``, ``allOf`` stays
    ``allOf``) we preserve the wire key exactly where it is already a valid C#
    identifier, only folding illegal runes and @-escaping a reserved word."""
    s = re.sub(r"[^A-Za-z0-9_]", "_", wire_key)
    if not s:
        s = "field"
    if s[0].isdigit():
        s = "_" + s
    return "@" + s if s in CSHARP_KEYWORDS else s


def emit_methodless_class(ns: str, cs_name: str, properties: dict, source_desc: str,
                          ref_names: dict | None = None, schema_name: str | None = None) -> str:
    """Emit one method-less C# data class in namespace ``ns``: a public property
    per wire field (``[JsonPropertyName("<wire key>")]``), typed per
    ``_wire_field_cs_type``. No methods, no constructor. Shared by the REST
    wire-type emitter and the SWML-verbs / relay-protocol / SWAIG-payload
    generators so they never diverge.

    ``ref_names`` maps a sibling surfaced-object LEAF name -> its fully-qualified
    C# type name (a ``$ref`` field to one becomes a class-typed property so it
    MATCHES the reference's recorded class-typed accessor).

    ``schema_name`` is the SPEC schema name of this type (the $defs /
    components.schemas key AS IT APPEARS IN THE SPEC — NOT ``cs_name``). It is the
    scope the SDK-surface overlay (rest-apis/x-sdk-overlay.yaml) matches against:
    an overlay-hidden field is DROPPED entirely (still on the wire); an
    overlay-deprecated field is emitted with an ``[Obsolete]`` attribute."""
    ref_names = ref_names or {}
    lines: list[str] = []
    lines.append("/// <summary>")
    lines.append(f"/// {cs_name} — data type for {source_desc}.")
    lines.append("///")
    lines.append("/// A plain data object with one public property per JSON field")
    lines.append("/// (mapped via <see cref=\"System.Text.Json.Serialization.JsonPropertyNameAttribute\"/>).")
    lines.append("/// Used to deserialize responses from and serialize requests to the")
    lines.append("/// SignalWire REST API.")
    lines.append("/// </summary>")
    lines.append(f"public class {cs_name}")
    lines.append("{")
    used: set[str] = set()
    first = True
    for wire_key, psc in properties.items():
        # SDK-surface policy comes from the single overlay (rest-apis/x-sdk-overlay.yaml),
        # NOT from markup in the (often vendored) specs — matched by (field, SPEC schema
        # name) so one policy applies wherever the field surfaces.
        if _overlay_hidden(wire_key, schema_name):
            # hidden: drop from the SDK surface entirely (still on the wire).
            continue
        prop = _cs_property_name(wire_key)
        while prop.lstrip("@") in used:
            prop = prop + "_"
        used.add(prop.lstrip("@"))
        cs_type = _wire_field_cs_type(psc if isinstance(psc, dict) else {}, ref_names)
        if not first:
            lines.append("")
        first = False
        lines.append(f'    /// <summary>Wire field <c>{wire_key}</c>.</summary>')
        if _overlay_deprecated(wire_key, schema_name):
            # deprecated: still emitted (back-compat), flagged with [Obsolete].
            lines.append(f'    [System.Obsolete("Deprecated wire field {wire_key!r}.")]')
        lines.append(f'    [JsonPropertyName({cs_str(wire_key)})]')
        lines.append(f"    public {cs_type} {prop} {{ get; set; }}")
    lines.append("}")
    return TYPES_HEADER.format(desc=source_desc, ns=ns) + "\n" + "\n".join(lines) + "\n"


def _enum_const_name(value: str) -> str:
    s = re.sub(r"[^A-Za-z0-9]+", "_", value).strip("_")
    if not s:
        s = "Value"
    if s[0].isdigit():
        s = "V_" + s
    # PascalCase-ish const name; @-escape a reserved word.
    return "@" + s if s in CSHARP_KEYWORDS else s


def emit_type_enum(ns: str, enum_name: str, values: list, source_desc: str) -> str:
    """Emit a method-less C# class carrying public ``const string`` members whose
    value IS the wire string — the .NET closed-set idiom for an x-sdk-enum public
    enum (method-less, so the surface records the bare class name)."""
    lines: list[str] = []
    lines.append("/// <summary>")
    lines.append(f"/// {enum_name} — the set of accepted string values for this field.")
    lines.append("/// Each public <c>const string</c> member is one of the values the API")
    lines.append("/// accepts; the member's value is the literal wire string to send.")
    lines.append("/// </summary>")
    lines.append(f"public static class {enum_name}")
    lines.append("{")
    used: set[str] = set()
    first = True
    for v in values:
        if v == "" or not isinstance(v, str):
            continue
        cname = _enum_const_name(v)
        while cname.lstrip("@") in used:
            cname = cname + "_"
        used.add(cname.lstrip("@"))
        if not first:
            lines.append("")
        first = False
        lines.append(f"    public const string {cname} = {cs_str(v)};")
    lines.append("}")
    return TYPES_HEADER.format(desc=source_desc, ns=ns) + "\n" + "\n".join(lines) + "\n"


def snake(s: str) -> str:
    """PascalCase/mixed -> snake_case for on-disk file names."""
    s1 = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", s)
    s2 = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", s1)
    s2 = re.sub(r"[^A-Za-z0-9]+", "_", s2)
    return s2.strip("_").lower() or "schema"


def method_call_path(spec: Spec, anchor: str, markup: dict, op_path: str):
    """Return (id_args, cs_path_expr). Under-collection tails compose via
    ``Path(...)`` (relative to BasePath); a sibling op builds an absolute
    interpolated string rooted at the server prefix (§4)."""
    segs, sibling = relative_tail(spec, anchor, markup, op_path)
    id_args: list[str] = []
    pieces: list[str] = []
    for s in segs:
        if s.startswith("{") and s.endswith("}"):
            arg = arg_for(s[1:-1])
            while arg in id_args:
                arg += "2"
            id_args.append(arg)
            pieces.append(arg)
        else:
            pieces.append(cs_str(s))
    if sibling:
        full = join_path(spec.server_path, op_path.lstrip("/"))
        expr = abs_cs_path(full, id_args)
    elif not pieces:
        expr = "BasePath"
    else:
        expr = "Path(" + ", ".join(pieces) + ")"
    return id_args, expr


def abs_cs_path(full: str, id_args: list[str]) -> str:
    """Build a C# string-concat expression for a sibling absolute path,
    substituting {brace} with the positional id_args in order."""
    out = []
    literal = []
    ai = 0
    i = 0
    while i < len(full):
        if full[i] == "{":
            j = full.find("}", i)
            if literal:
                out.append(cs_str("".join(literal)))
                literal = []
            if ai < len(id_args):
                out.append(id_args[ai])
                ai += 1
            i = j + 1
            continue
        literal.append(full[i])
        i += 1
    if literal:
        out.append(cs_str("".join(literal)))
    return " + ".join(out) if out else '""'


def emit_method(spec: Spec, anchor: str, markup: dict, base: str,
                method_snake: str, op_id: str) -> str:
    if op_id not in spec.ops:
        raise SystemExit(f"{markup['name']}.{method_snake}: op {op_id!r} not in spec")
    verb, op_path, has_body = spec.ops[op_id]
    id_args, path_expr = method_call_path(spec, anchor, markup, op_path)
    name = snake_to_pascal(method_snake) + "Async"
    cls = markup["name"]

    used: set[str] = set(id_args)
    id_records = [{"name": a.lstrip("@"), "kind": "positional", "type": "string", "required": True}
                  for a in id_args]
    id_params = ["string " + a for a in id_args]
    doc = ["    /// <summary>"]
    doc.append(f"    /// Generated from operation <c>{op_id}</c> ({verb.upper()} {op_path}).")
    doc.append("    /// </summary>")
    body_ml: list[str] = []
    write_verb = verb in ("post", "put", "patch")
    verb_fn = {"post": "PostAsync", "put": "PutAsync", "patch": "PatchAsync"}.get(verb)

    if write_verb and has_body:
        body_schema = spec.op_body.get(op_id) or {}
        if is_object_body(spec, body_schema):
            fields = object_body_fields(spec, body_schema)
            field_cs, build, field_doc = body_params(spec, cls, name, fields, id_records, used)
            params = id_params + field_cs
            body_ml = build
            doc[2:2] = field_doc  # insert param docs before </summary>? keep after — simpler:
            doc = ["    /// <summary>",
                   f"    /// Generated from operation <c>{op_id}</c> ({verb.upper()} {op_path}).",
                   "    /// </summary>"] + field_doc
            call_line = f"        return Client.{verb_fn}({path_expr}, _reqBody, cancellationToken);"
        else:
            # §5.2 union body → a single ``Dictionary<string,object?> body`` param.
            body_id = _dedupe_param("body", used)
            params = id_params + [f"Dictionary<string, object?> {body_id}"]
            _register_sidecar(cls, name, id_records + [
                {"name": "body", "kind": "positional", "type": "dict<string,any>", "required": True},
            ])
            doc.append(f"    /// <param name=\"{body_id}\">JSON request body.</param>")
            call_line = f"        return Client.{verb_fn}({path_expr}, {body_id}, cancellationToken);"
    elif write_verb:
        params = id_params
        _register_sidecar(cls, name, list(id_records))
        call_line = f"        return Client.{verb_fn}({path_expr}, null, cancellationToken);"
    elif verb == "get":
        # §5.3 GET query door — a trailing query-params map.
        qp_id = _dedupe_param("queryParams", used)
        params = id_params + [f"Dictionary<string, string>? {qp_id} = null"]
        _register_sidecar(cls, name, id_records + [
            {"name": "params", "kind": "var_keyword", "type": "any", "required": False, "default": {}},
        ])
        doc.append(f"    /// <param name=\"{qp_id}\">Query-string parameters.</param>")
        call_line = f"        return Client.GetAsync({path_expr}, {qp_id}, cancellationToken);"
    else:  # delete
        params = id_params
        _register_sidecar(cls, name, list(id_records))
        call_line = f"        return Client.DeleteAsync({path_expr}, cancellationToken);"

    params = params + ["CancellationToken cancellationToken = default"]
    sig = ", ".join(params)
    lines = "\n".join(doc) + "\n"
    lines += f"    public Task<Dictionary<string, object?>> {name}({sig})\n    {{\n"
    for bl in body_ml:
        lines += bl + "\n"
    lines += call_line + "\n    }\n"
    return lines


def emit_set_method(spec: Spec, markup: dict, sm_name: str, sm: dict,
                    update_schema_fields: set[str], field_schemas: dict[str, dict]) -> str:
    handler = sm.get("handler")
    if not handler:
        raise SystemExit(f"{markup['name']}.{sm_name}: set_method missing handler")
    cls = markup["name"]
    name = snake_to_pascal(sm_name) + "Async"
    args = sm.get("args") or {}
    used: set[str] = {"resourceId"}
    params = ["string resourceId"]
    records: list[dict] = [
        {"name": "resource_id", "kind": "positional", "type": "string", "required": True},
    ]
    required_lines = []
    optional_lines = []
    arg_doc: list[str] = []
    for arg_name, arg in args.items():
        field = arg.get("field")
        if not field:
            raise SystemExit(f"{markup['name']}.{sm_name}: arg {arg_name!r} missing field")
        if field not in update_schema_fields:
            raise SystemExit(
                f"{markup['name']}.{sm_name}: arg field {field!r} not in update request schema"
            )
        ident = _dedupe_param(escape_param(arg_name), used)
        required = bool(arg.get("required"))
        fschema = field_schemas.get(field, {})
        pt = cs_param_type(spec, fschema, required)
        ct = canonical_type(spec, fschema, required)
        raw = ident.lstrip("@")
        arg_doc.append(f"    /// <param name=\"{raw}\">Bound update field <c>{field}</c>.</param>")
        # set_method args are POSITIONAL in the oracle (they wrap update()).
        rec: dict = {"name": arg_name, "kind": "positional", "type": ct, "required": required}
        if required:
            params.append(f"{pt} {ident}")
            required_lines.append(f"            [{cs_str(field)}] = {ident},")
        else:
            params.append(f"{pt} {ident} = null")
            rec["default"] = None
            optional_lines.append((ident, field))
        records.append(rec)
    extra_id = _dedupe_param("extra", used)
    params.append(f"Dictionary<string, object?>? {extra_id} = null")
    arg_doc.append(f"    /// <param name=\"{extra_id}\">Forward-compat update fields.</param>")
    records.append({"name": "extra", "kind": "var_keyword", "type": "any",
                    "required": False, "default": {}})
    _register_sidecar(cls, name, records)
    sig = ", ".join(params)

    body = []
    body.append("    /// <summary>")
    body.append(f"    /// Declarative binding helper — sets <c>call_handler={handler}</c> via UpdateAsync.")
    body.append("    /// </summary>")
    body.extend(arg_doc)
    body.append(f"    public Task<Dictionary<string, object?>> {name}({sig})")
    body.append("    {")
    body.append("        var body = new Dictionary<string, object?>")
    body.append("        {")
    body.append(f"            [\"call_handler\"] = {cs_str(handler)},")
    body.extend(required_lines)
    body.append("        };")
    for ident, field in optional_lines:
        body.append(f"        if ({ident} is not null)")
        body.append("        {")
        body.append(f"            body[{cs_str(field)}] = {ident};")
        body.append("        }")
    body.append(f"        if ({extra_id} is not null)")
    body.append("        {")
    body.append(f"            foreach (var kv in {extra_id})")
    body.append("            {")
    body.append("                body[kv.Key] = kv.Value;")
    body.append("            }")
    body.append("        }")
    body.append("        return UpdateAsync(resourceId, body);")
    body.append("    }")
    return "\n".join(body) + "\n"


def schema_fields(spec: Spec, schema: dict, seen=None) -> set[str]:
    if schema is None:
        return set()
    if seen is None:
        seen = set()
    ref = schema.get("$ref")
    if ref:
        leaf = ref.rsplit("/", 1)[-1]
        if leaf in seen:
            return set()
        seen.add(leaf)
        return schema_fields(spec, spec.schemas.get(leaf), seen)
    out = set(((schema.get("properties")) or {}).keys())
    for comb in ("allOf", "anyOf", "oneOf"):
        for br in schema.get(comb) or []:
            out |= schema_fields(spec, br, seen)
    return out


def _item_update_verb(spec: Spec, anchor: str, markup: dict) -> str | None:
    """The actual update HTTP verb (``PUT``/``PATCH``) of the item-level op under
    ``<collection>/{id}`` — the real target of x-sdk-resource.update_method. Scans
    for the single-``{param}`` item path below the collection and returns whichever
    of put/patch it declares (put wins if both, matching the update-schema lookup).
    ``None`` when no item-level write op exists (nothing to validate against)."""
    coll = collection_segment(anchor, markup)
    for path, item in (spec.doc.get("paths") or {}).items():
        if not path.startswith(coll + "/{"):
            continue
        if path.count("/{") != 1 or not path.endswith("}"):
            continue
        if item.get("put"):
            return "PUT"
        if item.get("patch"):
            return "PATCH"
    return None


def update_request_fields(spec: Spec, anchor: str, markup: dict) -> set[str]:
    coll = collection_segment(anchor, markup)
    want_verb = "put" if markup.get("update_method") == "PUT" else "patch"
    for path, item in (spec.doc.get("paths") or {}).items():
        if not path.startswith(coll + "/{"):
            continue
        if path.count("/{") != 1 or not path.endswith("}"):
            continue
        op = item.get(want_verb) or item.get("put") or item.get("patch")
        if not op:
            continue
        content = (op.get("requestBody") or {}).get("content") or {}
        for media in content.values():
            sch = media.get("schema")
            if sch:
                return schema_fields(spec, sch)
    return set()


def update_field_schemas(spec: Spec, anchor: str, markup: dict) -> dict[str, dict]:
    coll = collection_segment(anchor, markup)
    want_verb = "put" if markup.get("update_method") == "PUT" else "patch"
    for path, item in (spec.doc.get("paths") or {}).items():
        if not path.startswith(coll + "/{"):
            continue
        if path.count("/{") != 1 or not path.endswith("}"):
            continue
        op = item.get(want_verb) or item.get("put") or item.get("patch")
        if not op:
            continue
        content = (op.get("requestBody") or {}).get("content") or {}
        for media in content.values():
            sch = media.get("schema")
            if sch:
                out: dict[str, dict] = {}
                for name, psc, _ in object_body_fields(spec, sch):
                    out[name] = psc
                return out
    return {}


def emit_command_dispatch(spec: Spec, anchor: str, markup: dict) -> str:
    name = markup["name"]
    request = markup.get("request")
    if not request:
        raise SystemExit(f"{name}: command-dispatch requires request")
    commands = discriminator_mapping(spec, request)
    op = spec.ops.get("call-commands")
    if op:
        base = join_path(spec.server_path, op[1].lstrip("/"))
    else:
        base = join_path(spec.server_path, anchor.lstrip("/"))

    lines = []
    lines.append(f"/// <summary>")
    lines.append(f"/// {name} — command-dispatch resource ({spec.name} spec).")
    lines.append(f"/// Each method POSTs {{command, params, id?}} to {base}.")
    lines.append(f"/// </summary>")
    lines.append(f"public class {name}")
    lines.append("{")
    lines.append("    private readonly SignalWire.REST.HttpClient _http;")
    lines.append("")
    lines.append(f"    private const string BasePathConst = {cs_str(base)};")
    lines.append("")
    lines.append(f"    public {name}(SignalWire.REST.HttpClient http)")
    lines.append("    {")
    lines.append("        _http = http;")
    lines.append("    }")
    lines.append("")
    lines.append("    /// <summary>The command-dispatch endpoint path.</summary>")
    lines.append("    public string BasePath => BasePathConst;")
    lines.append("")
    lines.append("    private Task<Dictionary<string, object?>> ExecuteAsync(")
    lines.append("        string command, string? callId, Dictionary<string, object?> parms,")
    lines.append("        CancellationToken cancellationToken = default)")
    lines.append("    {")
    lines.append("        var body = new Dictionary<string, object?>")
    lines.append("        {")
    lines.append("            [\"command\"] = command,")
    lines.append("            [\"params\"] = parms,")
    lines.append("        };")
    lines.append("        if (callId is not null)")
    lines.append("        {")
    lines.append("            body[\"id\"] = callId;")
    lines.append("        }")
    lines.append("        return _http.PostAsync(BasePathConst, body, cancellationToken);")
    lines.append("    }")
    mapping = (spec.schemas.get(request).get("discriminator") or {}).get("mapping") or {}
    for cmd in commands:
        mname = command_method_name(cmd) + "Async"
        cmd_schema_ref = mapping.get(cmd) or {}
        cmd_leaf = cmd_schema_ref.rsplit("/", 1)[-1] if cmd_schema_ref else ""
        cmd_schema = spec.schemas.get(cmd_leaf, {})
        fields, with_id = command_param_fields(spec, cmd_schema)

        used: set[str] = set()
        records: list[dict] = []
        id_cs: list[str] = []
        if with_id:
            used.add("callId")
            id_cs.append("string callId")
            records.append({"name": "call_id", "kind": "positional",
                            "type": "string", "required": True})
        field_cs: list[str] = []
        field_doc: list[str] = []
        build: list[str] = ["        var parms = new Dictionary<string, object?>();"]
        for wire_name, schema, required in ordered_fields(fields):
            ident = _dedupe_param(escape_param(wire_name), used)
            pt = cs_param_type(spec, schema, required)
            ct = canonical_type(spec, schema, required)
            raw = ident.lstrip("@")
            field_doc.append(f"    /// <param name=\"{raw}\">Wire param <c>{wire_name}</c>.</param>")
            rec: dict = {"name": wire_name, "kind": "keyword", "type": ct, "required": required}
            if required:
                field_cs.append(f"{pt} {ident}")
                build.append(f"        parms[{cs_str(wire_name)}] = {ident};")
            else:
                field_cs.append(f"{pt} {ident} = null")
                rec["default"] = None
                build.append(f"        if ({ident} is not null)")
                build.append("        {")
                build.append(f"            parms[{cs_str(wire_name)}] = {ident};")
                build.append("        }")
            records.append(rec)
        extras_id = _dedupe_param("extras", used)
        field_cs.append(f"Dictionary<string, object?>? {extras_id} = null")
        field_doc.append(f"    /// <param name=\"{extras_id}\">Forward-compat command params.</param>")
        records.append({"name": "extras", "kind": "keyword",
                        "type": "optional<dict<string,any>>", "required": False, "default": None})
        build.append(f"        if ({extras_id} is not null)")
        build.append("        {")
        build.append(f"            foreach (var kv in {extras_id})")
        build.append("            {")
        build.append("                parms[kv.Key] = kv.Value;")
        build.append("            }")
        build.append("        }")
        _register_sidecar(name, mname, records)

        # CancellationToken is a C#-async idiom param (not a wire param): it is
        # threaded through to the HttpClient call but deliberately NOT recorded
        # in the sidecar surface, mirroring CrudResource's CT params.
        field_cs.append("CancellationToken cancellationToken = default")
        sig = ", ".join(id_cs + field_cs)
        call_arg = "callId" if with_id else "null"
        lines.append("")
        lines.append("    /// <summary>")
        lines.append(f"    /// Command <c>{cmd}</c>.")
        lines.append("    /// </summary>")
        lines.extend(field_doc)
        lines.append(f"    public Task<Dictionary<string, object?>> {mname}({sig})")
        lines.append("    {")
        lines.extend(build)
        lines.append(f"        return ExecuteAsync({cs_str(cmd)}, {call_arg}, parms, cancellationToken);")
        lines.append("    }")
    lines.append("}")
    return GEN_HEADER.format(desc=f"Generated command-dispatch resource for the {spec.name!r} namespace.") + "\n" + "\n".join(lines) + "\n"


def emit_read_or_base_class(spec: Spec, anchor: str, markup: dict, base: str) -> str:
    """.NET has no ReadResource / BaseResource CLASS. Emit a standalone class
    whose surface is: (ReadResource) an inline List/Get pair + its declared
    methods; (BaseResource) purely its declared methods. Both bake BasePath and
    provide a private Path() helper (matches the .NET hand standalone resources
    like VideoRoomSessions / RegistryBrands)."""
    name = markup["name"]
    bp = base_path(spec, anchor, markup)
    lines = []
    lines.append(f"/// <summary>")
    lines.append(f"/// {name} — REST resource for the {spec.name} API.")
    lines.append(f"/// </summary>")
    lines.append(f"public class {name}")
    lines.append("{")
    lines.append("    private readonly SignalWire.REST.HttpClient _client;")
    lines.append("")
    lines.append(f"    public {name}(SignalWire.REST.HttpClient client)")
    lines.append("    {")
    lines.append("        _client = client;")
    lines.append("    }")
    lines.append("")
    lines.append("    /// <summary>The HTTP client this resource dispatches through.</summary>")
    lines.append("    protected SignalWire.REST.HttpClient Client => _client;")
    lines.append("")
    lines.append("    /// <summary>The resource's base API path.</summary>")
    lines.append(f"    public string BasePath => {cs_str(bp)};")
    lines.append("")
    lines.append("    /// <summary>Build a full path by appending segments to the base path.</summary>")
    lines.append("    protected string Path(params string[] parts)")
    lines.append("    {")
    lines.append("        return parts is null || parts.Length == 0 ? BasePath : BasePath + \"/\" + string.Join(\"/\", parts);")
    lines.append("    }")

    if base == "ReadResource":
        # inline list/get (the .NET hand read-only resources do exactly this).
        lines.append("")
        lines.append("    /// <summary>List resources (GET BasePath).</summary>")
        lines.append("    public Task<Dictionary<string, object?>> ListAsync(")
        lines.append("        Dictionary<string, string>? queryParams = null,")
        lines.append("        CancellationToken cancellationToken = default)")
        lines.append("    {")
        lines.append("        return Client.GetAsync(BasePath, queryParams, cancellationToken);")
        lines.append("    }")
        lines.append("")
        lines.append("    /// <summary>Retrieve a single resource by id (GET BasePath/{id}).</summary>")
        lines.append("    public Task<Dictionary<string, object?>> GetAsync(")
        lines.append("        string id, CancellationToken cancellationToken = default)")
        lines.append("    {")
        lines.append("        return Client.GetAsync(Path(id), cancellationToken: cancellationToken);")
        lines.append("    }")
        lines.append("")
        lines.append("    /// <summary>Iterate every item across all pages of this resource's")
        lines.append("    /// list endpoint, following ``links.next`` cursors (lazy — no request")
        lines.append("    /// fires until iteration). Mirrors Python ``ReadResource.paginate``.</summary>")
        lines.append("    public SignalWire.REST.PaginatedIterator Paginate(")
        lines.append("        Dictionary<string, string>? queryParams = null)")
        lines.append("    {")
        lines.append("        return new SignalWire.REST.PaginatedIterator(Client, BasePath, queryParams, dataKey: \"data\");")
        lines.append("    }")

    _emit_declared_and_sets(spec, anchor, markup, base, lines)
    lines.append("}")
    return GEN_HEADER.format(desc=f"Generated REST resource for the {spec.name!r} namespace.") + "\n" + "\n".join(lines) + "\n"


def _emit_declared_and_sets(spec: Spec, anchor: str, markup: dict, base: str, lines: list[str]) -> None:
    provided = BASE_PROVIDES[base]
    declared = markup.get("methods") or {}
    for method_snake, spec_ref in declared.items():
        op_id = spec_ref.get("op")
        if not op_id:
            raise SystemExit(f"{markup['name']}.{method_snake}: method markup missing op")
        if method_snake in provided:
            if method_snake == "list_addresses":
                verb, op_path, _ = spec.ops[op_id]
                _, sibling = relative_tail(spec, anchor, markup, op_path)
                if not sibling:
                    continue
            else:
                continue
        lines.append("")
        lines.append(emit_method(spec, anchor, markup, base, method_snake, op_id).rstrip("\n"))

    set_methods = markup.get("set_methods") or {}
    if set_methods:
        if base not in ("CrudResource", "FabricResource"):
            raise SystemExit(f"{markup['name']}: set_methods require a CRUD base, got {base}")
        upd_fields = update_request_fields(spec, anchor, markup)
        upd_field_schemas = update_field_schemas(spec, anchor, markup)
        for sm_name, sm in set_methods.items():
            lines.append("")
            lines.append(emit_set_method(spec, markup, sm_name, sm, upd_fields, upd_field_schemas).rstrip("\n"))


def emit_crud_resource(spec: Spec, anchor: str, markup: dict, base: str) -> str:
    """CrudResource / FabricResource → extend the hand C# base classes.
    CrudResource base uses PUT; a PATCH resource overrides UpdateAsync.
    FabricResource → CrudWithAddresses (adds ListAddressesAsync); PATCH override
    as needed."""
    name = markup["name"]
    # §9: write-capable bases require update_method matching the spec verb.
    upd = markup.get("update_method")
    if not upd:
        raise SystemExit(f"{name}: {base} requires update_method")
    # The actual update op is the item-level PUT/PATCH under <collection>/{id}
    # (the anchor collection path only carries list/create), so detect the real
    # verb there — an anchor-only check (as some sibling ports do) never fires,
    # since the anchor has no put/patch. Fail loud on a real mismatch (RULES §9).
    spec_verb = _item_update_verb(spec, anchor, markup)
    if spec_verb and upd != spec_verb:
        raise SystemExit(f"{name}: update_method {upd} != spec update verb {spec_verb}")

    parent = "CrudResource" if base == "CrudResource" else "CrudWithAddresses"
    bp = base_path(spec, anchor, markup)

    lines = []
    lines.append(f"/// <summary>")
    lines.append(f"/// {name} — REST resource for the {spec.name} API.")
    lines.append(f"/// </summary>")
    lines.append(f"public class {name} : SignalWire.REST.{parent}")
    lines.append("{")
    lines.append(f"    public {name}(SignalWire.REST.HttpClient client)")
    lines.append(f"        : base(client, {cs_str(bp)})")
    lines.append("    {")
    lines.append("    }")

    # CrudResource base updates via PUT. A PATCH resource overrides UpdateAsync.
    if upd == "PATCH":
        lines.append("")
        lines.append("    /// <summary>Update this resource via an HTTP PATCH request.</summary>")
        lines.append("    public override Task<Dictionary<string, object?>> UpdateAsync(")
        lines.append("        string id, Dictionary<string, object?> data,")
        lines.append("        CancellationToken cancellationToken = default)")
        lines.append("    {")
        lines.append("        return Client.PatchAsync(Path(id), data, cancellationToken);")
        lines.append("    }")

    _emit_declared_and_sets(spec, anchor, markup, base, lines)
    lines.append("}")
    return GEN_HEADER.format(desc=f"Generated REST resource for the {spec.name!r} namespace.") + "\n" + "\n".join(lines) + "\n"


# Surface manifest accumulator: ClassName -> sorted list of the canonical
# (Python-oracle) method names the resource publishes on its OWN class. The
# .NET adapter's surface enumerator projects this VERBATIM onto the
# ``<ns>_resources_generated.<Name>`` module so it matches the python oracle
# 0/0 (the oracle records a resource's OWN-body methods only: inherited CRUD
# ops live on the base and are NOT re-recorded on the subclass — RULES §11 /
# the python surface enumerator; the crud_base structural equivalence covers
# them on the signature side). Keyed by class name (unique across all specs).
_SURFACE: dict[str, list[str]] = {}


def _declared_surface_names(spec: Spec, anchor: str, markup: dict, base: str) -> set[str]:
    """The canonical method names the resource declares on its OWN body,
    matching what the python generated subclass records (and thus the oracle):

    * CRUD bases (CrudResource / FabricResource): python overrides ``create`` +
      ``update`` with the typed body — recorded on the subclass. ``list``/
      ``get``/``delete``/``list_addresses`` stay on the base and are NOT
      re-recorded (the .NET generator inlines list/get for ReadResource but the
      oracle does not carry them — so this manifest, not the .cs body, is the
      surface source of truth).
    * ReadResource: nothing of its own beyond declared ``methods``/``set_methods``
      (list/get inherited).
    * BaseResource: the generator emits ONLY its declared methods (no inline
      CRUD), so the manifest is exactly the declared set.

    Plus every declared operation method, set_method, and (for a sibling
    ``list_addresses``) that method. Always includes ``__init__``."""
    names: set[str] = {"__init__"}
    provided = BASE_PROVIDES[base]
    for method_snake, spec_ref in (markup.get("methods") or {}).items():
        if method_snake in provided:
            # ``list_addresses`` re-emitted only as a sibling op (matches the
            # generator's own _emit_declared_and_sets rule); other provided
            # methods stay on the base.
            if method_snake == "list_addresses":
                op_id = spec_ref.get("op")
                verb, op_path, _ = spec.ops[op_id]
                _, sibling = relative_tail(spec, anchor, markup, op_path)
                if sibling:
                    names.add("list_addresses")
            continue
        names.add(method_snake)
    for sm_name in (markup.get("set_methods") or {}).keys():
        names.add(sm_name)
    if base in ("CrudResource", "FabricResource"):
        names.update({"create", "update"})
    elif base == "ReadResource":
        # list/get inherited from the base — not recorded on the subclass. But
        # the python oracle DOES record ``paginate`` on each ReadResource
        # subclass (the log resources + FabricAddresses), so surface it here.
        # .NET realizes it as the inline ``Paginate`` returning PaginatedIterator.
        names.add("paginate")
    elif base == "BaseResource":
        # Every operation of a BaseResource IS a declared method (already added).
        pass
    return names


def _command_surface_names(spec: Spec, markup: dict) -> set[str]:
    request = markup.get("request")
    commands = discriminator_mapping(spec, request)
    names = {"__init__"}
    for cmd in commands:
        # snake_case of the C# method name (command_method_name gives Pascal).
        names.add(_pascal_to_snake_name(command_method_name(cmd)))
    return names


def _pascal_to_snake_name(name: str) -> str:
    s1 = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", name)
    s2 = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", s1)
    return s2.lower()


def emit_resource(spec: Spec, anchor: str, markup: dict) -> str:
    base = markup["base"]
    name = markup["name"]
    if markup.get("kind") == "command-dispatch":
        _SURFACE[name] = sorted(_command_surface_names(spec, markup))
        return emit_command_dispatch(spec, anchor, markup)
    if base not in BASE_PROVIDES:
        raise SystemExit(f"{name}: unknown base {base!r}")
    _SURFACE[name] = sorted(_declared_surface_names(spec, anchor, markup, base))
    if base in ("CrudResource", "FabricResource"):
        return emit_crud_resource(spec, anchor, markup, base)
    return emit_read_or_base_class(spec, anchor, markup, base)


# ---------------------------------------------------------------------------
# Client tree (§8).
# ---------------------------------------------------------------------------

# Container attr -> (C# container class, RestClient accessor).
CONTAINERS = {
    "fabric": ("FabricNamespace", "Fabric"),
    "video": ("VideoNamespace", "Video"),
    "logs": ("LogsNamespace", "Logs"),
    "registry": ("RegistryNamespace", "Registry"),
    "project": ("ProjectNamespace", "Project"),
    "datasphere": ("DatasphereNamespace", "Datasphere"),
}

# Accessor-name overrides — mirrors the Python reference generator's
# ``_ATTR_OVERRIDE`` table. Values are the canonical snake_case accessor; the
# C# accessor name is PascalCase of it.
ATTR_OVERRIDE = {
    "GenericResources": "resources", "FabricAddresses": "addresses",
    "FabricTokens": "tokens", "DatasphereDocuments": "documents",
    "ProjectTokens": "tokens", "PubSub": "pubsub",
    "MessageLogs": "messages", "VoiceLogs": "voice", "FaxLogs": "fax",
    "ConferenceLogs": "conferences",
}


def container_accessor(markup: dict, name: str, container: str) -> str:
    if markup.get("attr"):
        return snake_to_pascal(markup["attr"])
    if name in ATTR_OVERRIDE:
        return snake_to_pascal(ATTR_OVERRIDE[name])
    lead = container[:1].upper() + container[1:]
    stem = name[len(lead):] if name.startswith(lead) else name
    return stem[:1].upper() + stem[1:] if stem else name


def resolve_placement(specs: list[Spec]):
    placed = []
    for spec in specs:
        for anchor, markup in spec.resources():
            container = markup.get("namespace") or spec.namespace_attr or ""
            placed.append((spec, anchor, markup, container))
    return placed


def emit_container(container: str, members: list[tuple[str, str]]) -> str:
    cls, _ = CONTAINERS[container]
    lines = []
    lines.append(f"/// <summary>")
    lines.append(f"/// {cls} — generated container grouping the {container} namespace resources (§8).")
    lines.append(f"/// </summary>")
    lines.append(f"public class {cls}")
    lines.append("{")
    lines.append("    private readonly SignalWire.REST.HttpClient _http;")
    for accessor, class_name in members:
        lines.append(f"    private {class_name}? _{accessor[:1].lower() + accessor[1:]};")
    lines.append("")
    lines.append(f"    public {cls}(SignalWire.REST.HttpClient http)")
    lines.append("    {")
    lines.append("        _http = http;")
    lines.append("    }")
    for accessor, class_name in members:
        field = "_" + accessor[:1].lower() + accessor[1:]
        lines.append("")
        lines.append(f"    /// <summary>The {class_name} resource.</summary>")
        lines.append(f"    public {class_name} {accessor} => {field} ??= new {class_name}(_http);")
    lines.append("}")
    return GEN_HEADER.format(desc=f"Generated REST client container for the {container} namespace (§8).") + "\n" + "\n".join(lines) + "\n"


def flat_accessor(name: str) -> str:
    if name in ATTR_OVERRIDE:
        return snake_to_pascal(ATTR_OVERRIDE[name])
    return name


def emit_resource_tree(placed) -> str:
    """Emit ResourceTree: a partial class the hand RestClient composes,
    providing a lazy accessor per FLAT resource + per CONTAINER (§8)."""
    flats = []           # (accessor, class)
    containers_seen = []  # ordered container attrs
    seen_c = set()
    for spec, anchor, markup, container in placed:
        name = markup["name"]
        if not container:
            flats.append((flat_accessor(name), name))
        else:
            if container not in seen_c:
                seen_c.add(container)
                containers_seen.append(container)

    lines = []
    lines.append("/// <summary>")
    lines.append("/// ResourceTree — lazy accessors for every REST resource and")
    lines.append("/// namespace container. <see cref=\"SignalWire.REST.RestClient\"/> inherits")
    lines.append("/// this tree, so every resource is reachable directly from an")
    lines.append("/// authenticated client.")
    lines.append("/// </summary>")
    lines.append("public partial class ResourceTree")
    lines.append("{")
    lines.append("    private readonly SignalWire.REST.HttpClient _generatedHttp;")
    for accessor, cls in flats:
        lines.append(f"    private {cls}? _{accessor[:1].lower() + accessor[1:]};")
    for c in containers_seen:
        clsname, acc = CONTAINERS[c]
        lines.append(f"    private {clsname}? _{acc[:1].lower() + acc[1:]};")
    lines.append("")
    lines.append("    public ResourceTree(SignalWire.REST.HttpClient http)")
    lines.append("    {")
    lines.append("        _generatedHttp = http;")
    lines.append("    }")
    lines.append("")
    lines.append("    /// <summary>The authenticated transport this tree dispatches through")
    lines.append("    /// (exposed to the inheriting RestClient for disposal; protected so it")
    lines.append("    /// is not public route/surface).</summary>")
    lines.append("    protected SignalWire.REST.HttpClient GeneratedHttp => _generatedHttp;")
    for accessor, cls in flats:
        field = "_" + accessor[:1].lower() + accessor[1:]
        lines.append("")
        lines.append(f"    /// <summary>The {cls} resource.</summary>")
        lines.append(f"    public {cls} {accessor} => {field} ??= new {cls}(_generatedHttp);")
    for c in containers_seen:
        clsname, acc = CONTAINERS[c]
        field = "_" + acc[:1].lower() + acc[1:]
        lines.append("")
        lines.append(f"    /// <summary>The {clsname} container.</summary>")
        lines.append(f"    public {clsname} {acc} => {field} ??= new {clsname}(_generatedHttp);")
    lines.append("}")
    return GEN_HEADER.format(desc="Generated REST resource tree the hand RestClient composes (§8).") + "\n" + "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------
# REST wire-type surface (item A/H) — <ns>_types_generated per namespace.
# ---------------------------------------------------------------------------

# The TYPE namespaces — (spec-dir, C# namespace segment, oracle
# <ns>_types_generated leaf) — are DISCOVERED via discover_type_ns() (scanning
# rest-apis/): every resource namespace PLUS the types-only webhook-payload specs
# (components.schemas but no servers block, e.g. swml-webhooks). relay-rest folds
# the registry spec.

# The C# namespace every REST wire type lands under (routed to the oracle by the
# surface/signature enumerators via this namespace prefix).
TYPES_CS_NS_BASE = "SignalWire.REST.Namespaces.Generated.Types"


def _load_types_schemas(psdk: Path, spec_dir: str) -> dict:
    """Load a spec's components/schemas WITHOUT the full Spec model (swml-webhooks
    has no servers block, so Spec() would reject it). Preserves yaml order."""
    doc = yaml.safe_load((psdk / "rest-apis" / spec_dir / "openapi.yaml").read_text())
    return ((doc.get("components") or {}).get("schemas")) or {}


def emit_types(psdk: Path, outs: dict) -> None:
    """Emit every <ns>_types_generated method-less C# data class / closed-set into
    ``Types/<NsMod>/<snake_name>.cs`` keys of ``outs`` (relative to the Generated
    dir)."""
    for spec_dir, ns_mod, ns_key in discover_type_ns(psdk):
        schemas = _load_types_schemas(psdk, spec_dir)
        cs_ns = f"{TYPES_CS_NS_BASE}.{ns_mod}"
        # Surfaced-object leaf -> fully-qualified C# type in this module (a $ref
        # field to one becomes a class-typed property matching the reference).
        ref_names = {
            type_name(n): f"{cs_ns}.{type_name(n)}"
            for n, node in schemas.items()
            if isinstance(node, dict) and is_object_schema(node)
        }
        for raw_name, node in schemas.items():
            if not isinstance(node, dict):
                continue
            xe = node.get("x-sdk-enum")
            if xe:
                enum_name = type_name(xe)
                fn = f"Types/{ns_mod}/{snake(enum_name)}.cs"
                if fn not in outs:
                    outs[fn] = emit_type_enum(
                        cs_ns, enum_name, list(node.get("enum") or []),
                        f"x-sdk-enum on components/schemas {raw_name!r}, {ns_key!r} spec")
                continue
            if is_object_schema(node):
                cs_name = type_name(raw_name)
                fn = f"Types/{ns_mod}/{snake(cs_name)}.cs"
                if fn not in outs:
                    outs[fn] = emit_methodless_class(
                        cs_ns, cs_name, node.get("properties") or {},
                        f"{ns_key!r} spec components/schemas {raw_name!r}",
                        ref_names=ref_names, schema_name=raw_name)


# ---------------------------------------------------------------------------
# Driver.
# ---------------------------------------------------------------------------

def build_outputs(psdk: Path) -> dict[str, str]:
    load_bases(psdk)  # validate x-sdk-bases (fail loud); not otherwise needed
    _SIDECAR.clear()
    specs = [load_spec(psdk, ns) for ns in discover_spec_dirs(psdk)]
    _SURFACE.clear()
    outs: dict[str, str] = {}
    # class -> "<ns>_resources_generated" python module leaf (the oracle's
    # per-namespace generated-resource module). ns key mirrors the python
    # reference: ``relay-rest`` -> ``relay_rest``.
    class_module: dict[str, str] = {}
    for spec in specs:
        mod_ns = spec.name.replace("-", "_")
        for anchor, markup in spec.resources():
            src = emit_resource(spec, anchor, markup)
            outs[markup["name"] + ".cs"] = src
            class_module[markup["name"]] = f"{mod_ns}_resources_generated"
    placed = resolve_placement(specs)
    by_container: dict[str, list[tuple[str, str]]] = {}
    order: list[str] = []
    for spec, anchor, markup, container in placed:
        if not container:
            continue
        if container not in by_container:
            by_container[container] = []
            order.append(container)
        acc = container_accessor(markup, markup["name"], container)
        by_container[container].append((acc, markup["name"]))
    for container in order:
        if container not in CONTAINERS:
            raise SystemExit(f"container attr {container!r} has no C# container class (add to CONTAINERS)")
        cls, _ = CONTAINERS[container]
        outs[cls + ".cs"] = emit_container(container, by_container[container])
    outs["ResourceTree.cs"] = emit_resource_tree(placed)

    # Sidecar (§5): the canonical typed-param records the signature enumerator
    # UNFOLDS onto the reflected C# methods. Keyed "<ClassName>::<csMethod>".
    sidecar: dict[str, list[dict]] = {}
    for (cls, cs_method) in sorted(_SIDECAR.keys()):
        sidecar[f"{cls}::{cs_method}"] = _SIDECAR[(cls, cs_method)]
    # Container manifest: C# container class -> "_client_tree_generated" (the
    # oracle module all six namespace containers live in). The container's C#
    # property accessors are the .NET instance-attribute idiom (python sets them
    # as ``self.x = ...`` in __init__), so the surface records ONLY ``__init__``.
    containers = {cls: "_client_tree_generated" for (cls, _acc) in CONTAINERS.values()}

    outs["rest_signatures.json"] = json.dumps(
        {
            "_comment": "Code generated by scripts/generate_rest.py; DO NOT EDIT. "
                        "Canonical typed-param records for generated REST operation/"
                        "command/set methods; consumed by scripts/enumerate_signatures.py "
                        "to unfold the reflected C# params onto the Python oracle shape. "
                        "The class_module/surface/containers manifests drive the surface + "
                        "signature enumerators' <ns>_resources_generated / _client_tree_generated "
                        "projection.",
            "class_module": dict(sorted(class_module.items())),
            "containers": dict(sorted(containers.items())),
            "surface": dict(sorted(_SURFACE.items())),
            "methods": sidecar,
        },
        indent=2, sort_keys=False,
    ) + "\n"

    # Wire types (item A/H): one method-less C# data class / closed-set per
    # components/schemas object across all 13 REST namespaces, under Types/<NsMod>/.
    emit_types(psdk, outs)
    return outs


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true", help="GEN-FRESH: exit non-zero if stale")
    ap.add_argument("--out", default="", help="scratch: emit flat into this dir")
    args = ap.parse_args(argv)

    psdk = resolve_porting_sdk()
    outs = build_outputs(psdk)

    if args.out:
        out_dir = Path(args.out)
    else:
        out_dir = repo_root() / "src" / "SignalWire" / "REST" / "Namespaces" / "Generated"

    if args.check:
        stale = []
        for fn, src in outs.items():
            p = out_dir / fn
            if not p.is_file() or p.read_text() != src:
                stale.append(str(p))
        expected = set(outs.keys())
        for p in sorted(out_dir.rglob("*.cs")):
            rel = p.relative_to(out_dir).as_posix()
            # The GenTypes/ subtree (SWML-verbs / RELAY-protocol / SWAIG-payload)
            # is owned by the three standalone generators, each GEN-FRESH-checked
            # separately; this generator owns only the resource layer + Types/.
            if rel.startswith("GenTypes/"):
                continue
            if rel not in expected:
                stale.append(f"{p} (leftover — not in generator output)")
        if stale:
            sys.stderr.write("GEN-FRESH FAIL: %d generated REST file(s) stale:\n" % len(stale))
            for s in stale:
                sys.stderr.write("  - %s\n" % s)
            return 1
        print("GEN-FRESH: generated REST files match the canonical specs.")
        return 0

    out_dir.mkdir(parents=True, exist_ok=True)
    for fn, src in outs.items():
        p = out_dir / fn
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(src)
    print(f"generated {len(outs)} REST file(s) into {out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
