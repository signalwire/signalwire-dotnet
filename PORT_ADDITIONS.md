# PORT_ADDITIONS.md (signalwire-dotnet)

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

Symbols this .NET port exposes that have no Python-reference counterpart.
Each line is a deliberate addition with one-sentence rationale.

```
<fully.qualified.symbol>: <one-sentence rationale>
```

`scripts/diff_port_surface.py` reads this file alongside
`PORT_OMISSIONS.md`. Anything not in either file fails the diff.

## Categories of port-only symbols

The bulk of additions fall into these architectural buckets:

- **`signalwire.agent.agent_options.AgentOptions` /
  `signalwire.swml.service_options.ServiceOptions`** — .NET options
  data classes with init-only properties. Python uses kwargs to
  `AgentBase.__init__` / `SWMLService.__init__`.
- **`signalwire.swml.document.Document` /
  `signalwire.swml.schema.Schema`** — .NET ships these as classes;
  Python uses helpers under `signalwire.core.swml_builder` and
  `signalwire.utils.schema_utils`.
- **`signalwire.relay.<action_name>_action.<Action>Class`** — .NET ships
  each Action subclass in its own file under `SignalWire.Relay`; Python
  groups them all in `signalwire.relay.call`.
- **`signalwire.skills.<name>.<name>_skill.<Class>`** — .NET path
  translation puts each skill class file alongside its name; Python
  nests under `skills.<name>.skill`.
- **`signalwire.rest.crud_resource.CrudResource` /
  `http_client.HttpClient` / `rest_client.RestClient` /
  `signal_wire_rest_error.SignalWireRestError`** — .NET ships REST
  primitives directly under `SignalWire.REST`; Python's path is
  `signalwire.rest._base` / `signalwire.rest.client`.
- **`signalwire.rest.namespaces.*`** — .NET groups REST namespaces under
  `SignalWire.REST.Namespaces.*`; Python uses `signalwire.rest.<ns>`.
- **`signalwire.serverless.adapter.Adapter`** — .NET ships an explicit
  `Adapter` class; Python integrates equivalent logic into
  `ServerlessMixin` on AgentBase.
- **`signalwire.logging.logger.Logger`** — .NET ships `Logger` as a
  class with named factory; Python uses module-level functions under
  `signalwire.core.logging_config`.

## Per-symbol additions

