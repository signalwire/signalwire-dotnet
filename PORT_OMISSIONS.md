# PORT_OMISSIONS.md (signalwire-dotnet)

<!-- ══════════════════════════════════════════════════════════════════════════
BEFORE YOU ADD AN ENTRY TO THIS FILE — READ THIS.

Every entry here is a place the parity checker STOPS comparing. That is a real cost:
a divergence you list is a divergence no gate will ever catch again. So entries must
be RARE, and each one must earn its place. Default to skepticism: assume the entry is
NOT needed and make the case that it is.

The order of preference, always:
  1. FIX THE PORT so it matches the reference (add the missing member; make the
     signature match).
  2. FIX THE EMISSION so idiom folds onto the reference shape — the enumerator/emitter
     canonicalizes your language's spelling onto the oracle's (builder → __init__,
     getters → attributes, Result<T,E> → the plain return, CamelCase → the reference
     name, options-object/kwargs → the expanded param list, RAII/dispose → close).
     MOST divergences are idiom and belong here, not in this file.
  3. FIX THE REFERENCE if the oracle itself is wrong or stale (a Python-only symbol
     that leaked into the contract, a param the reference added and the oracle never
     re-enumerated). Fix Python / the oracle, then re-drift — do not paper over a
     broken reference with a per-port entry.
  4. Only when 1–3 genuinely cannot apply does an entry here become justified.

An entry is JUSTIFIED ONLY IF it is irreducible after correct emission — i.e. the
divergence survives because the two languages genuinely cannot express the same thing,
not because the emitter hasn't folded the idiom yet. If emission COULD fold it, the
entry is a bug in this file; go fix the emitter.

Each entry MUST state WHY, concretely, in one of these forms:
  • ADDITION — this symbol exists in the port but not the reference. Answer: is it
    genuine port-only surface with NO reference twin (say what it is and why the
    reference has no equivalent), or is it IDIOM the emitter should have folded (then
    it does not belong here — fold it)? A convenience/alias/back-compat wrapper is NOT
    a justification.
  • OMISSION — this reference symbol has no port member. Answer: WHY can it not exist
    here — what specific language feature is absent (e.g. no async-context-manager
    protocol, no __init__ method protocol)? "impossible:" means the construct cannot
    be expressed at all; if it merely LOOKS different, that's idiom → fold it, don't
    omit it. Cite a precedent when one exists (e.g. RelayClient omits the same dunder).
  • SIGNATURE — the symbol matches by name but its parameters differ. Answer: is the
    difference a foldable idiom collapse (options-object, leading context/self,
    builder) — then EXPAND it in the signature emitter so names+count match, don't list
    it — or a genuine reference-only parameter with no cross-language analogue?

If you cannot write a crisp, specific WHY that survives the "could emission fold this?"
test, the entry is not ready. Prove it's needed before you add it.
═══════════════════════════════════════════════════════════════════════════════ -->

Python symbols deliberately not implemented in this .NET port. Format:

```
<fully.qualified.symbol>: <one-sentence rationale>
```

`scripts/diff_port_surface.py` reads this file to know which Python
symbols to ignore when checking parity. Anything not in this file AND
not implemented in the port fails the diff.

The categories below summarise the major omission groups. Per-symbol
entries follow.

---

## Skip-list categories

These broad categories are Python-only per the SignalWire SDK skip
rules:

- **`signalwire.search.*`** — Vector / embedding indexing (Python ML
  stack: sentence-transformers, pgvector, faiss). Per the porting-sdk
  skip list, search is Python-only.
- **`signalwire.skills.native_vector_search.*`** — Local-mode indexing
  (SQLite/pgvector + sentence-transformers + FAISS) is the bulk of this
  skill and is not portable to the .NET BCL. The .NET port ships the
  remote-mode HTTP path (POSTs queries to a SignalWire search server)
  so agents that already use the centralised search service still work;
  configuring the skill without `remote_url` returns an explanatory
  error rather than an empty stub.
