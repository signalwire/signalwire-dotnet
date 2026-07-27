# PORT_SIGNATURE_OMISSIONS.md (signalwire-dotnet)

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

Signature divergences between this .NET port and the Python reference,
documented per the audit-cleanup-sweep methodology
(`porting-sdk/AUDIT_DISCIPLINE.md`). Each entry records a divergence in
parameter list shape, return type, or parameter binding that is the
result of a deliberate .NET-idiomatic design choice rather than a
missing capability.

Format:

```
<fully.qualified.symbol>: <one-sentence rationale>
```

`scripts/diff_port_signatures.py` reads this file alongside
`PORT_OMISSIONS.md` (surface-level Python-only) and `PORT_ADDITIONS.md`
(surface-level .NET-only). Anything not in one of those three files
fails the diff.

## Divergence categories

The bulk of entries fall into these architectural buckets:

- **Fluent-builder return types.** .NET methods return `this` (Service /
  AgentBase / ContextBuilder / SWMLBuilder) for method chaining; Python
  returns void per imperative idiom. Both shapes describe the same
  callable contract — caller can ignore or chain. The diff already
  excuses single-class fluent voids; cross-class projections (Service
  methods projected to ToolMixin/PromptMixin paths) need explicit
  entries.
- **Options/Params data class pattern.** .NET ctors and many call-site
  methods take a single typed `Options` / `Params` / `Opts` data class
  collecting what Python takes as named keyword arguments. Same
  configurable fields, different parameter binding. Examples:
  `Context.AddStep(AddStepOptions)` ↔ Python `Context.add_step(name=...,
  text=..., ...)`; `Step.SetGatherInfo(GatherInfoOptions)` ↔ Python
  `Step.set_gather_info(...)`. (Constructor cases — `AgentBase(AgentOptions)`,
  `RelayClient(RelayClientOptions)` — are no longer listed in this file: the
  shared differ folds `__init__`-as-a-member while the reference publishes a
  `construction` entry for the class, and compares construction by name
  instead. See ALLOWLIST_DISCIPLINE §495.)
- **`extra` Dictionary parameter.** Relay `Call.*` action methods take
  a single `Dictionary<string,object>` extra to forward additional
  protocol args; Python explodes them into named keyword arguments.
  Equivalent shape, different binding.
