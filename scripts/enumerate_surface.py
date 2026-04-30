#!/usr/bin/env python3
"""enumerate_surface.py -- emit port_surface.json for the .NET SignalWire SDK.

This walks ``src/SignalWire/**/*.cs``, parses out namespace/class/public-method
structure with regex, and emits JSON matching the shape of
``porting-sdk/python_surface.json``.

Symbol naming contract:

* C# uses PascalCase for methods and properties; Python uses snake_case. The
  diff against ``python_surface.json`` is by Python-canonical symbol name, so
  every method emitted here gets translated PascalCase -> snake_case.
* Constructors are emitted as ``__init__``.
* Async methods named ``FooAsync`` are emitted as ``foo`` (matches Python
  reference, which has no Async suffix).
* C# namespaces map to Python's canonical module path via ``CLASS_MODULE_MAP``.
* ``Service`` (in SignalWire.SWML) renames to ``SWMLService`` (Python convention).
* ``Client`` (in SignalWire.Relay) renames to ``RelayClient``.
* Skills carry the ``Skill`` suffix in C# (e.g. ``WebSearchSkill``); the Python
  reference keeps that suffix, so no rename needed on those.
* ``IDisposable.Dispose``, ``ToString``, ``GetHashCode``, ``Equals``, and other
  .NET object overrides are skipped — they're language-required, not part of
  the SDK contract.

Regex parsing is fine for this SDK's size (~50 .cs files); we don't need
Roslyn.

Usage:
    python3 scripts/enumerate_surface.py            # write port_surface.json
    python3 scripts/enumerate_surface.py --check    # exit 1 on drift
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path


# ---------------------------------------------------------------------------
# C# class/struct/enum -> Python module mapping
# ---------------------------------------------------------------------------
#
# Every class in the .NET SDK has to be reported under a Python-reference
# dotted module name so the diff against ``python_surface.json`` lines up.
# Anything not in this map falls back to the native-namespace translation
# (``SignalWire.Rest.PhoneNumbers`` -> ``signalwire.rest.phone_numbers``).
CLASS_MODULE_MAP: dict[str, str] = {
    # -- agent ------------------------------------------------------------
    "AgentBase": "signalwire.core.agent_base",

    # -- contexts ---------------------------------------------------------
    "Context": "signalwire.core.contexts",
    "ContextBuilder": "signalwire.core.contexts",
    "GatherInfo": "signalwire.core.contexts",
    "GatherQuestion": "signalwire.core.contexts",
    "Step": "signalwire.core.contexts",

    # -- datamap ----------------------------------------------------------
    "DataMap": "signalwire.core.data_map",

    # -- swaig ------------------------------------------------------------
    "FunctionResult": "signalwire.core.function_result",

    # -- skills -----------------------------------------------------------
    "SkillBase": "signalwire.core.skill_base",
    "SkillManager": "signalwire.core.skill_manager",
    "SkillRegistry": "signalwire.skills.registry",

    # -- server -----------------------------------------------------------
    "AgentServer": "signalwire.agent_server",

    # -- security ---------------------------------------------------------
    "SessionManager": "signalwire.core.security.session_manager",

    # -- swml -------------------------------------------------------------
    # ``Service`` in SignalWire.SWML == Python's ``SWMLService``.
    # Renamed via CLASS_RENAME_MAP, mapped here.

    # -- relay ------------------------------------------------------------
    "Call": "signalwire.relay.call",
    "Message": "signalwire.relay.message",
    # All Relay Action subclasses live under ``signalwire.relay.call`` in
    # Python (one big module). .NET splits each action into its own
    # source file / namespace.
    "Action": "signalwire.relay.call",
    "AIAction": "signalwire.relay.call",
    "CollectAction": "signalwire.relay.call",
    "ConnectAction": "signalwire.relay.call",
    "DetectAction": "signalwire.relay.call",
    "FaxAction": "signalwire.relay.call",
    "PayAction": "signalwire.relay.call",
    "PlayAction": "signalwire.relay.call",
    "RecordAction": "signalwire.relay.call",
    "ReferAction": "signalwire.relay.call",
    "SendDigitsAction": "signalwire.relay.call",
    "StandaloneCollectAction": "signalwire.relay.call",
    "StreamAction": "signalwire.relay.call",
    "TapAction": "signalwire.relay.call",
    "TranscribeAction": "signalwire.relay.call",
    "DialAction": "signalwire.relay.call",
    "DenoiseAction": "signalwire.relay.call",
    "EchoAction": "signalwire.relay.call",
    "QueueAction": "signalwire.relay.call",
    "PromptAction": "signalwire.relay.call",
    "Event": "signalwire.relay.event",

    # -- prefabs ----------------------------------------------------------
    "ConciergeAgent": "signalwire.prefabs.concierge",
    "FAQBotAgent": "signalwire.prefabs.faq_bot",
    "InfoGathererAgent": "signalwire.prefabs.info_gatherer",
    "ReceptionistAgent": "signalwire.prefabs.receptionist",
    "SurveyAgent": "signalwire.prefabs.survey",

    # -- skills (one canonical Python module per skill) -------------------
    "ApiNinjasTriviaSkill": "signalwire.skills.api_ninjas_trivia.skill",
    "ClaudeSkillsSkill": "signalwire.skills.claude_skills.skill",
    "CustomSkillsSkill": "signalwire.skills.custom_skills.skill",
    "DatasphereSkill": "signalwire.skills.datasphere.skill",
    "DatasphereServerlessSkill": "signalwire.skills.datasphere_serverless.skill",
    "DatetimeSkill": "signalwire.skills.datetime.skill",
    "GoogleMapsSkill": "signalwire.skills.google_maps.skill",
    "InfoGathererSkill": "signalwire.skills.info_gatherer.skill",
    "JokeSkill": "signalwire.skills.joke.skill",
    "MathSkill": "signalwire.skills.math.skill",
    "McpGatewaySkill": "signalwire.skills.mcp_gateway.skill",
    "NativeVectorSearchSkill": "signalwire.skills.native_vector_search.skill",
    "PlayBackgroundFileSkill": "signalwire.skills.play_background_file.skill",
    "SpiderSkill": "signalwire.skills.spider.skill",
    "SwmlTransferSkill": "signalwire.skills.swml_transfer.skill",
    "WeatherApiSkill": "signalwire.skills.weather_api.skill",
    "WebSearchSkill": "signalwire.skills.web_search.skill",
    "WikipediaSearchSkill": "signalwire.skills.wikipedia_search.skill",
}


# (source_namespace, source_class) -> (target_module, target_class) for
# classes that get a Python-canonical rename.
CLASS_RENAME_MAP: dict[tuple[str, str], tuple[str, str]] = {
    ("SignalWire.SWML", "Service"): (
        "signalwire.core.swml_service", "SWMLService",
    ),
    # SignalWire.Relay's ``Client`` is Python's ``RelayClient``.
    ("SignalWire.Relay", "Client"): (
        "signalwire.relay.client", "RelayClient",
    ),
    # SignalWire.REST's ``RestClient`` is Python's
    # ``signalwire.rest.client.RestClient``. .NET's auto-derived module
    # ``signalwire.rest.rest_client`` doesn't match Python's canonical
    # path ``signalwire.rest.client``.
    ("SignalWire.REST", "RestClient"): (
        "signalwire.rest.client", "RestClient",
    ),
    # .NET's REST namespace classes (in namespace ``SignalWire.REST.Namespaces``)
    # are named after the namespace (``Calling``, ``Fabric``); Python
    # places each in its own submodule and suffixes the class with
    # ``Namespace``.
    ("SignalWire.REST.Namespaces", "Calling"): (
        "signalwire.rest.namespaces.calling", "CallingNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Fabric"): (
        "signalwire.rest.namespaces.fabric", "FabricNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Compat"): (
        "signalwire.rest.namespaces.compat", "CompatNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Datasphere"): (
        "signalwire.rest.namespaces.datasphere", "DatasphereNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Logs"): (
        "signalwire.rest.namespaces.logs", "LogsNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Project"): (
        "signalwire.rest.namespaces.project", "ProjectNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Registry"): (
        "signalwire.rest.namespaces.registry", "RegistryNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Video"): (
        "signalwire.rest.namespaces.video", "VideoNamespace",
    ),
}


# Skill class renames -- our .NET names already carry the ``Skill`` suffix
# (e.g. ``WebSearchSkill``); the Python reference uses the same convention
# but the canonical class name itself sometimes differs (e.g. ``DataSphereSkill``
# in Python vs ``DatasphereSkill`` in .NET). Apply rename so the diff lines up.
SKILL_RENAMES: dict[str, str] = {
    "DatasphereSkill": "DataSphereSkill",
    "DatasphereServerlessSkill": "DataSphereServerlessSkill",
    "McpGatewaySkill": "MCPGatewaySkill",
    "SwmlTransferSkill": "SWMLTransferSkill",
}


# Method-name renames applied AFTER pascal_to_snake. When .NET's PascalCase
# CamelCases something Python keeps as a single word (e.g. ``Foreach`` =>
# ``foreach``), the casing rule produces an extra underscore. The map below
# normalises those mismatches.
METHOD_RENAMES: dict[str, str] = {
    "for_each": "foreach",
}

# Methods we never emit. .NET's IDisposable/object overrides aren't part of
# the SDK contract.
SKIP_METHOD_NAMES: set[str] = {
    "Dispose", "ToString", "GetHashCode", "Equals", "Finalize",
    "MemberwiseClone",
    # C# constructs that can superficially look like methods
    "operator", "using", "typedef", "friend", "template", "return",
    "if", "else", "for", "while", "do", "switch", "case", "lock",
    "try", "catch", "finally", "throw",
}


# Methods to project onto the AgentBase mixin classes Python uses but C# has
# flattened onto AgentBase. Mirrors enumerate_surface.py from the C++ port.
MIXIN_PROJECTIONS: dict[tuple[str, str], list[str]] = {
    ("signalwire.core.mixins.ai_config_mixin", "AIConfigMixin"): [
        "add_function_include", "add_hint", "add_hints", "add_internal_filler",
        "add_language", "add_pattern_hint", "add_pronunciation",
        "enable_debug_events",
        "set_function_includes", "set_global_data", "set_internal_fillers",
        "set_languages", "set_native_functions", "set_param", "set_params",
        "set_post_prompt_llm_params", "set_prompt_llm_params",
        "set_pronunciations", "update_global_data",
    ],
    ("signalwire.core.mixins.auth_mixin", "AuthMixin"): [],
    ("signalwire.core.mixins.mcp_server_mixin", "MCPServerMixin"): [],
    ("signalwire.core.mixins.prompt_mixin", "PromptMixin"): [
        "contexts", "define_contexts", "get_post_prompt", "get_prompt",
        "prompt_add_section", "prompt_add_subsection", "prompt_add_to_section",
        "prompt_has_section", "reset_contexts", "set_post_prompt",
        "set_prompt_pom", "set_prompt_text",
    ],
    # Python additionally extracted a ``PromptManager`` class that
    # PromptMixin delegates to. Most of the same methods exist there too
    # (the user-facing surface is identical — `agent.prompt_manager.X`
    # ≡ `agent.X`). Project the same set so the cross-language audit
    # treats both paths as covered.
    ("signalwire.core.agent.prompt.manager", "PromptManager"): [
        "define_contexts", "get_contexts", "get_post_prompt", "get_prompt",
        "get_raw_prompt",
        "prompt_add_section", "prompt_add_subsection", "prompt_add_to_section",
        "prompt_has_section", "set_post_prompt", "set_prompt_pom",
        "set_prompt_text",
    ],
    ("signalwire.core.mixins.serverless_mixin", "ServerlessMixin"): [],
    ("signalwire.core.mixins.skill_mixin", "SkillMixin"): [
        "add_skill", "has_skill", "list_skills", "remove_skill",
    ],
    ("signalwire.core.mixins.state_mixin", "StateMixin"): [],
    ("signalwire.core.mixins.tool_mixin", "ToolMixin"): [
        "define_tool", "on_function_call", "register_swaig_function",
    ],
    ("signalwire.core.mixins.web_mixin", "WebMixin"): [
        "enable_debug_routes", "manual_set_proxy_url", "run", "serve",
        "set_dynamic_config_callback",
    ],
}


# ---------------------------------------------------------------------------
# Parsing
# ---------------------------------------------------------------------------

# C# file-scoped namespace: ``namespace SignalWire.Skills;``
FILE_NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][\w.]*)\s*;")
# C# block namespace: ``namespace SignalWire.Skills {``
BLOCK_NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][\w.]*)\s*\{")
# Class declaration:
#   public class Foo : Bar { ...
#   public sealed class Foo {
#   public abstract class Foo<T> {
#   public static class ReservedToolNames
CLASS_RE = re.compile(
    r"^\s*public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
    r"(?:class|struct|interface|record)\s+([A-Z][A-Za-z0-9_]*)"
)

# Public method or property declaration. We DON'T require the closing `)`
# to be on the same line as the header — many .NET methods wrap arguments
# across multiple lines. We only look for the opening `(`, optionally
# preceded by a generic parameter list `<...>`.
#
#   public void Foo(...)
#   public int Bar { get; }
#   public async Task<Foo> BazAsync(...)
#   public override SomeType Quux(...)
#   public virtual T1 Baz(...)
#   public static Foo Bar(...)
#   public DataMap Parameter(   <-- args start, continue on next line
METHOD_RE = re.compile(
    r"^\s*public\s+"
    # Optional modifiers between `public` and the return type
    r"(?:(?:override|virtual|static|async|sealed|new|extern|unsafe|readonly|partial)\s+)*"
    # Return type. Two shapes:
    #   plain identifier with generics/arrays/nullable, OR
    #   parenthesised tuple type like `(string User, string Password)`.
    r"(?:[A-Za-z_][\w<>?,.\[\] *&]*\s+|\([^)]+\)\s+)?"
    r"(?P<name>[A-Z][A-Za-z0-9_]*)"
    # Optional generic parameter list, then mandatory opening paren.
    r"(?:\s*<[^>]*>)?\s*\("
)

# Property declaration. Two shapes:
#   1. Block-bodied:        public string Foo { get; set; } = "x";
#   2. Expression-bodied:   public Fabric Fabric => _fabric ??= new Fabric(_http);
# Both terminate the property header before any `(`. We match the property
# name by requiring the line is NOT a method (no `(` before the property
# accessor).
PROPERTY_RE = re.compile(
    r"^\s*public\s+"
    r"(?:(?:override|virtual|static|new|sealed|readonly|required)\s+)*"
    r"[A-Za-z_][\w<>?,.\[\] *&]*\s+"
    r"(?P<name>[A-Z][A-Za-z0-9_]*)"
    # Three accepted shapes:
    #   { get; ... }                -- block-bodied (single line)
    #   => <expression>;            -- expression-bodied (single line)
    #   =>                           -- expression-bodied with body on next line
    #   { ... at EOL                -- block-bodied with body across lines
    r"\s*(?:\{[^}]*\}|=>\s*[^;]*;|=>\s*$|\{\s*$)"
)


def strip_block_comments(text: str) -> str:
    """Remove /* ... */ comments (possibly multi-line)."""
    out = []
    i = 0
    n = len(text)
    while i < n:
        if text[i:i + 2] == "/*":
            end = text.find("*/", i + 2)
            if end == -1:
                break
            block = text[i:end + 2]
            out.append("\n" * block.count("\n"))
            i = end + 2
        else:
            out.append(text[i])
            i += 1
    return "".join(out)


def strip_line_comments(line: str) -> str:
    """Remove // and /// comments outside string literals."""
    # Quick check first: lines starting with `///` are XML doc comments only.
    stripped = line.lstrip()
    if stripped.startswith("//"):
        return ""
    # Inline `//` outside strings.
    in_str = False
    in_char = False
    escape = False
    for i, c in enumerate(line):
        if escape:
            escape = False
            continue
        if c == '\\':
            escape = True
            continue
        if not in_char and c == '"':
            in_str = not in_str
        elif not in_str and c == "'":
            in_char = not in_char
        elif not in_str and not in_char and line[i:i + 2] == "//":
            return line[:i]
    return line