- **`signalwire.skills.<name>.skill_original` / `skill_improved`** —
  Python-experimental skill variants; .NET ships the canonical skill.
- **`signalwire.cli.build_search.*` / `dokku.*` / `init_project.*` /
  `swaig_test_wrapper.*` / `test_swaig.*` / `types.*` / `simulation.*` /
  `execution.*` / `output.*` / `core.*`** — Python-CLI internal
  scaffolding. .NET CLI is binary-based (`dotnet swaig-test`).
- **`signalwire.livewire.*`** — LiveWire integration is Python-only.
- **`signalwire.mcp_gateway.*`** and the `mcp_gateway` skill
  (`signalwire.skills.mcp_gateway.*`) — the standalone MCP gateway server
  AND the gateway skill are Python-only and not ported to any SDK (§I.1
  user ruling, 2026-07). The real `AgentBase.add_mcp_server` /
  `enable_mcp_server` oracle methods ARE implemented (distinct from the
  dropped gateway skill).
- **`signalwire.pom.pom_tool.*`** — Python CLI helper for rendering a
  POM file from disk; .NET ships POM in-process only.
  (`signalwire.pom.pom` itself IS implemented at
  `src/SignalWire/POM/PromptObjectModel.cs`.)
- **`signalwire.utils.schema_utils.*`** — .NET ships
  `SignalWire.SWML.Schema`, whose methods are projected onto the
  `signalwire.utils.schema_utils.SchemaUtils` reference surface by the
  surface enumerator (extra convenience accessors recorded in
  PORT_ADDITIONS.md).

(Note: `signalwire.web.web_service.WebService`,
`signalwire.utils.url_validator.validate_url`, and
`signalwire.agents.bedrock.BedrockAgent` are now IMPLEMENTED — item H/I —
and are no longer omitted.)

## Architecture omissions

These are deliberate architectural deltas:

- **`signalwire.core.swml_renderer`, `signalwire.core.swml_handler`,
  `signalwire.core.swml_builder`** — Internal Python rendering
  abstractions; .NET integrates renderers/handlers on `Service` directly
  and ships `Document` under `SignalWire.SWML.Document`.
- **`signalwire.core.config_loader`, `signalwire.core.security_config`,
  `signalwire.core.auth_handler`, `signalwire.core.logging_config`** —
  Internal Python helpers; .NET reads env vars directly, uses
  `CryptographicOperations.FixedTimeEquals`, and inlines auth/logging on
  the corresponding classes.
- **`signalwire.core.swaig_function`, `signalwire.core.pom_builder`,
  `signalwire.core.agent.prompt.*`, `signalwire.core.agent.tools.*`,
  `signalwire.core.mixins.*`** — Internal Python data classes /
  decorator scaffolding. .NET stores SWAIG funcs as
  `Dictionary<string, object>` and projects mixin methods onto AgentBase
  via `MIXIN_PROJECTIONS` in `scripts/enumerate_surface.py`.
- **`signalwire.relay.constants`, `signalwire.relay.event`,
  `signalwire.relay.action.*`, `signalwire.relay.commands`,
  `signalwire.relay.state`, `signalwire.relay.message_command`,
  `signalwire.relay.helpers`, `signalwire.relay.errors`** — Module-level
  Python helpers; .NET ships `SignalWire.Relay.Constants` static class,
  inlines command/state/error logic on Client/Call (recorded in
  PORT_ADDITIONS.md).
- **`signalwire.rest._base`, `signalwire.rest._pagination`,
  `signalwire.rest.types`, `signalwire.rest.api_resource`,
  `signalwire.rest.errors`, `signalwire.rest.serializers`,
  `signalwire.rest.fabric.*`,
  `signalwire.rest.namespaces.*`** — Internal Python REST scaffolding.
  .NET ships `HttpClient`, `CrudResource`, `SignalWireRestError` under
  `SignalWire.REST` and groups namespaces under
  `SignalWire.REST.Namespaces.*` (recorded in PORT_ADDITIONS.md).