- **`var_keyword` ↔ `optional<dict<string,object>>` mismatch.** Python
  `**kwargs` ↔ .NET `Dictionary<string,object>?` works automatically
  in `diff_port_signatures.py` for non-optional dicts, but
  `optional<dict<string,object>>` (nullable in C#) doesn't trigger
  the equivalence. CallingNamespace methods exhibit this.
- **Typed delegate ↔ bare Callable.** .NET event-handler registration
  takes a typed `Action<...>` / `Func<...>` delegate; Python uses bare
  `Callable` since duck-typing handles the signature check.
- **Tuple-union return types.** .NET overloads return either
  `(user,password)` or `(user,password,source)` as a union; Python
  uses an `include_source` kwarg to select. Same data shape with
  different signature shape.
- **Concrete Resource subclass returns.** .NET REST namespace
  accessors return the concrete `Resource` subclass; Python uses
  base `CrudResource` since dynamic typing makes the subclass
  irrelevant.

## Per-symbol entries

signalwire.agent_server.AgentServer.get_agents: .NET GetAgents returns list of (route, agent) tuples for full enumeration; Python returns just the route strings since registered agents are accessible via .agents dict
signalwire.agent_server.AgentServer.unregister: .NET Unregister returns AgentServer for fluent chaining; Python returns bool indicating whether the route was actually registered
signalwire.core.agent.prompt.manager.PromptManager.define_contexts: .NET DefineContexts returns ContextBuilder for fluent context construction; Python returns void and takes a contexts dict argument
signalwire.core.agent.tools.registry.ToolRegistry.define_tool: .NET fluent-builder pattern: returns Service (SWMLService) for chaining (service.DefineTool(...).RegisterSwaigFunction(...)); Python returns void
signalwire.core.agent.tools.registry.ToolRegistry.get_function: .NET ToolRegistry.GetFunction returns SWAIGFunction|Dict union (richer typed model); Python returns the bare dict
signalwire.core.agent_base.AgentBase.on_debug_event: .NET OnDebugEvent returns AgentBase for fluent chaining; Python returns the Callable (decorator pattern)
signalwire.core.agent_base.AgentBase.on_summary: .NET OnSummary registers a callback for the summary lifecycle; Python takes summary + raw_data as direct override-point parameters
signalwire.core.contexts.Context.add_enter_filler: .NET filler-list methods take a List<string>; Python uses *args (var_positional) for the same payload
signalwire.core.contexts.Context.add_exit_filler: .NET filler-list methods take a List<string>; Python uses *args (var_positional) for the same payload
signalwire.core.contexts.Context.add_step: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.contexts.ContextBuilder.validate: .NET Validate returns void after throwing on errors; Python returns a list-of-error-strings (different validation idiom: exceptions vs error-list)
signalwire.core.contexts.GatherInfo.add_question: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.contexts.Step.add_gather_question: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.contexts.Step.set_gather_info: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.data_map.DataMap.expression: .NET DataMap.Expression takes a string pattern; Python accepts re.Pattern or string interchangeably for compiled-regex reuse
signalwire.core.function_result.FunctionResult.remove_global_data: .NET overloads RemoveGlobalData as RemoveGlobalData(List<string>) AND RemoveGlobalData(string), matching Python's keys: Union[str, List[str]]; the adapter selects the List<string> overload as canonical (vs the reference union<string,list<string>>) - the str arm is the additive overload (PORT_ADDITIONS.md) and emits the bare-string action value, proven by the emission differ
signalwire.core.function_result.FunctionResult.remove_metadata: .NET overloads RemoveMetadata as RemoveMetadata(List<string>) AND RemoveMetadata(string), matching Python's keys: Union[str, List[str]]; the adapter selects the List<string> overload as canonical (vs the reference union<string,list<string>>) - the str arm is the additive overload (PORT_ADDITIONS.md) and emits the bare-string action value, proven by the emission differ
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_function_include: .NET AddFunctionInclude takes a single FunctionInclude object aggregating url/functions/meta_data; Python takes them individually
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.enable_debug_events: .NET EnableDebugEvents level takes string severity ("info","debug","trace"); Python uses int level for log-level integration
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.set_internal_fillers: .NET SetInternalFillers takes List<string>; Python takes Dict[str, Dict[str, List[str]]] - different data shape captured at the protocol-payload level
signalwire.core.mixins.auth_mixin.AuthMixin.get_basic_auth_credentials: .NET overload returns either (user,password) or (user,password,source) tuple union; Python single signature returns (user,password)
signalwire.core.mixins.prompt_mixin.PromptMixin.define_contexts: .NET PromptMixin methods return ContextBuilder for fluent chaining; Python uses a union return since the same method may return either depending on overload
signalwire.core.mixins.tool_mixin.ToolMixin.define_tool: .NET fluent-builder pattern: ToolMixin methods return AgentBase/Service for chaining; Python returns void
signalwire.core.mixins.web_mixin.WebMixin.on_swml_request: .NET on_swml_request callback signature is (request_data, callback_path); Python adds the raw FastAPI Request object as a third parameter
signalwire.core.mixins.web_mixin.WebMixin.register_routing_callback: .NET RegisterRoutingCallback takes (path, callback_fn) for the explicit pair binding; Python takes (callback_fn, path) - parameter order swap
signalwire.core.mixins.web_mixin.WebMixin.run: .NET Run() blocks on HttpListener with config baked into AgentOptions; Python takes event/context/force_mode/host/port to support both ASGI deploy and serverless
signalwire.core.skill_base.SkillBase.register_tools: .NET SkillBase override hooks take (agent, parameters) per the Wire(agent, params) lifecycle; Python uses self with agent set on construction
signalwire.core.skill_base.SkillBase.setup: .NET SkillBase override hooks take (agent, parameters) per the Wire(agent, params) lifecycle; Python uses self with agent set on construction
signalwire.core.skill_base.SkillBase.validate_env_vars: .NET ValidateEnvVars returns the list of missing env-var names; Python returns bool (whether all are present) - same intent, richer return shape
signalwire.core.skill_manager.SkillManager.load_skill: .NET LoadSkill takes (skill_name, parameters) and looks up skill_class via the SkillRegistry; Python takes skill_class as a third explicit argument
signalwire.core.swml_builder.SWMLBuilder.add_section: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.ai: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.answer: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.hangup: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.play: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.reset: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.say: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_service.SWMLService.get_basic_auth_credentials: .NET overload returns either (user,password) or (user,password,source) tuple union; Python single signature returns (user,password)
signalwire.core.swml_service.SWMLService.register_routing_callback: .NET RegisterRoutingCallback takes (path, callback_fn) for the explicit pair binding; Python takes (callback_fn, path) - parameter order swap
signalwire.pom.pom.PromptObjectModel.add_pom_as_subsection: .NET takes target as a string section title; Python accepts either a Section instance or a string for ergonomic resolution
signalwire.pom.pom.PromptObjectModel.add_section: .NET POM section methods take all named arguments positionally; Python keeps body/bullets/numbered/numberedBullets keyword-only
signalwire.pom.pom.PromptObjectModel.from_json: .NET FromJson/FromYaml accepts only string source; Python additionally accepts a pre-parsed dict for in-memory construction
signalwire.pom.pom.PromptObjectModel.from_yaml: .NET FromJson/FromYaml accepts only string source; Python additionally accepts a pre-parsed dict for in-memory construction
signalwire.pom.pom.Section.add_subsection: .NET POM section methods take all named arguments positionally; Python keeps body/bullets/numbered/numberedBullets keyword-only
signalwire.relay.call.Action.wait: .NET Wait takes timeout as int seconds; Python uses optional<float> for sub-second precision
signalwire.relay.call.Call.ai: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.ai_hold: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.ai_message: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.ai_unhold: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.amazon_bedrock: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.bind_digit: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.clear_digit_bindings: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.collect: .NET Call.Collect returns StandaloneCollectAction (specialized subclass for the collect verb); Python returns the generic CollectAction
signalwire.relay.call.Call.connect: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.detect: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.detect_answering_machine: typed convenience wrapper over Detect — .NET exposes the AMD params as ordinary C# optional parameters (positional kind); Python marks them keyword-only. Same param set + RELAY {type:"machine"} wire shape; onCompleted is C# Action<Action> vs Python's RelayEvent callback
signalwire.relay.call.Call.detect_digit: typed convenience wrapper over Detect — .NET exposes digits/timeout as ordinary C# optional parameters (positional kind); Python marks them keyword-only. Same param set + RELAY {type:"digit"} wire shape; onCompleted is C# Action<Action> vs Python's RelayEvent callback
signalwire.relay.call.Call.detect_fax: typed convenience wrapper over Detect — .NET exposes tone/timeout as ordinary C# optional parameters (positional kind); Python marks them keyword-only. Same param set + RELAY {type:"fax"} wire shape; onCompleted is C# Action<Action> vs Python's RelayEvent callback
signalwire.relay.call.Call.echo: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.join_conference: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.join_room: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.leave_conference: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.live_translate: .NET takes the required `action` dict + optional `statusUrl` as explicit named parameters (matches Python's `action` + `status_url` params + wire params.action/status_url wrap) plus a single Dictionary<string,object> `extra` to forward additional protocol args; Python explodes those into named keyword arguments
signalwire.relay.call.Call.on: .NET Call.On registers a typed callback for an event_type; Python takes (event_type, handler) - same shape with parameter rename
signalwire.relay.call.Call.pay: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.play: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.play_and_collect: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.play_audio: typed convenience wrapper over Play — .NET exposes url/volume as ordinary C# optional parameters (positional kind); Python marks them keyword-only. Same param set + RELAY {type:"audio"} wire shape; onCompleted is C# Action<Action> vs Python's RelayEvent callback
signalwire.relay.call.Call.play_ringtone: typed convenience wrapper over Play — .NET exposes name/duration/volume as ordinary C# parameters (positional kind); Python marks duration/volume keyword-only. Same param set + RELAY {type:"ringtone"} wire shape; onCompleted is C# Action<Action> vs Python's RelayEvent callback
signalwire.relay.call.Call.play_silence: typed convenience wrapper over Play — .NET exposes duration as the first positional parameter and onCompleted positional; Python marks onCompleted keyword-only. Same param set + RELAY {type:"silence"} wire shape; onCompleted is C# Action<Action> vs Python's RelayEvent callback
signalwire.relay.call.Call.play_tts: typed convenience wrapper over Play — .NET exposes text + language/gender/voice/volume as ordinary C# parameters (positional kind); Python marks the optionals keyword-only. Same param set + RELAY {type:"tts"} wire shape; onCompleted is C# Action<Action> vs Python's RelayEvent callback
signalwire.relay.call.Call.prompt_audio: typed convenience wrapper over PlayAndCollect — .NET exposes url/collect + volume as ordinary C# parameters (positional kind); Python marks volume keyword-only. Same param set + RELAY {type:"audio"} play media + collect; onCompleted is C# Action<Action> vs Python's RelayEvent callback
signalwire.relay.call.Call.prompt_tts: typed convenience wrapper over PlayAndCollect — .NET exposes text/collect + language/gender/voice/volume as ordinary C# parameters (positional kind); Python marks the optionals keyword-only. Same param set + RELAY {type:"tts"} play media + collect; onCompleted is C# Action<Action> vs Python's RelayEvent callback
signalwire.relay.call.Call.queue_enter: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.queue_leave: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.receive_fax: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.record: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.refer: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.send_digits: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.send_fax: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.stream: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.tap: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.transcribe: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.transfer: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.user_event: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.wait_for_answered: same (self, timeout) shape and short-circuit semantics as Python; return type differs only by the documented Event-vs-RelayEvent class rename (.NET ships Event in its own file; see PORT_ADDITIONS.md)
signalwire.relay.call.Call.wait_for_ending: same (self, timeout) shape and short-circuit semantics as Python; return type differs only by the documented Event-vs-RelayEvent class rename (.NET ships Event in its own file; see PORT_ADDITIONS.md)
signalwire.relay.call.Call.wait_for_ringing: same (self, timeout) shape and short-circuit semantics as Python; return type differs only by the documented Event-vs-RelayEvent class rename (.NET ships Event in its own file; see PORT_ADDITIONS.md)
signalwire.relay.call.CollectAction.start_input_timers: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.PlayAction.pause: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.PlayAction.resume: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.PlayAction.volume: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.RecordAction.pause: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.RecordAction.resume: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.CollectAction.pause: .NET CollectAction.Pause returns void since callers chain via the Action object state; Python returns dict (the protocol response from the pause subcommand)
signalwire.relay.call.CollectAction.resume: .NET CollectAction.Resume returns void since callers chain via the Action object state; Python returns dict (the protocol response from the resume subcommand)
signalwire.relay.client.RelayClient.dial: .NET RelayClient/Message methods take a Params data class; Python uses individual named keyword arguments
signalwire.relay.client.RelayClient.on_call: .NET RelayClient.OnCall/OnMessage returns a typed handler delegate (CallHandler/MessageHandler) for unsubscribe support; Python returns the RelayClient itself for fluent chaining
signalwire.relay.client.RelayClient.on_message: .NET RelayClient.OnCall/OnMessage returns a typed handler delegate (CallHandler/MessageHandler) for unsubscribe support; Python returns the RelayClient itself for fluent chaining
signalwire.relay.client.RelayClient.send_message: .NET RelayClient/Message methods take a Params data class; Python uses individual named keyword arguments
signalwire.relay.message.Message.on: .NET Message.On takes a typed Action<Message,Event> delegate; Python uses Callable
signalwire.relay.message.Message.result: .NET Message.Wait/Result returns RelayEvent for typed access to the resolution; Python returns the bare string outcome
signalwire.relay.message.Message.wait: .NET Message.Wait/Result returns RelayEvent for typed access to the resolution; Python returns the bare string outcome
signalwire.rest._base.CrudWithAddresses.list_addresses: .NET ListAddresses takes optional Dictionary<string,object> for query params; Python uses **kwargs
signalwire.skills.registry.SkillRegistry.list_skills: .NET SkillRegistry.ListSkills returns a plain list of skill names (List<string>); Python's list_skills returns the richer list<dict<string,string>> skill-info inventory
signalwire.skills.registry.SkillRegistry.discover_skills: .NET SkillRegistry.DiscoverSkills returns List<string> of skill names (mirrors ListSkills); Python's discover_skills returns the same list<dict<string,string>> inventory as list_skills
signalwire.skills.registry.SkillRegistry.register_skill: .NET RegisterSkill takes (name, factory) for explicit factory registration; Python takes the skill_class and infers metadata via attributes
signalwire.relay.call.CollectAction.volume: .NET CollectAction.Volume returns void since callers chain via the Action object state; Python returns dict (the protocol response from the volume subcommand)

## Item H/I subsystems — cross-language idiom + reference-oracle gaps

The ~30 subsystems added this turn (AgentServer, BedrockAgent, WebService,
AuthHandler, SkillManager, DataMap module functions, LoggingConfig, the RELAY
event classes, etc.) reconcile by name/module in `enumerate_signatures.py`
(mirroring `enumerate_surface.py`). The residual per-symbol divergences below
are genuine cross-language idiom (options/data-object ctors, fluent returns,
typed-vs-`any` params, C# static factories) or griffe reference-oracle gaps
(the SIGNATURE oracle records no signature for a dynamically-defined class or a
subclass's inherited/overridden method, though the SURFACE oracle does). Each
is the same shape already excused above for the pre-existing surface.

signalwire.agent_server.AgentServer.agents: .NET exposes the registered agents as GetAgents()/typed accessors rather than a public ``agents`` dict property; the same registry is reachable, only not as a bare attribute (missing-port is the attribute form, not a capability gap)
signalwire.agent_server.AgentServer.register_global_routing_callback: .NET RegisterGlobalRoutingCallback takes a single callback delegate; Python takes (callback_fn, path) — the path is bound at registration time on the .NET side
signalwire.agent_server.AgentServer.run: .NET Run(host, port) blocks on the HTTP host with deploy mode baked into config; Python takes event/context to additionally support ASGI + serverless entrypoints
signalwire.core.agent.tools.inferred_schema.InferredSchema.__init__: .NET-only value type backing the type-inference layer; neither the SIGNATURE nor SURFACE oracle records InferredSchema — the C# record ctor is an internal construction helper (a port addition of the typed-handler machinery)
signalwire.core.agent.tools.inferred_schema.InferredSchema.deconstruct: .NET-only record Deconstruct() auto-generated by the C# compiler for the InferredSchema value type; no Python counterpart (port-side language construct)
signalwire.core.auth_handler.AuthHandler.verify_basic_auth: .NET VerifyBasicAuth takes (username, password) as the timing-safe comparison inputs; Python takes a single FastAPI HTTPBasicCredentials object it unpacks internally — same credential check, different binding
signalwire.core.auth_handler.AuthHandler.verify_bearer_token: .NET VerifyBearerToken takes the token as a bare string; Python takes a FastAPI HTTPAuthorizationCredentials object — the .NET layer is framework-agnostic so the raw token is passed directly
signalwire.core.contexts.create_simple_context: .NET ContextBuilder.CreateSimpleContext returns the ContextBuilder for fluent step construction; Python's module-level create_simple_context returns a bare Context — same entry point, fluent-vs-value return idiom
signalwire.core.data_map.create_expression_tool: .NET DataMap.CreateExpressionTool is a fluent builder taking (name, purpose, expressions) and returning the DataMap config dict; Python's module free function takes (name, patterns, parameters) and returns a DataMap — same expression-tool construction, different builder param binding + fluent-vs-value return
signalwire.core.data_map.create_simple_api_tool: .NET DataMap.CreateSimpleApiTool is a fluent builder taking (name, purpose, parameters, method, url, output, headers) and returning the DataMap config dict; Python's free function takes (name, url, response_template, parameters, method, headers, body, error_keys) returning a DataMap — same simple-API-tool construction, different param binding + fluent-vs-value return
signalwire.core.data_map.DataMap.fallback_output: .NET FallbackOutput takes the result as the C# FunctionResult builder concretely; the parity adapter records it as ``any`` because the argument is the same FunctionResult the reference types — the wire body (fallback output block) is identical
signalwire.core.data_map.DataMap.output: .NET Output takes the result as the C# FunctionResult builder concretely; the parity adapter records it as ``any`` because the argument is the same FunctionResult the reference types — the wire body (output block) is identical
signalwire.core.logging_config.strip_control_chars: .NET StripControlChars is a single-string sanitiser (takes the event_dict payload); Python's structlog processor signature is (logger, method_name, event_dict) per the structlog processor protocol — the .NET logging stack is not structlog so only the payload arg is meaningful
signalwire.core.mixins.tool_mixin.ToolMixin.define_tools: .NET DefineTools returns Service (SWMLService) for fluent chaining and takes the tool_defs list to register; Python's define_tools is a zero-arg getter returning the registered SWAIGFunction/dict list — .NET splits the register (DefineTools) from the getter, both present, mapped to the one reference name
signalwire.core.mixins.web_mixin.WebMixin.enable_debug_routes: .NET EnableDebugRoutes returns AgentBase for fluent chaining; Python's WebMixin.enable_debug_routes returns the SWMLService — both return the composed service object for chaining, differing only by the .NET AgentBase-vs-Service class at the same node
signalwire.core.mixins.web_mixin.WebMixin.serve: .NET Serve() blocks on the HttpListener with host/port baked into AgentOptions; Python takes (host, port) as call-site overrides — same serve entry point, config-object-vs-args binding
signalwire.core.security.security_utils.filter_sensitive_headers: .NET FilterSensitiveHeaders takes an optional Dictionary<string,string> and returns the redacted Dictionary<string,string>; Python's griffe records a TypeVar-parameterised dict<string,_V> — same header-redaction over a string-keyed/string-valued map, the _V is griffe's generic placeholder
signalwire.core.skill_base.SkillBase.define_tool: .NET SkillBase.DefineTool takes (name, description, parameters, handler) as the explicit tool-definition params; Python's define_tool takes **kwargs it forwards — same tool registration, explicit-params-vs-kwargs binding
signalwire.core.skill_manager.SkillManager.loaded_skills: .NET SkillManager exposes the loaded skills via ListSkills()/GetSkill() rather than a bare ``loaded_skills`` dict property; the same loaded-skill set is reachable, only not as a public attribute (missing-port is the attribute form, not a capability gap)
signalwire.core.swaig_function.SWAIGFunction.execute: .NET Execute types raw_data as the concrete SwaigRequest payload class; Python types it as an optional dict — the .NET port is MORE strongly typed (the SwaigRequest is the typed form of that same dict), not a capability gap
signalwire.core.swml_handler.AIVerbHandler.validate_config: .NET AIVerbHandler.ValidateConfig is a port-side pre-render validation helper on the verb handler; the SIGNATURE oracle records no such method on the reference AIVerbHandler (which validates lazily at render) — a port addition of an explicit validation hook
signalwire.core.swml_service.SWMLService.serve: .NET Serve() blocks on the HttpListener with host/port/ssl/domain baked into the SWMLService config object; Python takes (host, port, ssl_cert, ssl_key, ssl_enabled, domain) as call-site args — same serve entry point, config-object-vs-args binding
signalwire.register_skill: .NET signalwire.RegisterSkill takes (name, factory) for explicit factory registration; Python's module-level register_skill takes the skill_class and infers metadata via class attributes — same registration, factory-vs-class binding
signalwire.relay.call.Call.wait_for: .NET Call.WaitFor takes (state, timeout) to await a call-state transition; Python takes (event_type, predicate, timeout) for the general event-await — .NET specialises to the state case; return differs only by the documented Event-vs-RelayEvent class rename (see PORT_ADDITIONS.md)
signalwire.relay.call.Call.wait_for_ended: same (self, timeout) shape and short-circuit semantics as Python; return type differs only by the documented Event-vs-RelayEvent class rename (.NET ships Event in its own file; see PORT_ADDITIONS.md)
signalwire.relay.call.StandaloneCollectAction.start_input_timers: .NET StartInputTimers returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state — same subcommand, richer return
signalwire.rest._base.HttpClient.post: .NET HttpClient.Post takes the params body as an ordinary object it JSON-serialises; the parity adapter records it as ``any`` because the argument accepts the same arbitrary JSON body the reference types as an optional dict — same POST body, port accepts an open object
signalwire.skills.datetime.skill.DateTimeSkill.register_tools: griffe reference-oracle gap — the SIGNATURE oracle records no inherited/overridden methods on the DateTimeSkill subclass (only its own get_tools), while the SURFACE oracle records register_tools as reference surface; the .NET skill overrides it via SkillBase inheritance
signalwire.skills.datetime.skill.DateTimeSkill.setup: griffe reference-oracle gap — the SIGNATURE oracle records no inherited/overridden methods on the DateTimeSkill subclass (only its own get_tools), while the SURFACE oracle records setup as reference surface; the .NET skill overrides it via SkillBase inheritance
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.register_tools: .NET SkillBase override hooks take (agent, parameters) per the Wire(agent, params) lifecycle; Python uses self with agent set on construction — same register-tools hook, receiver-vs-explicit-agent/params idiom
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.setup: .NET SkillBase override hooks take (agent, parameters) per the Wire(agent, params) lifecycle; Python uses self with agent set on construction — same setup hook, receiver-vs-explicit-agent/params idiom
signalwire.skills.registry.SkillRegistry.get_skill_class: .NET SkillRegistry.GetFactory returns the skill factory delegate (Func returning a SkillBase) which the adapter maps to the reference get_skill_class; Python returns the SkillBase class directly — same lookup, factory-delegate-vs-class return
signalwire.skills.registry.SkillRegistry.list_all_skill_sources: .NET ListAllSkillSources returns a flat dict<string,string> (skill -> source path); Python returns dict<string,list<string>> (skill -> list of source paths) — same source inventory, single-vs-multi source-path shape
signalwire.web.web_service.WebService.app: .NET WebService has no public ``app`` ASGI-application property (the .NET service hosts via HttpListener, not an ASGI app object); Python exposes the FastAPI app — framework-specific attribute with no .NET counterpart
signalwire.web.web_service.WebService.security: .NET WebService has no public ``security`` HTTPBearer property (auth is applied via the timing-safe AuthHandler, not a FastAPI security dependency object); Python exposes the FastAPI security scheme — framework-specific attribute with no .NET counterpart
signalwire.web.web_service.WebService.start: .NET Start(host, port) returns the bound int port for the caller and omits Python's ssl_cert/ssl_key args (TLS is configured on the HttpListener via the options); same start entry point, richer return + config-object TLS binding

signalwire.agent_server.AgentServer.app: ADDITION-inverse (missing-port) — Python's AgentServer.app returns the framework-specific fastapi.FastAPI ASGI application object; the .NET AgentServer hosts the multi-agent server on a System.Net.HttpListener (see Server/AgentServer.cs Run()), so there is no ASGI app object to expose. Framework-specific attribute with no .NET counterpart. Direct precedent: signalwire.web.web_service.WebService.app (same FastAPI-app-vs-HttpListener omission, already listed).
signalwire.swml.verb_info.VerbInfo.__init__: ADDITION — VerbInfo is a port-only C# positional record (Swml/Schema.cs: `record VerbInfo(string Name, string SchemaName, JsonElement Definition)`), an internal SWML-schema helper holding a verb's name/schema-name/definition triple. The reference has no signalwire.swml.verb_info module or VerbInfo class at all. The 3-param `__init__` is the record's compiler-synthesized primary constructor; it has no reference twin because the class itself is port-internal. (The class's scalar accessors name/schema_name/definition are already excused as port-side state accessors.)
signalwire.swml.verb_info.VerbInfo.deconstruct: ADDITION — same port-only VerbInfo record as above; `deconstruct` is the C# compiler-synthesized Deconstruct method every positional record emits for positional pattern-matching/tuple-destructuring. Pure record-idiom member on a port-internal class with no reference equivalent.