# ---------------------------------------------------------------------------
# Per-file parser
# ---------------------------------------------------------------------------

def parse_cs_file(path: Path) -> list[tuple[str, str, list[str]]]:
    """Return list of (namespace, class_name, public_member_names).

    Methods + properties are returned untranslated (PascalCase).
    """
    raw = path.read_text(encoding="utf-8", errors="replace")
    text = strip_block_comments(raw)

    namespace = ""
    # Stack of (kind, name, brace_depth_at_entry, visibility) — we track
    # current class for method assignment.
    scope_stack: list[tuple[str, str, int]] = []
    brace_depth = 0
    file_namespace_seen = False

    # class -> ordered list of member names
    members: dict[str, list[str]] = {}

    for raw_line in text.splitlines():
        line = strip_line_comments(raw_line)
        if not line.strip():
            continue

        # File-scoped namespace
        if not file_namespace_seen:
            m = FILE_NAMESPACE_RE.match(line)
            if m:
                namespace = m.group(1)
                file_namespace_seen = True
                continue

        # Block-scoped namespace
        m = BLOCK_NAMESPACE_RE.match(line)
        if m:
            namespace = m.group(1)
            scope_stack.append(("namespace", namespace, brace_depth))
            brace_depth += line.count("{") - line.count("}")
            continue

        # Class / struct / interface / record opener
        cls_m = CLASS_RE.match(line)
        if cls_m and "{" in line:
            class_name = cls_m.group(1)
            scope_stack.append(("class", class_name, brace_depth))
            brace_depth += line.count("{") - line.count("}")
            continue
        if cls_m and "{" not in line:
            # Class header on a line without `{` — happens with constraints
            # like `public class Foo<T> where T : new()`. Look ahead to next `{`.
            class_name = cls_m.group(1)
            scope_stack.append(("class", class_name, brace_depth))
            # Don't change brace_depth yet; the next `{` line will handle it.
            continue

        # Inside a class scope?
        current_class = None
        for kind, name, _depth in reversed(scope_stack):
            if kind == "class":
                current_class = name
                break

        if current_class is not None and brace_depth == _class_body_depth(scope_stack):
            # Try property first (single line with `{`)
            m = PROPERTY_RE.match(line)
            if m:
                name = m.group("name")
                if name not in SKIP_METHOD_NAMES and not name.startswith("_"):
                    members.setdefault(current_class, []).append(name)
                # Properties may close on the same line; update braces.
                brace_depth += line.count("{") - line.count("}")
                continue

            # Method declaration. Only count if the line contains a paren.
            if "(" in line:
                m = METHOD_RE.match(line)
                if m:
                    name = m.group("name")
                    if name not in SKIP_METHOD_NAMES and not name.startswith("_"):
                        # Constructor: name == class name
                        if name == current_class:
                            members.setdefault(current_class, []).append("__init__")
                        else:
                            members.setdefault(current_class, []).append(name)

        # Update brace tracking
        opens = line.count("{")
        closes = line.count("}")
        brace_depth += opens - closes

        # Pop scopes whose brace_depth has been exited
        while scope_stack and brace_depth <= scope_stack[-1][2]:
            scope_stack.pop()

    findings: list[tuple[str, str, list[str]]] = []
    for cls, names in members.items():
        # Dedup preserving order, then sort
        seen: list[str] = []
        seen_set: set[str] = set()
        for n in names:
            if n not in seen_set:
                seen.append(n)
                seen_set.add(n)
        findings.append((namespace, cls, sorted(seen)))
    return findings