signalwire.relay.client_options.ClientOptions: .NET options data class with init-only properties (6.2: replaced the string-keyed Dictionary ctor); Python uses kwargs to RelayClient.__init__
signalwire.relay.client_options.ClientOptions.project: .NET options data class with init-only properties; Python uses kwargs to RelayClient.__init__
signalwire.relay.client_options.ClientOptions.token: .NET options data class with init-only properties; Python uses kwargs to RelayClient.__init__
signalwire.relay.client_options.ClientOptions.host: .NET options data class with init-only properties; Python uses kwargs to RelayClient.__init__
signalwire.relay.client_options.ClientOptions.scheme: .NET options data class with init-only properties; Python uses kwargs to RelayClient.__init__
signalwire.relay.client_options.ClientOptions.contexts: .NET options data class with init-only properties; Python uses kwargs to RelayClient.__init__
signalwire.relay.client_options.ClientOptions.max_active_calls: .NET options data class with init-only properties; Python uses kwargs to RelayClient.__init__ (max_active_calls)
signalwire.signal_wire_options.SignalWireOptions: .NET DI options data class for the AddSignalWire() IServiceCollection registration (6.2); Python has no host-framework DI layer
signalwire.signal_wire_options.SignalWireOptions.project_id: .NET DI options data class for AddSignalWire(); Python has no host-framework DI layer
signalwire.signal_wire_options.SignalWireOptions.token: .NET DI options data class for AddSignalWire(); Python has no host-framework DI layer
signalwire.signal_wire_options.SignalWireOptions.space: .NET DI options data class for AddSignalWire(); Python has no host-framework DI layer
signalwire.signal_wire_options.SignalWireOptions.request_options: .NET DI options data class for AddSignalWire(); Python has no host-framework DI layer
signalwire.signal_wire_service_collection_extensions.SignalWireServiceCollectionExtensions: .NET IServiceCollection extension host for AddSignalWire() (6.2 DI idiom, BCL-conventional Microsoft.Extensions.DependencyInjection namespace); Python has no host-framework DI layer
signalwire.signal_wire_service_collection_extensions.SignalWireServiceCollectionExtensions.add_signal_wire: .NET AddSignalWire() DI registration (6.2) — RestClient singleton with IHttpClientFactory-sourced transport; Python has no host-framework DI layer
signalwire.agent_server.AgentServer.get_sip_username_mapping: Public helper added in .NET; Python equivalents are register_global_routing_callback / serve_static_files
signalwire.agent_server.AgentServer.handle_request: Public dispatch entry point on AgentServer; Python uses Flask/FastAPI request objects directly
signalwire.agent_server.AgentServer.host: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.agent_server.AgentServer.is_sip_routing_enabled: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.agent_server.AgentServer.port: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.agent_server.AgentServer.serve_static: Public helper added in .NET; Python equivalents are register_global_routing_callback / serve_static_files
signalwire.core.agent_base.AgentBase.build_ai_verb: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.agent_base.AgentBase.clone_for_request: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.agent_base.AgentBase.handle_request: .NET AgentBase overrides Service.handle_request (spliced to the SWMLService reference signature); the reference records handle_request only on SWMLService, so the AgentBase override needs this addition entry to excuse the signature-side missing-reference
signalwire.core.agent_base.AgentBase.is_webhook_signature_validation_enabled: .NET surfaces a public read-only flag for whether SigningKey is configured; Python users check `bool(agent.signing_key)` directly
signalwire.core.agent_base.AgentBase.signing_key: Public read-only property exposing the configured Signing Key; Python sets it as an attribute (porting-sdk/webhooks.md AgentBase integration)
signalwire.core.agent_base.AgentBase.get_skill_manager: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.agent_base.AgentBase.render_swml: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.agent_base.AgentBase.render_swml_with_context: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.contexts.ContextBuilder.attach_tool_name_supplier: Public helper added in .NET; Python ships equivalents at module level
signalwire.core.contexts.ContextBuilder.has_contexts: Public helper added in .NET; Python ships equivalents at module level
signalwire.core.contexts.Context.get_initial_step: Public helper added in .NET; Python ships equivalents at module level
signalwire.core.contexts.Context.get_step_order: Public helper added in .NET; Python ships equivalents at module level
signalwire.core.contexts.Context.get_steps: Public helper added in .NET; Python ships equivalents at module level
signalwire.core.contexts.Context.get_valid_contexts: Public helper added in .NET; Python ships equivalents at module level
signalwire.core.contexts.Context.name: Public read-only property surface; Python @property accessor with the same name
signalwire.core.contexts.GatherInfo.completion_action: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.contexts.GatherInfo.questions: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.contexts.GatherQuestion.key: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.contexts.Step.gather_info_data: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.contexts.Step.name: Public read-only property surface; Python @property accessor with the same name
signalwire.core.contexts.Step.valid_contexts: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.contexts.Step.valid_steps: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.security.webhook_middleware.WebhookValidationMiddleware.__init__: .NET ships the webhook signature validation as a constructable middleware class wrapping (signing_key, trust_proxy); Python uses a make_webhook_validation_dependency factory function that returns a FastAPI dependency callable
signalwire.core.security.webhook_middleware.WebhookValidationMiddleware.validate: .NET middleware exposes Validate(method, path, headers, body) returning a (status, headers, body) tuple to short-circuit HttpListener dispatch; Python's FastAPI dependency raises HTTPException(403) instead
signalwire.core.security.webhook_middleware.WebhookValidationMiddleware.extract_signature_header: Public static helper for pulling X-SignalWire-Signature / X-Twilio-Signature alias from a header dict; Python inlines this in webhook_middleware._extract_signature_header (private helper)
signalwire.core.security.webhook_middleware.WebhookValidationMiddleware.reconstruct_url: Public method exposing the URL-reconstruction logic (SWML_PROXY_URL_BASE / X-Forwarded-* / Host fallback); Python keeps this as a private _reconstruct_url helper
signalwire.core.skill_base.SkillBase.description: Public abstract/virtual properties on SkillBase; Python uses class-level constants instead
signalwire.core.skill_base.SkillBase.name: Public read-only property surface; Python @property accessor with the same name
signalwire.core.skill_base.SkillBase.required_env_vars: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.skill_base.SkillBase.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.skill_base.SkillBase.version: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.skill_base.SkillBase.wire: Public abstract/virtual properties on SkillBase; Python uses class-level constants instead
signalwire.logging.logger.Logger.debug: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.error: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.info: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.level: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.name: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.should_log: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.suppressed: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.warn: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.prefabs.concierge.ConciergeAgent.get_amenities: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.prefabs.concierge.ConciergeAgent.get_services: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.prefabs.concierge.ConciergeAgent.get_venue_name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.prefabs.faq_bot.FAQBotAgent.get_faqs: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.prefabs.faq_bot.FAQBotAgent.get_suggest_related: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.prefabs.info_gatherer.InfoGathererAgent.get_questions: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.prefabs.receptionist.ReceptionistAgent.get_departments: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.prefabs.receptionist.ReceptionistAgent.get_greeting: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.prefabs.survey.SurveyAgent.get_survey_name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.prefabs.survey.SurveyAgent.get_survey_questions: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.relay.call.Call.actions: Public Call method ported from Python's signalwire.relay.call.Call (.NET preserves the same surface name)
signalwire.relay.call.Call.call_id: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.call.Call.client: Public Call method ported from Python's signalwire.relay.call.Call (.NET preserves the same surface name)
signalwire.relay.call.Call.context: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.call.Call.device: Public Call method ported from Python's signalwire.relay.call.Call (.NET preserves the same surface name)
signalwire.relay.call.Call.dial_winner: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.call.Call.dispatch_event: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.call.Call.end_reason: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.call.Call.node_id: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.call.Call.on_event_callbacks: Public Call method ported from Python's signalwire.relay.call.Call (.NET preserves the same surface name)
signalwire.relay.call.Call.peer: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.call.Call.resolve_all_actions: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.call.Call.state: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.call.Call.tag: Public Call surface; Python ships these as @property accessors or methods under different names
signalwire.relay.client.RelayClient.agent: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.authenticate: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.authorization_state: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.build_web_socket_uri: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.calls: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.connected: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.contexts: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.get_call: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.handle_event: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.handle_message: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.host: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.messages: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.on_call_handler: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.on_event_handler: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.on_message_handler: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.project: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.read_loop: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.read_once: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.reconnect: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.scheme: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.send_ack: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.send: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.token: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.event.Event: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.message.Message.body: Public read-only property surface; Python @property accessor with the same name
signalwire.relay.message.Message.completed: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.relay.message.Message.context: Public read-only property surface; Python @property accessor with the same name
signalwire.relay.message.Message.direction: Public read-only property surface; Python @property accessor with the same name
signalwire.relay.message.Message.dispatch_event: Message helpers ported from Python; Python uses equivalent semantics under different method names
signalwire.relay.message.Message.from_number: Public read-only property surface; Python @property accessor with the same name
signalwire.relay.message.Message.media: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.relay.message.Message.message_id: Public read-only property surface; Python @property accessor with the same name
signalwire.relay.message.Message.on_completed: Message helpers ported from Python; Python uses equivalent semantics under different method names
signalwire.relay.message.Message.reason: Public read-only property surface; Python @property accessor with the same name
signalwire.relay.message.Message.resolve: Message helpers ported from Python; Python uses equivalent semantics under different method names
signalwire.relay.message.Message.state: Public read-only property surface; Python @property accessor with the same name
signalwire.relay.message.Message.tags: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.relay.message.Message.to_number: Public read-only property surface; Python @property accessor with the same name
signalwire.relay.client.RelayError.code: .NET exposes the RELAY error code as a read-only `Code` property; Python sets it as the `self.code` instance attribute in `RelayError.__init__` (which the surface enumerator records only as `__init__`) — same error code, .NET property vs Python instance attr
signalwire.serverless.adapter.Adapter.detect: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter.handle_azure: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter.handle_cgi: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter.handle_google_cloud_function: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter.handle_lambda: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter.serve: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.get_prompt_sections: Skill subclass overrides (Python ships the same overrides; the .NET file path differs from Python's nested module structure)
signalwire.skills.custom_skills.skill.CustomSkillsSkill: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.custom_skills.skill.CustomSkillsSkill.register_tools: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.custom_skills.skill.CustomSkillsSkill.setup: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.info_gatherer.skill.InfoGathererSkill.get_prompt_sections: Skill subclass overrides (Python ships the same overrides; the .NET file path differs from Python's nested module structure)
signalwire.skills.registry.SkillRegistry.reset: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.skill_name_extensions.SkillNameExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the SkillName closed-set enum; .NET-only typed helper, no Python reference equivalent (Python uses bare str skill names).
signalwire.skills.skill_name_extensions.SkillNameExtensions.to_wire_name: dotnet_enum_idiom: maps the typed SkillName closed-set enum member to its canonical snake_case wire name; AddSkill/RemoveSkill/HasSkill expose a SkillName overload next to the string overload so built-in skill names are typo-checked at compile time, with the string path preserved for parity (Python uses bare str) and custom skills.
signalwire.swaig.callback_method_extensions.CallbackMethodExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the CallbackMethod closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates join_conference's status_callback_method / recording_status_callback_method str against {GET,POST}).
signalwire.swaig.callback_method_extensions.CallbackMethodExtensions.to_wire_name: dotnet_enum_idiom: maps the typed CallbackMethod closed-set enum member ({GET,POST} — the set the Python reference validates join_conference's callback-method args against) to its canonical wire value; JoinConferenceOptions exposes CallbackMethod fields next to the flat string overload so the HTTP verb is typo-checked at compile time, with the string path preserved for parity (Python takes bare str).
signalwire.swaig.codec_extensions.CodecExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the Codec closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates tap's codec str against {PCMU,PCMA} — the 2-value SWAIG-tap set, distinct from the larger RELAY connect/stream codec superset).
signalwire.swaig.codec_extensions.CodecExtensions.to_wire_name: dotnet_enum_idiom: maps the typed Codec closed-set enum member ({PCMU,PCMA} — the set the Python reference validates tap's codec arg against) to its canonical upper-case wire value; the full-arity Tap(string uri, string controlId, TapDirection direction, Codec codec, int rtpPtime, string? statusUrl) overload (direction/codec typed, Python parameter order) is the canonical/audited signature — the reference now emits enum<PCMA,PCMU> for codec, so the audit requires a typed port form — and a same-arity bare-string Tap overload is preserved as a .NET-only parity escape hatch (Python takes bare str; identical SWML via a shared core). Deliberately distinct from the RELAY connect/stream codec superset, which stays a string.
signalwire.swaig.conference_beep_extensions.ConferenceBeepExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the ConferenceBeep closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates join_conference's beep str against {true,false,onEnter,onExit}).
signalwire.swaig.conference_beep_extensions.ConferenceBeepExtensions.to_wire_name: dotnet_enum_idiom: maps the typed ConferenceBeep closed-set enum member ({true,false,onEnter,onExit} — the set the Python reference validates join_conference's beep arg against) to its canonical wire value; the JoinConference(name, JoinConferenceOptions) overload exposes ConferenceBeep next to the flat string overload so the beep value is typo-checked at compile time, with the string path preserved for parity (Python takes bare str).
signalwire.swaig.conference_record_extensions.ConferenceRecordExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the ConferenceRecord closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates join_conference's record str against {do-not-record,record-from-start}).
signalwire.swaig.conference_record_extensions.ConferenceRecordExtensions.to_wire_name: dotnet_enum_idiom: maps the typed ConferenceRecord closed-set enum member ({do-not-record,record-from-start} — the set the Python reference validates join_conference's record arg against) to its canonical wire value; the JoinConference(name, JoinConferenceOptions) overload exposes ConferenceRecord next to the flat string overload so the record mode is typo-checked at compile time, with the string path preserved for parity (Python takes bare str).
signalwire.swaig.conference_trim_extensions.ConferenceTrimExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the ConferenceTrim closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates join_conference's trim str against {trim-silence,do-not-trim}).
signalwire.swaig.conference_trim_extensions.ConferenceTrimExtensions.to_wire_name: dotnet_enum_idiom: maps the typed ConferenceTrim closed-set enum member ({trim-silence,do-not-trim} — the set the Python reference validates join_conference's trim arg against) to its canonical wire value; the JoinConference(name, JoinConferenceOptions) overload exposes ConferenceTrim next to the flat string overload so the trim mode is typo-checked at compile time, with the string path preserved for parity (Python takes bare str).
signalwire.swaig.join_conference_options.JoinConferenceOptions: dotnet_options_object: .NET-idiomatic typed options bag for the JoinConference(name, JoinConferenceOptions) convenience overload; mirrors the Python reference's 18 optional join_conference params one-for-one, surfacing the four closed-set args as the ConferenceBeep/ConferenceRecord/ConferenceTrim/CallbackMethod enums. The flat all-string overload remains the parity-bearing signature; this record is a .NET convenience (one object vs 18 positional args), no Python reference equivalent.
signalwire.swaig.join_conference_options.JoinConferenceOptions.beep: dotnet_options_object: typed ConferenceBeep field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor); the parity-bearing flat JoinConference overload carries beep as a bare str matching Python.
signalwire.swaig.join_conference_options.JoinConferenceOptions.coach: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference coach param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.end_on_exit: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference end_on_exit param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.max_participants: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference max_participants param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.muted: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference muted param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.record: dotnet_options_object: typed ConferenceRecord field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor); the parity-bearing flat JoinConference overload carries record as a bare str matching Python.
signalwire.swaig.join_conference_options.JoinConferenceOptions.recording_status_callback: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference recording_status_callback param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.recording_status_callback_event: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference recording_status_callback_event param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.recording_status_callback_method: dotnet_options_object: typed CallbackMethod field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor); the parity-bearing flat JoinConference overload carries recording_status_callback_method as a bare str matching Python.
signalwire.swaig.join_conference_options.JoinConferenceOptions.region: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference region param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.result: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference result param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.start_on_enter: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference start_on_enter param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.status_callback: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference status_callback param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.status_callback_event: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference status_callback_event param; parity is carried by the flat JoinConference overload.
signalwire.swaig.join_conference_options.JoinConferenceOptions.status_callback_method: dotnet_options_object: typed CallbackMethod field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor); the parity-bearing flat JoinConference overload carries status_callback_method as a bare str matching Python.
signalwire.swaig.join_conference_options.JoinConferenceOptions.trim: dotnet_options_object: typed ConferenceTrim field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor); the parity-bearing flat JoinConference overload carries trim as a bare str matching Python.
signalwire.swaig.join_conference_options.JoinConferenceOptions.wait_url: dotnet_options_object: field on the .NET-only JoinConferenceOptions bag (surfaces as a property accessor) mirroring the Python join_conference wait_url param; parity is carried by the flat JoinConference overload.
signalwire.swaig.parameter_schema.ParameterSchema: dotnet_param_builder: fluent, type-safe builder for a SWAIG tool's parameters — produces the byte-identical JSON-Schema properties Dictionary that DefineTool already takes by hand; a typed convenience over the SAME wire output, not a new format. No Python reference equivalent (Python hand-writes the dict); the untyped Dictionary<string,object> path is unchanged, so this is purely additive ergonomics.
signalwire.swaig.parameter_schema.ParameterSchema.create: dotnet_param_builder: static factory starting a new empty ParameterSchema builder (ParameterSchema.Create()); .NET-only entry point, no Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.string: dotnet_param_builder: adds a JSON-Schema "string" property (description/required/default/format/enum optional); emits the same {"type":"string",...} property dict a developer writes by hand. No Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.number: dotnet_param_builder: adds a JSON-Schema "number" property; emits the same {"type":"number",...} property dict written by hand today. No Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.integer: dotnet_param_builder: adds a JSON-Schema "integer" property; emits the same {"type":"integer",...} property dict written by hand today. No Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.boolean: dotnet_param_builder: adds a JSON-Schema "boolean" property; emits the same {"type":"boolean",...} property dict written by hand today. No Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.enum: dotnet_param_builder: adds a "string" property constrained to a closed set; the Type overload sources the set from a Tier-1 enum (RecordFormat/RecordDirection/TapDirection/Codec) via its ToWireName, emitting schema enum:[wire-names], integrating the typed enums instead of re-typing the list at the call site (an IEnumerable<string> overload covers ad-hoc sets). No Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.array: dotnet_param_builder: adds an "array" property whose items are a scalar kind or a nested ParameterSchema; emits {"type":"array","items":{...}} identical to the hand-written form. No Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.object: dotnet_param_builder: adds a nested "object" property described by a child ParameterSchema; emits {"type":"object","properties":{...}} identical to the hand-written form. No Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.required: dotnet_param_builder: marks declared properties required by setting the inline ["required"]=true flag this port's hand-written tool params already use (MathSkill/SpiderSkill/InfoGathererSkill); byte-identical to that convention. No Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.required_names: dotnet_param_builder: read-only ordered list of required-property names for callers that additionally want the top-level JSON-Schema required:[...] array (the shape DataMap and Python's SWAIGFunction emit). No Python counterpart.
signalwire.swaig.parameter_schema.ParameterSchema.build: dotnet_param_builder: returns the finished JSON-Schema properties Dictionary<string,object> — the exact value passed as the parameters arg to DefineTool — as a fresh deep copy per call. No Python counterpart.
signalwire.swaig.record_direction_extensions.RecordDirectionExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the RecordDirection closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates record_call's direction str against {speak,listen,both}).
signalwire.swaig.record_direction_extensions.RecordDirectionExtensions.to_wire_name: dotnet_enum_idiom: maps the typed RecordDirection closed-set enum member ({speak,listen,both} — the set the Python reference validates record_call's direction arg against) to its canonical wire value; the full-arity RecordCall(..., RecordFormat format, RecordDirection direction, ...) overload (format/direction typed, Python parameter order) is the canonical/audited signature — the reference now emits enum<both,listen,speak> for direction, so the audit requires a typed port form — and a same-arity bare-string RecordCall overload is preserved as a .NET-only parity escape hatch (Python takes bare str; identical SWML via a shared core). The string overload is selected only when format/direction are passed as strings.
signalwire.swaig.record_format_extensions.RecordFormatExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the RecordFormat closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates record_call's format str against {wav,mp3,mp4}).
signalwire.swaig.record_format_extensions.RecordFormatExtensions.to_wire_name: dotnet_enum_idiom: maps the typed RecordFormat closed-set enum member ({wav,mp3,mp4} — the set the Python reference validates record_call's format arg against) to its canonical wire value; the full-arity RecordCall(..., RecordFormat format, RecordDirection direction, ...) overload (format/direction typed, Python parameter order) is the canonical/audited signature — the reference now emits enum<mp3,mp4,wav> for format, so the audit requires a typed port form — and a same-arity bare-string RecordCall overload is preserved as a .NET-only parity escape hatch (Python takes bare str; identical SWML via a shared core). The string overload is selected only when format/direction are passed as strings.
signalwire.swaig.tap_direction_extensions.TapDirectionExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the TapDirection closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates tap's direction str against {speak,hear,both} — note tap uses hear where record_call uses listen, so this is distinct from RecordDirection).
signalwire.swaig.tap_direction_extensions.TapDirectionExtensions.to_wire_name: dotnet_enum_idiom: maps the typed TapDirection closed-set enum member ({speak,hear,both} — the set the Python reference validates tap's direction arg against) to its canonical wire value; the full-arity Tap(string uri, string controlId, TapDirection direction, Codec codec, int rtpPtime, string? statusUrl) overload (direction/codec typed, Python parameter order) is the canonical/audited signature — the reference now emits enum<both,hear,speak> for direction, so the audit requires a typed port form — and a same-arity bare-string Tap overload is preserved as a .NET-only parity escape hatch (Python takes bare str; identical SWML via a shared core). The string overload is selected only when direction/codec are passed as strings. Distinct from RecordDirection ({speak,listen,both}) — the two verbs validate different vocabularies.
signalwire.swml.document.Document.add_raw_verb: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.add_section: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.add_verb: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.add_verb_to_section: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.clear_section: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.get_verbs: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.has_section: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.__init__: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.render: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.render_pretty: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.reset: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.to_dict: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.swml.document.Document.version: .NET ships Document class; Python uses signalwire.core.swml_builder helpers
signalwire.core.agent_base.AgentBase.create_tool_token: Public helper on AgentBase to mint scoped function-call tokens; Python ships equivalent via SessionManager
signalwire.core.skill_base.SkillBase.agent: Public read-only property surface; Python @property accessor with the same name
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.datasphere.skill.DataSphereSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.datasphere.skill.DataSphereSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.google_maps.skill.GoogleMapsSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.google_maps.skill.GoogleMapsSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.info_gatherer.skill.InfoGathererSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.info_gatherer.skill.InfoGathererSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.joke.skill.JokeSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.joke.skill.JokeSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.math.skill.MathSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.math.skill.MathSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.registry.SkillRegistry.instance: Public static SkillRegistry.Instance singleton accessor; Python uses module-level get_skill_registry()
signalwire.skills.spider.skill.SpiderSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.spider.skill.SpiderSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.weather_api.skill.WeatherApiSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.weather_api.skill.WeatherApiSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.web_search.skill.WebSearchSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.web_search.skill.WebSearchSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.register_tools: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.setup: Inherited from SkillBase abstract API; .NET enumerator emits methods on the declaring class only - these are required overrides per skill
signalwire.pom.pom.PromptObjectModel.debug: C# auto-property exposing the Python attribute ``debug`` set in ``__init__``; Python's enumerator emits attributes only when defined on the class
signalwire.pom.pom.PromptObjectModel.sections: C# auto-property exposing the Python attribute ``sections`` set in ``__init__``; Python's enumerator emits attributes only when defined on the class
signalwire.pom.pom.Section.body: C# auto-property exposing the Python attribute ``body`` set in ``__init__``; Python's enumerator emits attributes only when defined on the class
signalwire.pom.pom.Section.bullets: C# auto-property exposing the Python attribute ``bullets`` set in ``__init__``; Python's enumerator emits attributes only when defined on the class
signalwire.pom.pom.Section.numbered: C# auto-property exposing the Python attribute ``numbered`` set in ``__init__``; Python's enumerator emits attributes only when defined on the class
signalwire.pom.pom.Section.numbered_bullets: C# auto-property exposing the Python attribute ``numberedBullets`` set in ``__init__``; Python's enumerator emits attributes only when defined on the class
signalwire.pom.pom.Section.subsections: C# auto-property exposing the Python attribute ``subsections`` set in ``__init__``; Python's enumerator emits attributes only when defined on the class
signalwire.pom.pom.Section.title: C# auto-property exposing the Python attribute ``title`` set in ``__init__``; Python's enumerator emits attributes only when defined on the class

## Surface-audit additions (dotnet projection)

The .NET enumerator projects symbols under canonical Python paths
(`signalwire.relay.call.*`, `signalwire.rest.client.RestClient`, etc.).
This section documents port-only entries surfaced by the Layer B audit
under those canonical paths, mirroring the bucket terminology used by
other ports' PORT_ADDITIONS files (mixin-lifted, namespace_field_accessor,
action_protocol_method, idiomatic_getter, ...).

### Logger getters (idiomatic_getter)

dotnet exposes `Logger` instances on AgentServer / SkillManager / SkillRegistry as readonly properties. Python keeps the equivalent as `self.logger` instance attributes that the Python enumerator excludes as state.

signalwire.agent_server.AgentServer.logger: idiomatic_getter: .NET public Logger property; Python keeps `self.logger` as an instance attribute that the Python adapter excludes as state.
signalwire.core.skill_manager.SkillManager.logger: idiomatic_getter: .NET public Logger property; Python keeps `self.logger` as an instance attribute that the Python adapter excludes as state.
signalwire.skills.registry.SkillRegistry.logger: idiomatic_getter: .NET public Logger property; Python keeps `self.logger` as an instance attribute that the Python adapter excludes as state.

### AgentBase mixin-lifted accessors

mixin_lifted: .NET folds Python's PromptMixin / SkillManagerMixin / SipMixin onto AgentBase as readonly properties so callers don't reach into a sub-object — same pattern as the documented mixin-lifted bucket in other ports.

signalwire.core.agent_base.AgentBase.get_prompt_sections: prompt_mixin_lifted: .NET AgentBase exposes a get_prompt_sections() accessor; Python's equivalent lives on PromptMixin (mirrors mixin-lifted pattern).
signalwire.core.agent_base.AgentBase.is_auto_map_sip_usernames: mixin_lifted: .NET AgentBase exposes a typed `is_auto_map_sip_usernames` predicate getter for the SIP-routing config; Python keeps the equivalent as an attribute-style boolean flag.
signalwire.core.agent_base.AgentBase.skill_manager: mixin_lifted: .NET AgentBase exposes a `skill_manager` getter for the underlying SkillManager; Python keeps the equivalent as a private attribute accessed via `self._skill_manager`.

### PromptMixin port-emitted methods on the actual mixin path

The .NET enumerator emits `get_contexts` / `get_raw_prompt` on `PromptMixin` directly (the mixin Python defines them on too). Python's signature inventory excludes these because the Python adapter only emits methods on the consuming class via the mixin-roll-up rule; .NET emits them on the declaring mixin.

signalwire.core.mixins.prompt_mixin.PromptMixin.get_contexts: prompt_mixin_lifted: .NET emits this method on the declaring mixin path; Python's adapter excludes mixin declarations and only emits via the consuming class. Functional parity preserved.
signalwire.core.mixins.prompt_mixin.PromptMixin.get_raw_prompt: prompt_mixin_lifted: .NET emits this method on the declaring mixin path; Python's adapter excludes mixin declarations and only emits via the consuming class. Functional parity preserved.

### POM / SWMLBuilder back-reference accessors (idiomatic_getter)

signalwire.core.pom_builder.PomBuilder.pom: idiomatic_getter: .NET public PomBuilder.pom getter returning the wrapped PromptObjectModel; Python's `pom` is an instance attribute that the surface enumerator excludes (Python only includes methods, not instance-level attribute names).
signalwire.core.swml_builder.SWMLBuilder.service: idiomatic_getter: .NET readonly accessor exposing the parent SWMLService back-reference; Python keeps the equivalent as a private attribute.

### SWMLService tool_mixin_lifted method


### Action common methods (action_protocol_method)

action_protocol_method: .NET models all Action variants (PlayAction, RecordAction, ...) on top of a single Action base class that exposes the shared protocol (call_id, control_id, state, handle_event, resolve, stop, ...). Python uses an action-class-per-type hierarchy; the same data is exposed via attribute access on each action and the protocol implementations live on shared internals.

signalwire.relay.call.Action.accepts_terminal_event: action_protocol_method: .NET shared Action protocol; Python folds this into wait()/dispatch paths.
signalwire.relay.call.Action.call_id: action_protocol_method: .NET Action accessor for the parent call_id; Python uses attribute access.
signalwire.relay.call.Action.completed: action_protocol_method: .NET Action accessor for the completed-state predicate; Python uses attribute access.
signalwire.relay.call.Action.control_id: action_protocol_method: .NET Action accessor for the per-action control_id; Python uses attribute access.
signalwire.relay.call.Action.events: action_protocol_method: .NET Action accessor for the underlying event stream; Python uses attribute access.
signalwire.relay.call.Action.execute_subcommand: action_protocol_method: .NET shared Action protocol method for sub-command dispatch; Python uses internal helpers.
signalwire.relay.call.Action.get_call_id: action_protocol_method: .NET getter sibling of `call_id`; Python uses attribute access only.
signalwire.relay.call.Action.get_control_id: action_protocol_method: .NET getter sibling of `control_id`; Python uses attribute access only.
signalwire.relay.call.Action.get_node_id: action_protocol_method: .NET getter sibling of `node_id`; Python uses attribute access only.
signalwire.relay.call.Action.get_stop_method: action_protocol_method: .NET shared protocol used to dispatch the per-action `stop` RPC name; Python keeps this in a per-action `_stop_method` attribute.
signalwire.relay.call.Action.handle_event: action_protocol_method: .NET shared Action event-handling protocol; Python keeps the equivalent in private dispatch helpers.
signalwire.relay.call.Action.node_id: action_protocol_method: .NET Action accessor for the node_id; Python uses attribute access.
signalwire.relay.call.Action.on_completed: action_protocol_method: .NET callback registration helper; Python keeps the equivalent on Call/Action via async iterators.
signalwire.relay.call.Action.payload: action_protocol_method: .NET Action accessor for the originating-event payload; Python uses attribute access.
signalwire.relay.call.Action.resolve: action_protocol_method: .NET shared Action protocol for promise/task resolution; Python uses async/await and asyncio.Future internally.
signalwire.relay.call.Action.result: action_protocol_method: .NET Action accessor for the typed result struct; Python uses attribute access (`action.result`).
signalwire.relay.call.Action.state: action_protocol_method: .NET Action accessor for the state enum; Python uses attribute access.
signalwire.relay.call.Action.stop: action_protocol_method: .NET shared Action stop helper; Python uses per-action stop methods.

### Per-action `get_stop_method` overrides (action_protocol_method)

signalwire.relay.call.AIAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.
signalwire.relay.call.CollectAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.
signalwire.relay.call.DetectAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.
signalwire.relay.call.FaxAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.
signalwire.relay.call.PayAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.
signalwire.relay.call.PlayAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.
signalwire.relay.call.RecordAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.
signalwire.relay.call.StreamAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.
signalwire.relay.call.TapAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.
signalwire.relay.call.TranscribeAction.get_stop_method: action_protocol_method: .NET per-action override returning the `stop` RPC name; Python uses a `_stop_method` attribute.

### Per-action subclass overrides (action_protocol_method)

signalwire.relay.call.CollectAction.accepts_terminal_event: action_protocol_method: .NET CollectAction override of the shared terminal-event predicate; Python uses inline branching.
signalwire.relay.call.CollectAction.collect_result: idiomatic_getter: .NET strongly-typed accessor for the CollectAction result payload; Python uses attribute access on the action.
signalwire.relay.call.CollectAction.handle_event: action_protocol_method: .NET CollectAction event-handling override; Python folds into the dispatch helpers.
signalwire.relay.call.DetectAction.detect_result: idiomatic_getter: .NET strongly-typed accessor for the DetectAction result payload; Python uses attribute access on the action.
signalwire.relay.call.DetectAction.handle_event: action_protocol_method: .NET DetectAction event-handling override; Python folds into the dispatch helpers.
signalwire.relay.call.FaxAction.fax_type: idiomatic_getter: .NET FaxAction accessor for the fax_type enum; Python uses attribute access.
signalwire.relay.call.RecordAction.duration: idiomatic_getter: .NET RecordAction explicit accessor for `duration`; Python uses attribute-style access.
signalwire.relay.call.RecordAction.size: idiomatic_getter: .NET RecordAction explicit accessor for `size`; Python uses attribute-style access.
signalwire.relay.call.RecordAction.url: idiomatic_getter: .NET RecordAction explicit accessor for `url`; Python uses attribute-style access.

### Call accessors (idiomatic_getter)

signalwire.relay.call.Call.direction: idiomatic_getter: .NET Call accessor for the call direction enum; Python uses attribute access.
signalwire.relay.call.Call.typed_listeners: idiomatic_getter: .NET Call accessor for the typed-event listener registry; Python uses dict attribute access.

### CrudWithAddresses explicit constructor

signalwire.rest._base.CrudWithAddresses.__init__: .NET-port explicit constructor for the abstract CrudWithAddresses; Python inherits BaseResource's `__init__` implicitly so the enumerator only emits it on the base.

### PaginatedIterator .NET-idiom accessors

idiomatic_getter / dotnet_async_enumerator: .NET PaginatedIterator exposes property-style accessors on its fields (`http`, `path`, `params`, `data_key`, `index`, `items`) and ships an `IAsyncEnumerable`-flavored `get_async_enumerator` / `next` / `done` API. Python uses `__iter__` / `__next__` and attribute access.

signalwire.rest._pagination.PaginatedIterator.data_key: idiomatic_getter: .NET property accessor for the underlying field; Python uses attribute access.
signalwire.rest._pagination.PaginatedIterator.done: idiomatic_getter: .NET predicate accessor (true once the iterator is exhausted); Python uses `StopIteration` / `not has_more`.
signalwire.rest._pagination.PaginatedIterator.get_async_enumerator: dotnet_async_enumerator: .NET ships an `IAsyncEnumerable`-flavored helper; Python uses `__aiter__`.
signalwire.rest._pagination.PaginatedIterator.http: idiomatic_getter: .NET property accessor for the underlying field; Python uses attribute access.
signalwire.rest._pagination.PaginatedIterator.index: idiomatic_getter: .NET property accessor for the underlying field; Python uses attribute access.
signalwire.rest._pagination.PaginatedIterator.items: idiomatic_getter: .NET property accessor for the underlying field; Python uses attribute access.
signalwire.rest._pagination.PaginatedIterator.next: dotnet_async_enumerator: .NET advances the iterator one item at a time; Python uses `__next__`.
signalwire.rest._pagination.PaginatedIterator.params: idiomatic_getter: .NET property accessor for the underlying field; Python uses attribute access.
signalwire.rest._pagination.PaginatedIterator.path: idiomatic_getter: .NET property accessor for the underlying field; Python uses attribute access.

### RestClient namespace getters (namespace_field_accessor)

namespace_field_accessor: .NET RestClient exposes each namespace as a readonly property; Python uses attribute access on the client instance. The path `signalwire.rest.client.RestClient` is the canonical Python-projection path the .NET enumerator now emits (older entries used `signalwire.rest.rest_client.RestClient`).

signalwire.rest.client.RestClient.base_url: namespace_field_accessor: .NET RestClient field accessor for the base URL; Python uses attribute access.
signalwire.rest.client.RestClient.http: namespace_field_accessor: .NET RestClient field accessor for the HTTP transport; Python uses attribute access.
signalwire.rest.client.RestClient.project_id: namespace_field_accessor: .NET RestClient field accessor for the project_id; Python uses attribute access.
signalwire.rest.client.RestClient.space: namespace_field_accessor: .NET RestClient field accessor for the space domain; Python uses attribute access.
signalwire.rest.client.RestClient.token: namespace_field_accessor: .NET RestClient field accessor for the API token; Python uses attribute access.

### SkillRegistry .NET-specific accessors

signalwire.skills.registry.SkillRegistry.external_paths: idiomatic_getter: .NET SkillRegistry accessor for paths added via AddSearchPath; Python's equivalent state is private and accessed via the registry's internal list.

### ExecutionMode (dotnet helper class)

dotnet_helper_class: .NET ships ExecutionMode as a dedicated class with classmethod-style helpers (`get_execution_mode`, `is_serverless_mode`); Python ships these as module-level functions under `signalwire.utils`.


### SchemaUtils / SchemaValidationError extra accessors

signalwire.utils.schema_utils.SchemaValidationError.errors: idiomatic_getter: .NET typed accessor for the validation error list; Python keeps the equivalent on the exception's `args` tuple.
signalwire.utils.schema_utils.SchemaValidationError.verb_name: idiomatic_getter: .NET typed accessor for the verb name that failed validation; Python uses message-string parsing.

### UrlValidator (dotnet wrapper class)

dotnet_helper_class: .NET ships UrlValidator as a class with static-method-style helpers; Python uses module-level `validate_url(...)` and a private resolver hook in `signalwire.utils.url_validator`.

signalwire.core.security.webhook_middleware.WebhookValidationMiddleware: dotnet_idiom: middleware as a class (Python uses FastAPI dependency factory function)

### Tier-3 typed RELAY state enums + Device (idiom: type the knowable shape alongside the parity string)

dotnet_tier3_typed_state: The RELAY call/dial/message states are knowable closed sets (Python `signalwire/relay/constants.py` `CALL_STATES`/`MESSAGE_STATE_*` + `relay-protocol/messaging.state.event.json`). .NET ships them as `enum` (CallState/DialState/MessageState) with a `*Extensions` class exposing `ToWireName()`/`IsTerminal()`/`TryParse()`, ALONGSIDE the parity-bearing bare-string state (`Constants.*`, `Call.State`, `Message.State` — unchanged). The three vocabularies are never conflated. The enums themselves are not surface-enumerated (only their extension helpers + the typed accessors are). Python exposes only the bare `str`, so these typed helpers + accessors have no reference counterpart.

signalwire.relay.call.Call.call_state: dotnet_tier3_typed_state: typed CallState? accessor parsing the bare-string Call.State into the knowable closed set {created,ringing,answered,ending,ended} (grounded in constants.py CALL_STATES); returns null for an unknown (server-growable) value so Call.State stays canonical. Python exposes only `Call.state` (bare str).
signalwire.relay.call_state_extensions.CallStateExtensions: dotnet_tier3_typed_state: static extension class for the CallState enum (ToWireName/IsTerminal/TryParse); no Python equivalent (Python uses bare str call states).
signalwire.relay.call_state_extensions.CallStateExtensions.is_terminal: dotnet_tier3_typed_state: terminal-state predicate for CallState, agreeing with Constants.CallTerminalStates (terminal = ended); Python checks `state == CALL_STATE_ENDED` inline.
signalwire.relay.call_state_extensions.CallStateExtensions.to_wire_name: dotnet_tier3_typed_state: maps a CallState member to its canonical wire string (the value on the calling.call.state event); the bare-string Call.State path preserves parity (Python uses bare str).
signalwire.relay.call_state_extensions.CallStateExtensions.try_parse: dotnet_tier3_typed_state: wire-string -> CallState parser that returns false (not throw) on an unknown server value, so the raw Call.State string is preserved forward-compatibly; Python has no typed parse (bare str).
signalwire.relay.dial_state_extensions.DialStateExtensions: dotnet_tier3_typed_state: static extension class for the DialState enum (dial-outcome vocabulary {dialing,answered,failed}, distinct from CallState); grounds the dial_state value Client.HandleDialEvent reads. No Python equivalent (bare str).
signalwire.relay.dial_state_extensions.DialStateExtensions.is_terminal: dotnet_tier3_typed_state: terminal-outcome predicate for DialState (answered/failed resolve the dial; dialing is in-progress); Python branches on the dial_state string inline.
signalwire.relay.dial_state_extensions.DialStateExtensions.to_wire_name: dotnet_tier3_typed_state: maps a DialState member to its canonical wire string (the calling.call.dial dial_state value); Python uses bare str.
signalwire.relay.dial_state_extensions.DialStateExtensions.try_parse: dotnet_tier3_typed_state: wire-string -> DialState parser tolerating an unknown server value (returns false, not throw); Python has no typed parse.
signalwire.relay.message.Message.message_state: dotnet_tier3_typed_state: typed MessageState? accessor parsing the bare-string Message.State into the knowable closed set {queued,initiated,sent,delivered,undelivered,failed,received} (grounded in constants.py MESSAGE_STATE_* + messaging.state.event.json); returns null for an unknown value so Message.State stays canonical. Python exposes only `Message.state` (bare str).
signalwire.relay.message_state_extensions.MessageStateExtensions: dotnet_tier3_typed_state: static extension class for the MessageState enum (messaging-delivery vocabulary, distinct from the voice vocabularies); no Python equivalent (bare str).
signalwire.relay.message_state_extensions.MessageStateExtensions.is_terminal: dotnet_tier3_typed_state: terminal-state predicate for MessageState, agreeing with Constants.MessageTerminalStates (delivered/undelivered/failed); Python checks against MESSAGE_TERMINAL_STATES inline.
signalwire.relay.message_state_extensions.MessageStateExtensions.to_wire_name: dotnet_tier3_typed_state: maps a MessageState member to its canonical wire string (the messaging.state message_state value); the bare-string Message.State path preserves parity (Python uses bare str).
signalwire.relay.message_state_extensions.MessageStateExtensions.try_parse: dotnet_tier3_typed_state: wire-string -> MessageState parser tolerating an unknown server value (returns false, not throw); Python has no typed parse.
signalwire.relay.device.Device: dotnet_tier3_typed_object: typed {type, params} RELAY device object (grounded in relay-protocol/calling.connect.params.json), recurring across connect/refer/dial/tap; types the SHAPE only (type stays a string — the discriminant set is not schema-enumerated). Python and the rest of this port pass devices as raw dicts; this is an additive convenience.
signalwire.relay.device.Device.__init__: dotnet_tier3_typed_object: constructs a Device from (type, params); the parity-bearing path remains the raw dict the RELAY methods already accept.
signalwire.relay.device.Device.to_dict: dotnet_tier3_typed_object: projects the Device to the raw {type, params} wire dictionary, byte-identical to the hand-written device dict; no Python equivalent (Python builds the dict directly).
signalwire.relay.device.Device.from_dict: dotnet_tier3_typed_object: reconstructs a Device from a raw {type, params} dict (e.g. Call.Device or a wire frame), returning null when no type discriminant is present; Python reads the raw dict directly.
signalwire.relay.device.Device.type: dotnet_tier3_typed_object: read-only string discriminant of the typed Device shape (kept a string — not schema-enumerated); Python uses the raw dict's "type" key.
signalwire.relay.device.Device.params: dotnet_tier3_typed_object: read-only params payload of the typed Device shape; Python uses the raw dict's "params" key.

## BedrockAgent options object (item H/I)

The .NET BedrockAgent takes a strongly-typed `BedrockOptions` construction
object (the C# options-object idiom, matching AgentOptions/ServiceOptions)
instead of Python's keyword arguments. The properties carry the same
construction parameters Python passes as `__init__` kwargs; the object itself is
a .NET-idiom addition with no Python-surface counterpart.

signalwire.agents.bedrock_options.BedrockOptions: dotnet_options_object: typed construction options for BedrockAgent (the C# options-object idiom; Python passes these as __init__ kwargs).
signalwire.agents.bedrock_options.BedrockOptions.name: dotnet_options_object: agent name construction option (Python __init__ kwarg).
signalwire.agents.bedrock_options.BedrockOptions.route: dotnet_options_object: HTTP route construction option (Python __init__ kwarg).
signalwire.agents.bedrock_options.BedrockOptions.system_prompt: dotnet_options_object: system-prompt construction option (Python __init__ kwarg).
signalwire.agents.bedrock_options.BedrockOptions.voice_id: dotnet_options_object: TTS voice-id construction option (Python __init__ kwarg / set_voice).
signalwire.agents.bedrock_options.BedrockOptions.temperature: dotnet_options_object: LLM temperature construction option (Python __init__ kwarg / set_llm_temperature).
signalwire.agents.bedrock_options.BedrockOptions.top_p: dotnet_options_object: LLM top_p construction option (Python __init__ kwarg / inference params).
signalwire.agents.bedrock_options.BedrockOptions.max_tokens: dotnet_options_object: LLM max-tokens construction option (Python __init__ kwarg / inference params).
signalwire.agents.bedrock_options.BedrockOptions.basic_auth_user: dotnet_options_object: basic-auth user construction option (Python __init__ kwarg / env).
signalwire.agents.bedrock_options.BedrockOptions.basic_auth_password: dotnet_options_object: basic-auth password construction option (Python __init__ kwarg / env).

<!-- agentbase-family folded additions (surface diff uses the agentbase-family
     token; the per-class signalwire.core.agent_base.AgentBase.<m> keys above
     remain for the UNFOLDED signature-drift gate — see ALLOWLIST_DISCIPLINE §3). -->
agentbase-family.build_ai_verb: .NET-specific AgentBase helper used by the SWML rendering pipeline; no Python-reference twin (Python builds the ai verb inline in render_swml). Folds to the agentbase-family token on the surface.
agentbase-family.clone_for_request: .NET-specific AgentBase per-request clone helper (dynamic-config multi-tenancy); Python clones inline. No reference twin.
agentbase-family.create_tool_token: Public AgentBase helper minting scoped function-call tokens; Python ships the equivalent on SessionManager, not on AgentBase. Genuine port-only accessor on this class.
agentbase-family.get_contexts: Public AgentBase accessor returning the ContextBuilder; Python's equivalent lives on PromptManager (composition delegate flattened onto AgentBase). No AgentBase-level twin in the reference.
agentbase-family.get_prompt_sections: .NET AgentBase exposes a get_prompt_sections() accessor; Python's equivalent lives on PromptMixin/PromptManager (mixin-lifted). No AgentBase-level reference twin.
agentbase-family.get_raw_prompt: .NET AgentBase get_raw_prompt() accessor; Python's equivalent lives on PromptManager (composition delegate). No AgentBase-level reference twin.
agentbase-family.get_skill_manager: .NET-specific AgentBase accessor returning the SkillManager it composes; Python exposes skill state differently (no equivalent AgentBase accessor).
agentbase-family.is_auto_map_sip_usernames: .NET AgentBase typed boolean predicate getter for the SIP-routing config; Python keeps the equivalent as an attribute-style flag, not an AgentBase method.
agentbase-family.is_webhook_signature_validation_enabled: .NET AgentBase public read-only flag for whether a SigningKey is configured; Python users check bool(agent.signing_key) directly.
agentbase-family.render_swml: .NET-specific AgentBase render entrypoint; Python routes rendering through the SwmlRenderer composition delegate. No AgentBase-level reference twin.
agentbase-family.render_swml_with_context: .NET-specific AgentBase context-aware render entrypoint; Python has no AgentBase-level twin (rendering is on the SwmlRenderer delegate).
agentbase-family.signing_key: Public read-only AgentBase property exposing the configured Signing Key; Python sets it as a plain attribute (porting-sdk/webhooks.md AgentBase integration), not a class-typed accessor recorded on the reference.
