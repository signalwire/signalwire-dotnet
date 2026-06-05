# PORT_SIGNATURE_OMISSIONS.md (signalwire-dotnet)

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
  `AgentBase(AgentOptions)` ↔ Python `AgentBase(name=..., route=...,
  ...)`; `RelayClient(RelayClientOptions)` ↔ Python `RelayClient(
  project=..., token=..., ...)`.
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
signalwire.agent_server.AgentServer.logger: .NET .Logger property returns the SignalWire.Logging.Logger class instance; Python reference adapter resolves logger to get_logger() which has a different class:path
signalwire.agent_server.AgentServer.unregister: .NET Unregister returns AgentServer for fluent chaining; Python returns bool indicating whether the route was actually registered
signalwire.core.agent.prompt.manager.PromptManager.define_contexts: .NET DefineContexts returns ContextBuilder for fluent context construction; Python returns void and takes a contexts dict argument
signalwire.core.agent.prompt.manager.PromptManager.prompt_add_section: .NET fluent-builder pattern: returns AgentBase for chaining (agent.SetPromptText("..").SetPostPrompt("..")); Python returns void per imperative idiom
signalwire.core.agent.prompt.manager.PromptManager.prompt_add_subsection: .NET fluent-builder pattern: returns AgentBase for chaining (agent.SetPromptText("..").SetPostPrompt("..")); Python returns void per imperative idiom
signalwire.core.agent.prompt.manager.PromptManager.prompt_add_to_section: .NET fluent-builder pattern: returns AgentBase for chaining (agent.SetPromptText("..").SetPostPrompt("..")); Python returns void per imperative idiom
signalwire.core.agent.prompt.manager.PromptManager.set_post_prompt: .NET fluent-builder pattern: returns AgentBase for chaining (agent.SetPromptText("..").SetPostPrompt("..")); Python returns void per imperative idiom
signalwire.core.agent.prompt.manager.PromptManager.set_prompt_pom: .NET fluent-builder pattern: returns AgentBase for chaining (agent.SetPromptText("..").SetPostPrompt("..")); Python returns void per imperative idiom
signalwire.core.agent.prompt.manager.PromptManager.set_prompt_text: .NET fluent-builder pattern: returns AgentBase for chaining (agent.SetPromptText("..").SetPostPrompt("..")); Python returns void per imperative idiom
signalwire.core.agent.tools.registry.ToolRegistry.define_tool: .NET fluent-builder pattern: returns Service (SWMLService) for chaining (service.DefineTool(...).RegisterSwaigFunction(...)); Python returns void
signalwire.core.agent.tools.registry.ToolRegistry.get_function: .NET ToolRegistry.GetFunction returns SWAIGFunction|Dict union (richer typed model); Python returns the bare dict
signalwire.core.agent.tools.registry.ToolRegistry.register_swaig_function: .NET fluent-builder pattern: returns Service (SWMLService) for chaining (service.DefineTool(...).RegisterSwaigFunction(...)); Python returns void
signalwire.core.agent_base.AgentBase.__init__: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.agent_base.AgentBase.on_debug_event: .NET OnDebugEvent returns AgentBase for fluent chaining; Python returns the Callable (decorator pattern)
signalwire.core.agent_base.AgentBase.on_summary: .NET OnSummary registers a callback for the summary lifecycle; Python takes summary + raw_data as direct override-point parameters
signalwire.core.agent_base.AgentBase.pom: .NET .Pom property returns the typed PromptObjectModel; Python .pom returns the raw list-of-dicts representation
signalwire.core.contexts.Context.add_enter_filler: .NET filler-list methods take a List<string>; Python uses *args (var_positional) for the same payload
signalwire.core.contexts.Context.add_exit_filler: .NET filler-list methods take a List<string>; Python uses *args (var_positional) for the same payload
signalwire.core.contexts.Context.add_step: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.contexts.ContextBuilder.__init__: .NET ContextBuilder is constructed from PromptMixin.DefineContexts() and binds the agent automatically; Python passes agent explicitly
signalwire.core.contexts.ContextBuilder.validate: .NET Validate returns void after throwing on errors; Python returns a list-of-error-strings (different validation idiom: exceptions vs error-list)
signalwire.core.contexts.GatherInfo.add_question: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.contexts.GatherQuestion.__init__: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.contexts.Step.add_gather_question: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.contexts.Step.set_gather_info: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.data_map.DataMap.expression: .NET DataMap.Expression takes a string pattern; Python accepts re.Pattern or string interchangeably for compiled-regex reuse
signalwire.core.function_result.FunctionResult.remove_global_data: .NET overloads RemoveGlobalData as RemoveGlobalData(List<string>) AND RemoveGlobalData(string), matching Python's keys: Union[str, List[str]]; the adapter selects the List<string> overload as canonical (vs the reference union<string,list<string>>) - the str arm is the additive overload (PORT_ADDITIONS.md) and emits the bare-string action value, proven by the emission differ
signalwire.core.function_result.FunctionResult.remove_metadata: .NET overloads RemoveMetadata as RemoveMetadata(List<string>) AND RemoveMetadata(string), matching Python's keys: Union[str, List[str]]; the adapter selects the List<string> overload as canonical (vs the reference union<string,list<string>>) - the str arm is the additive overload (PORT_ADDITIONS.md) and emits the bare-string action value, proven by the emission differ
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_function_include: .NET AddFunctionInclude takes a single FunctionInclude object aggregating url/functions/meta_data; Python takes them individually
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_language: .NET AddLanguage exposes name/code/voice/params (4 most-used parameters); engine/model/fillers go via SetParam - full parity captured at Service.SetParam
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_pattern_hint: .NET AddPatternHint exposes pattern/hint via positional args; replace/ignore_case go via SetParam variants
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_pronunciation: .NET ignore_case takes string ("yes"/"no"); Python uses bool - semantic divergence from older protocol versions
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.enable_debug_events: .NET EnableDebugEvents level takes string severity ("info","debug","trace"); Python uses int level for log-level integration
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.set_internal_fillers: .NET SetInternalFillers takes List<string>; Python takes Dict[str, Dict[str, List[str]]] - different data shape captured at the protocol-payload level
signalwire.core.mixins.auth_mixin.AuthMixin.get_basic_auth_credentials: .NET overload returns either (user,password) or (user,password,source) tuple union; Python single signature returns (user,password)
signalwire.core.mixins.prompt_mixin.PromptMixin.define_contexts: .NET PromptMixin methods return ContextBuilder for fluent chaining; Python uses a union return since the same method may return either depending on overload
signalwire.core.mixins.tool_mixin.ToolMixin.define_tool: .NET fluent-builder pattern: ToolMixin methods return AgentBase/Service for chaining; Python returns void
signalwire.core.mixins.tool_mixin.ToolMixin.register_swaig_function: .NET fluent-builder pattern: ToolMixin methods return AgentBase/Service for chaining; Python returns void
signalwire.core.mixins.web_mixin.WebMixin.on_swml_request: .NET on_swml_request callback signature is (request_data, callback_path); Python adds the raw FastAPI Request object as a third parameter
signalwire.core.mixins.web_mixin.WebMixin.register_routing_callback: .NET RegisterRoutingCallback takes (path, callback_fn) for the explicit pair binding; Python takes (callback_fn, path) - parameter order swap
signalwire.core.mixins.web_mixin.WebMixin.run: .NET Run() blocks on HttpListener with config baked into AgentOptions; Python takes event/context/force_mode/host/port to support both ASGI deploy and serverless
signalwire.core.pom_builder.PomBuilder.from_sections: .NET PomBuilder.FromSections is a static method (no cls receiver); Python uses @classmethod which adds cls to the signature
signalwire.core.security.session_manager.SessionManager.__init__: .NET SessionManager ctor binds the secret_key from configuration internally; Python takes secret_key as a parameter
signalwire.core.skill_base.SkillBase.register_tools: .NET SkillBase override hooks take (agent, parameters) per the Wire(agent, params) lifecycle; Python uses self with agent set on construction
signalwire.core.skill_base.SkillBase.setup: .NET SkillBase override hooks take (agent, parameters) per the Wire(agent, params) lifecycle; Python uses self with agent set on construction
signalwire.core.skill_base.SkillBase.validate_env_vars: .NET ValidateEnvVars returns the list of missing env-var names; Python returns bool (whether all are present) - same intent, richer return shape
signalwire.core.skill_manager.SkillManager.load_skill: .NET LoadSkill takes (skill_name, parameters) and looks up skill_class via the SkillRegistry; Python takes skill_class as a third explicit argument
signalwire.core.skill_manager.SkillManager.logger: .NET .Logger property returns the SignalWire.Logging.Logger class instance; Python reference adapter resolves logger to get_logger() which has a different class:path
signalwire.core.swml_builder.SWMLBuilder.add_section: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.ai: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.answer: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.hangup: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.play: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.reset: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_builder.SWMLBuilder.say: .NET SWMLBuilder fluent methods return SWMLBuilder for chaining; Python types this as Self via typing.Self
signalwire.core.swml_service.SWMLService.__init__: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.core.swml_service.SWMLService.get_basic_auth_credentials: .NET overload returns either (user,password) or (user,password,source) tuple union; Python single signature returns (user,password)
signalwire.core.swml_service.SWMLService.register_routing_callback: .NET RegisterRoutingCallback takes (path, callback_fn) for the explicit pair binding; Python takes (callback_fn, path) - parameter order swap
signalwire.pom.pom.PromptObjectModel.add_pom_as_subsection: .NET takes target as a string section title; Python accepts either a Section instance or a string for ergonomic resolution
signalwire.pom.pom.PromptObjectModel.add_section: .NET POM section methods take all named arguments positionally; Python keeps body/bullets/numbered/numberedBullets keyword-only
signalwire.pom.pom.PromptObjectModel.from_json: .NET FromJson/FromYaml accepts only string source; Python additionally accepts a pre-parsed dict for in-memory construction
signalwire.pom.pom.PromptObjectModel.from_yaml: .NET FromJson/FromYaml accepts only string source; Python additionally accepts a pre-parsed dict for in-memory construction
signalwire.pom.pom.Section.__init__: .NET POM section methods take all named arguments positionally; Python keeps body/bullets/numbered/numberedBullets keyword-only
signalwire.pom.pom.Section.add_subsection: .NET POM section methods take all named arguments positionally; Python keeps body/bullets/numbered/numberedBullets keyword-only
signalwire.pom.pom.Section.render_markdown: .NET render methods take section_number as a delimiter string; Python takes a list of integers for nested numbering
signalwire.pom.pom.Section.render_xml: .NET render methods take section_number as a delimiter string; Python takes a list of integers for nested numbering
signalwire.prefabs.concierge.ConciergeAgent.__init__: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.prefabs.faq_bot.FAQBotAgent.__init__: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.prefabs.info_gatherer.InfoGathererAgent.__init__: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.prefabs.receptionist.ReceptionistAgent.__init__: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.prefabs.survey.SurveyAgent.__init__: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.relay.call.AIAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.call.Action.__init__: .NET Action ctor takes call as a string control_id; Python takes the full Call object reference
signalwire.relay.call.Action.wait: .NET Wait takes timeout as int seconds; Python uses optional<float> for sub-second precision
signalwire.relay.call.Call.__init__: .NET RelayClient/Message methods take a Params data class; Python uses individual named keyword arguments
signalwire.relay.call.Call.ai: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.ai_hold: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.ai_message: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.ai_unhold: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.amazon_bedrock: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.answer: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
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
signalwire.relay.call.Call.leave_room: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.live_transcribe: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
signalwire.relay.call.Call.live_translate: .NET Call action methods take a single Dictionary<string,object> extra to forward additional protocol args; Python explodes them into named keyword arguments
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
signalwire.relay.call.CollectAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.call.CollectAction.start_input_timers: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.DetectAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.call.FaxAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.call.PayAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.call.PlayAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.call.PlayAction.pause: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.PlayAction.resume: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.PlayAction.volume: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.RecordAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.call.RecordAction.pause: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.RecordAction.resume: .NET action subcommand returns the protocol response Dictionary for inspection; Python returns void since callers chain via the Action object state
signalwire.relay.call.StreamAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.call.TapAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.call.TranscribeAction.__init__: .NET Action subclass ctors take (control_id, call_id, node_id, client) for direct routing; Python passes (call, control_id) and pulls the rest from the Call object
signalwire.relay.client.RelayClient.__init__: .NET options-object pattern: a typed Options/Params data class collects what Python takes as named keyword arguments - same captured fields, different parameter binding
signalwire.relay.client.RelayClient.dial: .NET RelayClient/Message methods take a Params data class; Python uses individual named keyword arguments
signalwire.relay.client.RelayClient.on_call: .NET RelayClient.OnCall/OnMessage returns a typed handler delegate (CallHandler/MessageHandler) for unsubscribe support; Python returns the RelayClient itself for fluent chaining
signalwire.relay.client.RelayClient.on_message: .NET RelayClient.OnCall/OnMessage returns a typed handler delegate (CallHandler/MessageHandler) for unsubscribe support; Python returns the RelayClient itself for fluent chaining
signalwire.relay.client.RelayClient.send_message: .NET RelayClient/Message methods take a Params data class; Python uses individual named keyword arguments
signalwire.relay.message.Message.__init__: .NET RelayClient/Message methods take a Params data class; Python uses individual named keyword arguments
signalwire.relay.message.Message.on: .NET Message.On takes a typed Action<Message,Event> delegate; Python uses Callable
signalwire.relay.message.Message.result: .NET Message.Wait/Result returns RelayEvent for typed access to the resolution; Python returns the bare string outcome
signalwire.relay.message.Message.wait: .NET Message.Wait/Result returns RelayEvent for typed access to the resolution; Python returns the bare string outcome
signalwire.rest._base.CrudWithAddresses.list_addresses: .NET ListAddresses takes optional Dictionary<string,object> for query params; Python uses **kwargs
signalwire.rest.client.RestClient.addresses: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.chat: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.compat: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.datasphere: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.imported_numbers: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.logs: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.lookup: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.mfa: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.number_groups: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.phone_numbers: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.project: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.pubsub: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.queues: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.recordings: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.registry: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.short_codes: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.sip_profile: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.verified_callers: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.client.RestClient.video: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.calling.CallingNamespace.__init__: .NET CallingNamespace ctor takes (client, project_id) for explicit dependency injection; Python takes a pre-bound http_client
signalwire.rest.namespaces.calling.CallingNamespace.ai_hold: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.ai_message: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.ai_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.ai_unhold: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.collect: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.collect_start_input_timers: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.collect_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.denoise: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.denoise_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.detect: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.detect_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.dial: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.disconnect: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.end: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.live_transcribe: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.live_translate: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.play: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.play_pause: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.play_resume: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.play_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.play_volume: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.receive_fax_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.record: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.record_pause: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.record_resume: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.record_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.refer: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.send_fax_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.stream: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.stream_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.tap: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.tap_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.transcribe: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.transcribe_stop: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.transfer: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.calling.CallingNamespace.user_event: .NET CallingNamespace methods take an optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.fabric.FabricNamespace.addresses: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.ai_agents: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.call_flows: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.conference_rooms: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.cxml_applications: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.cxml_scripts: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.cxml_webhooks: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.freeswitch_connectors: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.relay_applications: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.resources: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.sip_endpoints: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.sip_gateways: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.subscribers: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.swml_scripts: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.swml_webhooks: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.rest.namespaces.fabric.FabricNamespace.tokens: .NET REST namespace accessors return the concrete Resource subclass; Python uses base CrudResource since type inference is dynamic
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.__init__: .NET skill ctors are parameterless (Wire(agent, params) sets state post-construction); Python skills take agent and params via __init__
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.__init__: .NET skill ctors are parameterless (Wire(agent, params) sets state post-construction); Python skills take agent and params via __init__
signalwire.skills.registry.SkillRegistry.list_skills: .NET SkillRegistry.ListSkills returns a plain list of skill names (List<string>); Python's list_skills returns the richer list<dict<string,string>> skill-info inventory
signalwire.skills.registry.SkillRegistry.discover_skills: .NET SkillRegistry.DiscoverSkills returns List<string> of skill names (mirrors ListSkills); Python's discover_skills returns the same list<dict<string,string>> inventory as list_skills
signalwire.skills.registry.SkillRegistry.logger: .NET .Logger property returns the SignalWire.Logging.Logger class instance; Python reference adapter resolves logger to get_logger() which has a different class:path
signalwire.skills.registry.SkillRegistry.register_skill: .NET RegisterSkill takes (name, factory) for explicit factory registration; Python takes the skill_class and infers metadata via attributes
signalwire.skills.spider.skill.SpiderSkill.__init__: .NET skill ctors are parameterless (Wire(agent, params) sets state post-construction); Python skills take agent and params via __init__
signalwire.skills.weather_api.skill.WeatherApiSkill.__init__: .NET skill ctors are parameterless (Wire(agent, params) sets state post-construction); Python skills take agent and params via __init__
signalwire.rest.namespaces.calling.CallingNamespace.update: .NET CallingNamespace.UpdateAsync (Python-parity alias for UpdateCallAsync) takes optional Dictionary<string,object> for protocol overflow params; Python uses **kwargs - same shape with optional<> wrapper preventing the diff built-in var_keyword/dict equivalence
signalwire.rest.namespaces.compat.CompatAccounts.list: .NET CompatAccounts.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params - same shape, var_keyword vs positional dict equivalence not auto-detected
signalwire.rest.namespaces.compat.CompatConferences.list: .NET CompatConferences.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatConferences.list_participants: .NET CompatConferences.ListParticipantsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatConferences.list_recordings: .NET CompatConferences.ListRecordingsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatFaxes.list_media: .NET CompatFaxes.ListMediaAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatMessages.list_media: .NET CompatMessages.ListMediaAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatPhoneNumbers.list: .NET CompatPhoneNumbers.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatPhoneNumbers.list_available_countries: .NET ListAvailableCountriesAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatPhoneNumbers.search_local: .NET SearchLocalAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatPhoneNumbers.search_toll_free: .NET SearchTollFreeAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatQueues.list_members: .NET CompatQueues.ListMembersAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatRecordings.list: .NET CompatRecordings.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.compat.CompatTranscriptions.list: .NET CompatTranscriptions.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.datasphere.DatasphereDocuments.list_chunks: .NET DatasphereDocuments.ListChunksAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.fabric.CallFlowsResource.list_addresses: .NET CallFlowsHelper.ListAddressesAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.fabric.CallFlowsResource.list_versions: .NET CallFlowsHelper.ListVersionsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.fabric.ConferenceRoomsResource.list_addresses: .NET ConferenceRoomsHelper.ListAddressesAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.fabric.CxmlApplicationsResource.create: .NET CxmlApplicationsHelper.CreateAsync takes optional Dictionary<string,object>; Python uses **kwargs (both raise NotImplementedError; the parameter shape is irrelevant since the call is intercepted before any wire activity)
signalwire.rest.namespaces.fabric.FabricAddresses.list: .NET FabricAddresses.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.fabric.GenericResources.list: .NET FabricResources.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.fabric.GenericResources.list_addresses: .NET FabricResources.ListAddressesAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.fabric.SubscribersResource.list_sip_endpoints: .NET SubscribersHelper.ListSipEndpointsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.logs.ConferenceLogs.list: .NET ConferenceLogs.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.logs.FaxLogs.list: .NET FaxLogs.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.logs.MessageLogs.list: .NET MessageLogs.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.logs.VoiceLogs.list: .NET VoiceLogs.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.logs.VoiceLogs.list_events: .NET VoiceLogs.ListEventsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.number_groups.NumberGroupsResource.list_memberships: .NET NumberGroups.ListMembershipsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.queues.QueuesResource.list_members: .NET Queues.ListMembersAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.registry.RegistryBrands.list: .NET RegistryBrands.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.registry.RegistryBrands.list_campaigns: .NET RegistryBrands.ListCampaignsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.registry.RegistryCampaigns.list_numbers: .NET RegistryCampaigns.ListNumbersAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.registry.RegistryCampaigns.list_orders: .NET RegistryCampaigns.ListOrdersAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.video.VideoConferences.list_conference_tokens: .NET VideoConferences.ListConferenceTokensAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.video.VideoConferences.list_streams: .NET VideoConferences.ListStreamsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.video.VideoRoomRecordings.list: .NET VideoRoomRecordings.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.video.VideoRoomRecordings.list_events: .NET VideoRoomRecordings.ListEventsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.video.VideoRoomSessions.list: .NET VideoRoomSessions.ListAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.video.VideoRoomSessions.list_events: .NET VideoRoomSessions.ListEventsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.video.VideoRoomSessions.list_members: .NET VideoRoomSessions.ListMembersAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.video.VideoRoomSessions.list_recordings: .NET VideoRoomSessions.ListRecordingsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.rest.namespaces.video.VideoRooms.list_streams: .NET VideoRooms.ListStreamsAsync takes optional Dictionary<string,string> for query params; Python uses **params
signalwire.relay.call.CollectAction.volume: .NET CollectAction.Volume returns void since callers chain via the Action object state; Python returns dict (the protocol response from the volume subcommand)