- **`signalwire.run_agent`, `signalwire.start_agent`,
  `signalwire.RestClient`, `signalwire.add_skill_directory`,
  `signalwire.list_skills`, `signalwire.list_skills_with_params`,
  `signalwire.register_skill`** — Module-level Python convenience
  functions. .NET surfaces equivalent functionality via
  `AgentBase.Run()`, `AgentServer.Run()`, `RestClient` (in
  `SignalWire.REST`), and `SkillRegistry.Instance` methods.

(Per-symbol entries below — one line per Python symbol.)

---

signalwire.core.agent.tools.decorator.ToolDecorator.create_class_decorator: impossible: Python @tool class/instance decorator API relies on the decorator protocol; C# has no method-decorator feature — tools register via DefineTool directly (TS + PHP both omit this as impossible)
signalwire.core.agent.tools.decorator.ToolDecorator.create_instance_decorator: impossible: Python @tool class/instance decorator API relies on the decorator protocol; C# has no method-decorator feature — tools register via DefineTool directly (TS + PHP both omit this as impossible)
signalwire.core.agent.tools.decorator.ToolDecorator: impossible: Python @tool class/instance decorator API relies on the decorator protocol; C# has no method-decorator feature — tools register via DefineTool directly (TS + PHP both omit this as impossible)
signalwire.core.agent.tools.registry.ToolRegistry.register_class_decorated_tools: impossible: Python @tool class/instance decorator API relies on the decorator protocol; C# has no method-decorator feature — tools register via DefineTool directly (TS + PHP both omit this as impossible)
signalwire.core.auth_handler.AuthHandler.flask_decorator: impossible: framework-bound Flask decorator factory; C# ships webhook auth as ASP.NET middleware (a PORT_ADDITION) — the Flask-decorator FORM has no C# analog (TS/PHP omit likewise)
signalwire.core.auth_handler.AuthHandler.get_fastapi_dependency: impossible: framework-bound factory returning a FastAPI dependency; C# ships the equivalent as ASP.NET middleware (a PORT_ADDITION) — the FastAPI-dependency FORM has no C# analog (TS/PHP ship native middleware likewise)
signalwire.core.mixins.tool_mixin.ToolMixin.tool: impossible: Python @tool class/instance decorator API relies on the decorator protocol; C# has no method-decorator feature — tools register via DefineTool directly (TS + PHP both omit this as impossible)
signalwire.core.mixins.web_mixin.WebMixin.get_app: impossible: returns a FastAPI APIRouter / FastAPI app object; C# exposes HTTP via HttpListener/ASP.NET directly — there is no FastAPI-object analog to return (TS/PHP omit likewise)
signalwire.core.security.webhook_middleware.make_webhook_validation_dependency: impossible: framework-bound factory returning a FastAPI dependency; C# ships the equivalent as ASP.NET middleware (a PORT_ADDITION) — the FastAPI-dependency FORM has no C# analog (TS/PHP ship native middleware likewise)
signalwire.core.swml_builder.SWMLBuilder.__getattr__: impossible: Python __getattr__ dynamic-dispatch protocol (method_missing); C# has no such interception — SWML verbs are dispatched via explicit AddVerb (TS/PHP omit likewise)
signalwire.core.swml_service.SWMLService.__getattr__: impossible: Python __getattr__ dynamic-dispatch protocol (method_missing); C# has no such interception — SWML verbs are dispatched via explicit AddVerb (TS/PHP omit likewise)
signalwire.relay.client.RelayClient.__aenter__: impossible: Python async-context-manager protocol dunder; C# uses IAsyncDisposable / await using on the client instead (TS/PHP omit likewise)
signalwire.relay.client.RelayClient.__aexit__: impossible: Python async-context-manager protocol dunder; C# uses IAsyncDisposable / await using on the client instead (TS/PHP omit likewise)
signalwire.relay.client.RelayClient.__del__: impossible: Python finalizer dunder; C# uses IAsyncDisposable/Dispose deterministic cleanup instead (TS/PHP omit likewise)
signalwire.rest._pagination.PaginatedIterator.__iter__: impossible: Python iterator-protocol dunder; C# PaginatedIterator implements IAsyncEnumerable (await foreach) instead — no __iter__/__next__ equivalent (TS/PHP omit likewise)
signalwire.rest._pagination.PaginatedIterator.__next__: impossible: Python iterator-protocol dunder; C# PaginatedIterator implements IAsyncEnumerable (await foreach) instead — no __iter__/__next__ equivalent (TS/PHP omit likewise)
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.get_parameter_schema: Internal MCP gateway helpers; .NET inlines on McpGatewaySkill
signalwire.core.swml_service.SWMLService.schema_utils: approved: dotnet-no-public-schema-utils — VERIFIED not a rename: dotnet's SWMLService exposes NO public schema_utils/SchemaUtils member of any name; SignalWire.SWML.Schema is used internally. Python's @property has no accessor twin here. Not a language limit — pending API sign-off.
signalwire.core.swml_service.SWMLService.security: approved: dotnet-no-public-security — VERIFIED not a rename: dotnet's SWMLService exposes NO public security/Security member of any name; the SecurityConfig is a private field. Python's @property has no accessor twin here. Not a language limit — pending API sign-off.
signalwire.core.swml_service.SWMLService.verb_registry: approved: dotnet-no-public-verb-registry — VERIFIED not a rename: dotnet's SWMLService exposes NO public verb_registry/VerbRegistry member of any name (register_verb_handler/add_verb are methods, not the registry accessor). Python's @property has no accessor twin here. Not a language limit — pending API sign-off.