def _class_body_depth(scope_stack: list[tuple[str, str, int]]) -> int:
    """Return brace depth one level inside the topmost class scope."""
    for kind, _name, depth in reversed(scope_stack):
        if kind == "class":
            return depth + 1
    return -1


# ---------------------------------------------------------------------------
# PascalCase -> snake_case translation
# ---------------------------------------------------------------------------

# Acronyms preserved as single units: HTTP -> http, LLM -> llm, SIP -> sip,
# SWML -> swml, SMS -> sms, TTS -> tts, SWAIG -> swaig, AI -> ai, MCP -> mcp,
# SIP -> sip, IVR -> ivr, JSON -> json, URL -> url, ID -> id.
def pascal_to_snake(name: str) -> str:
    if name == "__init__":
        return name
    # Drop trailing "Async" — the Python reference doesn't carry it.
    if name.endswith("Async") and len(name) > 5:
        name = name[:-5]
    # Insert _ before uppercase that follows lowercase or digit.
    s1 = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", name)
    # Insert _ before uppercase that's followed by a lowercase, when preceded
    # by another uppercase (e.g. "HTTPClient" -> "HTTP_Client").
    s2 = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", s1)
    out = s2.lower()
    return METHOD_RENAMES.get(out, out)


# ---------------------------------------------------------------------------
# Module mapping
# ---------------------------------------------------------------------------