<!-- agentbase-family folded omissions (surface diff folds WebMixin.get_app /
     ToolMixin.tool onto the agentbase-family token; the per-class ToolMixin.tool
     and WebMixin.get_app keys above remain for the UNFOLDED signature gate). -->
agentbase-family.get_app: impossible: returns a FastAPI APIRouter / FastAPI app object; C# exposes HTTP via HttpListener/ASP.NET directly — there is no FastAPI-object analog to return (TS/PHP omit likewise). Folded onto the agentbase-family token on the surface.
agentbase-family.tool: impossible: Python @tool class/instance decorator API relies on the decorator protocol; C# has no method-decorator feature — tools register via DefineTool directly (TS + PHP both omit this as impossible). Folded onto the agentbase-family token on the surface.
signalwire.agent_server.AgentServer.agents: approved: dotnet-agents-dict-property — VERIFIED not a rename: the reference exposes BOTH `agents` (a dict<str,AgentBase> @property) AND `get_agents()`; dotnet ships get_agents() (which already maps 1:1 to the reference's own `get_agents` twin) but exposes NO `agents`/`Agents` dict property of any name. The dict-collection-property idiom is simply not exposed — renaming get_agents would collide with its own reference twin. Accessor-expressible via get_agents() — pending API sign-off.
signalwire.core.skill_manager.SkillManager.loaded_skills: approved: dotnet-loaded-skills-dict-property — VERIFIED not a rename: the reference exposes BOTH `loaded_skills` (a dict @property) AND `list_loaded_skills()`; dotnet ships list_loaded_skills() (maps 1:1 to the reference's own `list_loaded_skills` twin) but exposes NO `loaded_skills`/`LoadedSkills` dict property of any name. The dict-property idiom is not exposed — renaming would collide with its own twin. Accessor-expressible via list_loaded_skills() — pending API sign-off.
signalwire.web.web_service.WebService.security: approved: dotnet-webservice-no-public-security — VERIFIED not a rename: dotnet's WebService exposes NO public security/Security member of any name; the SecurityConfig is private (mirrors the SWMLService.security omission). Python's @property has no accessor twin here. Not a language limit — pending API sign-off.