def native_namespace_to_module(namespace: str) -> str:
    """``SignalWire.Rest.Namespaces`` -> ``signalwire.rest.namespaces``."""
    return namespace.lower()


def module_for_class(class_name: str, namespace: str) -> str | None:
    if class_name in CLASS_MODULE_MAP:
        return CLASS_MODULE_MAP[class_name]
    # Fall back to native translation, with class name snake_cased as the
    # final leaf so ``SignalWire.Rest.Namespaces.PhoneNumbers`` ->
    # ``signalwire.rest.namespaces.phone_numbers``.
    leaf = pascal_to_snake(class_name)
    base = native_namespace_to_module(namespace)
    return f"{base}.{leaf}" if base else f"signalwire.{leaf}"


def emit_class_name(class_name: str) -> str:
    return SKILL_RENAMES.get(class_name, class_name)


# ---------------------------------------------------------------------------
# Top-level
# ---------------------------------------------------------------------------

def git_sha(repo: Path) -> str:
    try:
        return subprocess.check_output(
            ["git", "-C", str(repo), "rev-parse", "HEAD"],
            stderr=subprocess.DEVNULL,
        ).decode().strip()
    except Exception:
        return "N/A"


def build_snapshot(repo: Path, src_dir: Path) -> dict:
    modules: dict[str, dict] = {}

    cs_files = sorted(src_dir.rglob("*.cs"))

    for path in cs_files:
        # Skip build artifacts
        rel = path.relative_to(repo).as_posix()
        if "/obj/" in rel or "/bin/" in rel:
            continue

        try:
            findings = parse_cs_file(path)
        except Exception as e:  # pragma: no cover
            print(f"warning: failed to parse {path}: {e}", file=sys.stderr)
            continue

        for namespace, class_name, methods in findings:
            # Apply CLASS_RENAME_MAP
            if (namespace, class_name) in CLASS_RENAME_MAP:
                target_mod, target_class = CLASS_RENAME_MAP[(namespace, class_name)]
            else:
                target_mod = module_for_class(class_name, namespace)
                target_class = emit_class_name(class_name)
            if target_mod is None:
                continue

            # Translate method names
            translated = sorted({pascal_to_snake(m) for m in methods})

            entry = modules.setdefault(target_mod, {"classes": {}, "functions": []})
            existing = entry["classes"].get(target_class, [])
            entry["classes"][target_class] = sorted(set(existing) | set(translated))

    # Mixin projections: replicate methods present on AgentBase under each
    # Python mixin module, then REMOVE them from AgentBase so the diff
    # against python_surface.json doesn't flag them as extras (Python keeps
    # them only on the mixin class).
    ab_module = modules.get("signalwire.core.agent_base", {})
    ab_methods = set(ab_module.get("classes", {}).get("AgentBase", []))
    projected: set[str] = set()
    for (mod, cls), expected_methods in MIXIN_PROJECTIONS.items():
        present = [m for m in expected_methods if m in ab_methods]
        entry = modules.setdefault(mod, {"classes": {}, "functions": []})
        entry["classes"][cls] = sorted(present)
        projected.update(present)
    if "signalwire.core.agent_base" in modules:
        ab_classes = modules["signalwire.core.agent_base"].get("classes", {})
        if "AgentBase" in ab_classes:
            ab_classes["AgentBase"] = sorted(
                set(ab_classes["AgentBase"]) - projected
            )

    # Sort module dict deterministically
    sorted_modules = {k: modules[k] for k in sorted(modules.keys())}

    # Drop empty modules
    sorted_modules = {
        k: v for k, v in sorted_modules.items()
        if v["classes"] or v["functions"]
    }

    return {
        "version": "1",
        "generated_from": f"signalwire-dotnet @ {git_sha(repo)}",
        "modules": sorted_modules,
    }


def main(argv: list[str]) -> int:
    repo = Path(__file__).resolve().parent.parent
    default_src = repo / "src" / "SignalWire"
    default_output = repo / "port_surface.json"

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--src-dir", type=Path, default=default_src,
        help=f"Source root to walk (default: {default_src})",
    )
    parser.add_argument(
        "--output", type=Path, default=default_output,
        help=f"Where to write JSON (default: {default_output})",
    )
    parser.add_argument(
        "--stdout", action="store_true",
        help="Print JSON to stdout instead of writing --output",
    )
    parser.add_argument(
        "--check", action="store_true",
        help="Compare against the file at --output; exit 1 on drift",
    )
    args = parser.parse_args(argv)

    if not args.src_dir.is_dir():
        print(f"error: src dir not found: {args.src_dir}", file=sys.stderr)
        return 1

    snapshot = build_snapshot(repo, args.src_dir)
    rendered = json.dumps(snapshot, indent=2, sort_keys=True) + "\n"

    if args.check:
        if not args.output.is_file():
            print(f"error: {args.output} does not exist", file=sys.stderr)
            return 1
        existing = args.output.read_text(encoding="utf-8")

        def strip_meta(s: str) -> str:
            obj = json.loads(s)
            obj.pop("generated_from", None)
            return json.dumps(obj, indent=2, sort_keys=True) + "\n"

        if strip_meta(rendered) != strip_meta(existing):
            print(
                "DRIFT: port_surface.json is stale relative to source.\n"
                "  Regenerate:\n"
                "    python3 scripts/enumerate_surface.py",
                file=sys.stderr,
            )
            return 1
        return 0

    if args.stdout:
        sys.stdout.write(rendered)
    else:
        args.output.write_text(rendered, encoding="utf-8")
        n_modules = len(snapshot["modules"])
        n_classes = sum(len(m["classes"]) for m in snapshot["modules"].values())
        n_methods = sum(
            sum(len(ms) for ms in m["classes"].values())
            for m in snapshot["modules"].values()
        )
        print(
            f"wrote {args.output} ({n_modules} modules, {n_classes} classes, {n_methods} methods)",
            file=sys.stderr,
        )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
