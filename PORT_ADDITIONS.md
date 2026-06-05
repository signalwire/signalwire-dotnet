# PORT_ADDITIONS.md (signalwire-dotnet)

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

signalwire.agent.agent_options.AgentOptions.auto_answer: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.basic_auth_password: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.basic_auth_user: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.host: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.name: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.port: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.record_call: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.record_format: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.record_stereo: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.route: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.use_pom: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent_server.AgentServer.get_sip_username_mapping: Public helper added in .NET; Python equivalents are register_global_routing_callback / serve_static_files
signalwire.agent_server.AgentServer.handle_request: Public dispatch entry point on AgentServer; Python uses Flask/FastAPI request objects directly
signalwire.agent_server.AgentServer.host: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.agent_server.AgentServer.is_sip_routing_enabled: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.agent_server.AgentServer.port: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.agent_server.AgentServer.serve_static: Public helper added in .NET; Python equivalents are register_global_routing_callback / serve_static_files
signalwire.core.agent_base.AgentBase.build_ai_verb: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.agent_base.AgentBase.clone_for_request: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.agent_base.AgentBase.contexts: Public read-only property surface; Python @property accessor with the same name
signalwire.core.agent_base.AgentBase.handle_request: .NET overrides Service.handle_request on AgentBase to gate POST routes through the webhook signature validator when signing_key is set; Python wires the equivalent via FastAPI Depends() in web_mixin._register_routes
signalwire.core.agent_base.AgentBase.is_webhook_signature_validation_enabled: .NET surfaces a public read-only flag for whether SigningKey is configured; Python users check `bool(agent.signing_key)` directly
signalwire.core.agent_base.AgentBase.signing_key: Public read-only property exposing the configured Signing Key; Python sets it as an attribute (porting-sdk/webhooks.md AgentBase integration)
signalwire.agent.agent_options.AgentOptions.signing_key: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.agent.agent_options.AgentOptions.trust_proxy_for_signature: .NET options data class with init-only properties; Python uses kwargs to AgentBase.__init__
signalwire.core.agent_base.AgentBase.get_skill_manager: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.agent_base.AgentBase.render_swml: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.agent_base.AgentBase.render_swml_with_context: .NET-specific AgentBase helpers used by the rendering pipeline; Python implements equivalents under signalwire.core.agent.* sub-package
signalwire.core.contexts.ContextBuilder.attach_tool_name_supplier: Public helper added in .NET; Python ships equivalents at module level
signalwire.core.contexts.ContextBuilder.create_simple_context: Public helper added in .NET; Python ships equivalents at module level
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
signalwire.core.data_map.DataMap.create_expression_tool: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.data_map.DataMap.create_simple_api_tool: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.security.session_manager.SessionManager.create_token: Public method on SessionManager ported from Python; .NET groups them with simpler signatures
signalwire.core.security.session_manager.SessionManager.token_expiry_secs: Public property exposing the token expiry; Python uses a class constant
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
signalwire.core.skill_manager.SkillManager.list_skills: .NET method; Python ships equivalent functionality via list_loaded_skills
signalwire.core.swml_service.SWMLService.define_tool: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.define_tools: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.document: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.get_full_url: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.get_proxy_url_base: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.handle_request: Public dispatch entry point on SWMLService; Python uses Flask/FastAPI request objects directly
signalwire.core.swml_service.SWMLService.host: Public read-only property surface; Python @property accessor with the same name
signalwire.core.swml_service.SWMLService.list_tool_names: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.name: Public read-only property surface; Python @property accessor with the same name
signalwire.core.swml_service.SWMLService.on_function_call: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.port: Public read-only property surface; Python @property accessor with the same name
signalwire.core.swml_service.SWMLService.register_swaig_function: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.render_swml: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.route: Public read-only property surface; Python @property accessor with the same name
signalwire.core.swml_service.SWMLService.run: .NET HttpListener-based blocking entry point; Python's SWMLService.serve() plays the same role under uvicorn/Flask. Same purpose, different runtime
signalwire.core.swml_service.SWMLService.sleep: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.core.swml_service.SWMLService.verb: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.logging.logger.Logger.debug: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.error: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.get_logger: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.info: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.level: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.name: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger: .NET ships Logger as a class with named factory; Python uses module-level functions
signalwire.logging.logger.Logger.reset: .NET ships Logger as a class with named factory; Python uses module-level functions
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
signalwire.relay.action.Action.completed: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.events: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.execute_subcommand: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.get_call_id: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.get_control_id: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.get_node_id: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.handle_event: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.is_done: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.on_completed: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.payload: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.resolve: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.result: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.state: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.stop: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.action.Action.wait: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.ai_action.AIAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.ai_action.AIAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.ai_action.AIAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
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
signalwire.relay.call.Call.pass: Public Call method ported from Python's signalwire.relay.call.Call (.NET preserves the same surface name)
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
signalwire.relay.client.RelayClient.inbound_queue: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.messages: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.on_call_handler: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.on_event_handler: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.on_message_handler: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.pending_dials: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.pending: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.project: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.protocol: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.read_loop: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.read_once: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.reconnect: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.scheme: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.send_ack: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.send: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.session_id: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.client.RelayClient.token: Public Client surface ported from Python's RelayClient; Python ships these as private methods or @property accessors
signalwire.relay.collect_action.CollectAction.collect_result: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.collect_action.CollectAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.collect_action.CollectAction.handle_event: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.collect_action.CollectAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.collect_action.CollectAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.collect_action.CollectAction.start_input_timers: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.detect_action.DetectAction.detect_result: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.detect_action.DetectAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.detect_action.DetectAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.detect_action.DetectAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.event.Event.call_id: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.control_id: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.event_type: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.__init__: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.node_id: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.params: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.parse: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.state: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.tag: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.timestamp: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.event.Event.to_dict: .NET Event class lives in its own file; Python ships RelayEvent under signalwire.relay.constants
signalwire.relay.fax_action.FaxAction.fax_type: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.fax_action.FaxAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.fax_action.FaxAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.fax_action.FaxAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
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
signalwire.relay.pay_action.PayAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.pay_action.PayAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.pay_action.PayAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.play_action.PlayAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.play_action.PlayAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.play_action.PlayAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.play_action.PlayAction.pause: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.play_action.PlayAction.resume: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.play_action.PlayAction.volume: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.record_action.RecordAction.duration: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.record_action.RecordAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.record_action.RecordAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.record_action.RecordAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.record_action.RecordAction.pause: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.record_action.RecordAction.resume: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.record_action.RecordAction.size: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.record_action.RecordAction.url: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.stream_action.StreamAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.stream_action.StreamAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.stream_action.StreamAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.tap_action.TapAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.tap_action.TapAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.tap_action.TapAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.transcribe_action.TranscribeAction.get_stop_method: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.transcribe_action.TranscribeAction.__init__: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.relay.transcribe_action.TranscribeAction: .NET ships each Action subclass in its own file under SignalWire.Relay; Python groups them in signalwire.relay.call
signalwire.rest.crud_resource.CrudResource.base_path: .NET ships CrudResource under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.crud_resource.CrudResource.create: .NET ships CrudResource under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.crud_resource.CrudResource.delete: .NET ships CrudResource under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.crud_resource.CrudResource.get: .NET ships CrudResource under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.crud_resource.CrudResource.__init__: .NET ships CrudResource under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.crud_resource.CrudResource.list: .NET ships CrudResource under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.crud_resource.CrudResource: .NET ships CrudResource under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.crud_resource.CrudResource.update: .NET ships CrudResource under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.auth_header: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.base_url: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.delete: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.get: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.__init__: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.list_all: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.patch: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.post: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.project_id: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.put: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.http_client.HttpClient.token: .NET ships HttpClient under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.namespaces.calling.Calling.ai_hold: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.ai_message: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.ai_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.ai_unhold: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.client: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.collect: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.collect_start_input_timers: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.collect_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.denoise: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.denoise_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.detect: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.detect_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.dial: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.disconnect: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.end: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.get_base_path: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.__init__: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.live_transcribe: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.live_translate: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.play: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.play_pause: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.play_resume: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.play_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.play_volume: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.project_id: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.receive_fax_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.record: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.record_pause: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.record_resume: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.record_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.refer: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.send_fax_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.stream: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.stream_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.tap: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.tap_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.transcribe: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.transcribe_stop: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.transfer: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.update_call: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.calling.Calling.user_event: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.addresses: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.ai_agents: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.call_flows: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.call_queues: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.client: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.conference_rooms: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.conversations: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.dial_plans: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.freeclimb_apps: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.__init__: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.phone_numbers: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.sip_endpoints: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.sip_profiles: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.subscribers: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.namespaces.fabric.Fabric.swml_scripts: .NET groups REST namespaces under SignalWire.REST.Namespaces; Python uses signalwire.rest.<namespace>
signalwire.rest.rest_client.RestClient.addresses: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.base_url: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.calling: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.chat: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.compat: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.datasphere: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.fabric: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.http: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.imported_numbers: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.__init__: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.logs: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.lookup: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.mfa: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.number_groups: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.phone_numbers: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.project_id: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.project: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.pubsub: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.queues: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.recordings: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.registry: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.short_codes: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.sip_profile: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.space: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.token: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.verified_callers: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.rest_client.RestClient.video: .NET ships RestClient under SignalWire.REST.RestClient; Python's path is signalwire.rest.client
signalwire.rest.signal_wire_rest_error.SignalWireRestError.__init__: .NET ships SignalWireRestError under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.signal_wire_rest_error.SignalWireRestError: .NET ships SignalWireRestError under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.signal_wire_rest_error.SignalWireRestError.response_body: .NET ships SignalWireRestError under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.rest.signal_wire_rest_error.SignalWireRestError.status_code: .NET ships SignalWireRestError under SignalWire.REST; Python's path is signalwire.rest._base
signalwire.serverless.adapter.Adapter.detect: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter.handle_azure: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter.handle_lambda: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.serverless.adapter.Adapter.serve: .NET ships Adapter class; Python uses ServerlessMixin on AgentBase + signalwire.utils.is_serverless_mode
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.get_prompt_sections: Skill subclass overrides (Python ships the same overrides; the .NET file path differs from Python's nested module structure)
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.custom_skills.skill.CustomSkillsSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.custom_skills.skill.CustomSkillsSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.custom_skills.skill.CustomSkillsSkill: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.custom_skills.skill.CustomSkillsSkill.register_tools: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.custom_skills.skill.CustomSkillsSkill.setup: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.custom_skills.skill.CustomSkillsSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.datasphere.skill.DataSphereSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.datasphere.skill.DataSphereSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.datasphere.skill.DataSphereSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.datetime.skill.DatetimeSkill.description: .NET DatetimeSkill class capitalisation differs from Python's DateTimeSkill; both register the same tool name
signalwire.skills.datetime.skill.DatetimeSkill.get_prompt_sections: .NET DatetimeSkill class capitalisation differs from Python's DateTimeSkill; both register the same tool name
signalwire.skills.datetime.skill.DatetimeSkill.name: .NET DatetimeSkill class capitalisation differs from Python's DateTimeSkill; both register the same tool name
signalwire.skills.datetime.skill.DatetimeSkill: .NET DatetimeSkill class capitalisation differs from Python's DateTimeSkill; both register the same tool name
signalwire.skills.datetime.skill.DatetimeSkill.register_tools: .NET DatetimeSkill class capitalisation differs from Python's DateTimeSkill; both register the same tool name
signalwire.skills.datetime.skill.DatetimeSkill.setup: .NET DatetimeSkill class capitalisation differs from Python's DateTimeSkill; both register the same tool name
signalwire.skills.google_maps.skill.GoogleMapsSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.google_maps.skill.GoogleMapsSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.info_gatherer.skill.InfoGathererSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.info_gatherer.skill.InfoGathererSkill.get_prompt_sections: Skill subclass overrides (Python ships the same overrides; the .NET file path differs from Python's nested module structure)
signalwire.skills.info_gatherer.skill.InfoGathererSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.info_gatherer.skill.InfoGathererSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.joke.skill.JokeSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.joke.skill.JokeSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.math.skill.MathSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.math.skill.MathSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.registry.SkillRegistry.get_factory: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.registry.SkillRegistry.reset: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.skill_name_extensions.SkillNameExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the SkillName closed-set enum; .NET-only typed helper, no Python reference equivalent (Python uses bare str skill names).
signalwire.skills.skill_name_extensions.SkillNameExtensions.to_wire_name: dotnet_enum_idiom: maps the typed SkillName closed-set enum member to its canonical snake_case wire name; AddSkill/RemoveSkill/HasSkill expose a SkillName overload next to the string overload so built-in skill names are typo-checked at compile time, with the string path preserved for parity (Python uses bare str) and custom skills.
signalwire.skills.spider.skill.SpiderSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.spider.skill.SpiderSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.spider.skill.SpiderSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.weather_api.skill.WeatherApiSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.weather_api.skill.WeatherApiSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.web_search.skill.WebSearchSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.web_search.skill.WebSearchSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.web_search.skill.WebSearchSkill.supports_multiple_instances: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.web_search.skill.WebSearchSkill.version: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.description: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.name: Public method surfaced by the .NET enumerator under SignalWire.<namespace>; not in Python reference at this exact path
signalwire.swaig.callback_method_extensions.CallbackMethodExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the CallbackMethod closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates join_conference's status_callback_method / recording_status_callback_method str against {GET,POST}).
signalwire.swaig.callback_method_extensions.CallbackMethodExtensions.to_wire_name: dotnet_enum_idiom: maps the typed CallbackMethod closed-set enum member ({GET,POST} — the set the Python reference validates join_conference's callback-method args against) to its canonical wire value; JoinConferenceOptions exposes CallbackMethod fields next to the flat string overload so the HTTP verb is typo-checked at compile time, with the string path preserved for parity (Python takes bare str).
signalwire.swaig.codec_extensions.CodecExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the Codec closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates tap's codec str against {PCMU,PCMA} — the 2-value SWAIG-tap set, distinct from the larger RELAY connect/stream codec superset).
signalwire.swaig.codec_extensions.CodecExtensions.to_wire_name: dotnet_enum_idiom: maps the typed Codec closed-set enum member ({PCMU,PCMA} — the set the Python reference validates tap's codec arg against) to its canonical upper-case wire value; FunctionResult.Tap exposes a (TapDirection, Codec) overload next to the string overload so the tap codec is typo-checked at compile time, with the string path preserved for parity (Python takes bare str). Deliberately distinct from the RELAY connect/stream codec superset, which stays a string.
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
signalwire.swaig.record_direction_extensions.RecordDirectionExtensions.to_wire_name: dotnet_enum_idiom: maps the typed RecordDirection closed-set enum member ({speak,listen,both} — the set the Python reference validates record_call's direction arg against) to its canonical wire value; FunctionResult.RecordCall exposes a (RecordFormat, RecordDirection) overload next to the string overload so the recording direction is typo-checked at compile time, with the string path preserved for parity (Python takes bare str).
signalwire.swaig.record_format_extensions.RecordFormatExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the RecordFormat closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates record_call's format str against {wav,mp3,mp4}).
signalwire.swaig.record_format_extensions.RecordFormatExtensions.to_wire_name: dotnet_enum_idiom: maps the typed RecordFormat closed-set enum member ({wav,mp3,mp4} — the set the Python reference validates record_call's format arg against) to its canonical wire value; FunctionResult.RecordCall exposes a (RecordFormat, RecordDirection) overload next to the string overload so the recording format is typo-checked at compile time, with the string path preserved for parity (Python takes bare str).
signalwire.swaig.tap_direction_extensions.TapDirectionExtensions: dotnet_enum_idiom: static extension class exposing ToWireName() for the TapDirection closed-set enum; .NET-only typed helper, no Python reference equivalent (Python validates tap's direction str against {speak,hear,both} — note tap uses hear where record_call uses listen, so this is distinct from RecordDirection).
signalwire.swaig.tap_direction_extensions.TapDirectionExtensions.to_wire_name: dotnet_enum_idiom: maps the typed TapDirection closed-set enum member ({speak,hear,both} — the set the Python reference validates tap's direction arg against) to its canonical wire value; FunctionResult.Tap exposes a (TapDirection, Codec) overload next to the string overload so the tap direction is typo-checked at compile time, with the string path preserved for parity (Python takes bare str). Distinct from RecordDirection ({speak,listen,both}) — the two verbs validate different vocabularies.
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
signalwire.swml.schema.Schema.get_verb_names: .NET ships Schema as a class; Python uses signalwire.utils.schema_utils functions
signalwire.swml.schema.Schema.get_verb: .NET ships Schema as a class; Python uses signalwire.utils.schema_utils functions
signalwire.swml.schema.Schema.is_valid_verb: .NET ships Schema as a class; Python uses signalwire.utils.schema_utils functions
signalwire.swml.schema.Schema: .NET ships Schema as a class; Python uses signalwire.utils.schema_utils functions
signalwire.swml.schema.Schema.reset: .NET ships Schema as a class; Python uses signalwire.utils.schema_utils functions
signalwire.swml.schema.Schema.verb_count: .NET ships Schema as a class; Python uses signalwire.utils.schema_utils functions
signalwire.swml.service_options.ServiceOptions.basic_auth_password: .NET options data class with init-only properties; Python uses kwargs to SWMLService.__init__
signalwire.swml.service_options.ServiceOptions.basic_auth_user: .NET options data class with init-only properties; Python uses kwargs to SWMLService.__init__
signalwire.swml.service_options.ServiceOptions.host: .NET options data class with init-only properties; Python uses kwargs to SWMLService.__init__
signalwire.swml.service_options.ServiceOptions.name: .NET options data class with init-only properties; Python uses kwargs to SWMLService.__init__
signalwire.swml.service_options.ServiceOptions: .NET options data class with init-only properties; Python uses kwargs to SWMLService.__init__
signalwire.swml.service_options.ServiceOptions.port: .NET options data class with init-only properties; Python uses kwargs to SWMLService.__init__
signalwire.swml.service_options.ServiceOptions.route: .NET options data class with init-only properties; Python uses kwargs to SWMLService.__init__
signalwire.core.agent_base.AgentBase.create_tool_token: Public helper on AgentBase to mint scoped function-call tokens; Python ships equivalent via SessionManager
signalwire.core.skill_base.SkillBase.agent: Public read-only property surface; Python @property accessor with the same name
signalwire.core.swml_service.SWMLService.get_basic_auth_credentials_with_source: Public 3-tuple variant of GetBasicAuthCredentials returning (user,password,source); Python folds source into a single method via include_source kwarg
signalwire.core.swml_service.SWMLService.get_function: Public tool-registry query method on Service (which AgentBase inherits); Python equivalents live on ToolMixin/ToolRegistry - the cross-class duplication is a .NET inheritance artefact
signalwire.core.swml_service.SWMLService.has_function: Public tool-registry query method on Service (which AgentBase inherits); Python equivalents live on ToolMixin/ToolRegistry - the cross-class duplication is a .NET inheritance artefact
signalwire.core.swml_service.SWMLService.on_swml_request: Public async hook on Service for in-flight SWML mutation; Python WebMixin.on_swml_request plays the same role on AgentBase
signalwire.core.swml_service.SWMLService.remove_function: Public tool-registry query method on Service (which AgentBase inherits); Python equivalents live on ToolMixin/ToolRegistry - the cross-class duplication is a .NET inheritance artefact
signalwire.core.swml_service.SWMLService.validate_basic_auth: Public auth helper inherited from Service via the .NET class hierarchy; Python AuthMixin defines the same name on AgentBase
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
signalwire.swml.verb_info.VerbInfo.__init__: .NET-only public type used by the SWML verb registry; Python keeps verb metadata as plain dicts
signalwire.swml.verb_info.VerbInfo.deconstruct: .NET-only public type used by the SWML verb registry; Python keeps verb metadata as plain dicts
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

signalwire.core.swml_service.SWMLService.get_all_functions: tool_mixin_lifted: .NET exposes the tool registry's accessor directly on SWMLService; Python keeps this on `ToolRegistry` and accesses via `agent.tool_registry.get_all_functions()`.

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

signalwire.rest.client.RestClient.addresses: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.base_url: namespace_field_accessor: .NET RestClient field accessor for the base URL; Python uses attribute access.
signalwire.rest.client.RestClient.calling: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.chat: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.compat: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.datasphere: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.fabric: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.http: namespace_field_accessor: .NET RestClient field accessor for the HTTP transport; Python uses attribute access.
signalwire.rest.client.RestClient.imported_numbers: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.logs: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.lookup: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.mfa: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.number_groups: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.phone_numbers: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.project: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.project_id: namespace_field_accessor: .NET RestClient field accessor for the project_id; Python uses attribute access.
signalwire.rest.client.RestClient.pubsub: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.queues: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.recordings: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.registry: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.short_codes: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.sip_profile: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.space: namespace_field_accessor: .NET RestClient field accessor for the space domain; Python uses attribute access.
signalwire.rest.client.RestClient.token: namespace_field_accessor: .NET RestClient field accessor for the API token; Python uses attribute access.
signalwire.rest.client.RestClient.verified_callers: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.
signalwire.rest.client.RestClient.video: namespace_field_accessor: .NET RestClient namespace accessor; Python uses attribute access on the client.

### Per-namespace field accessors (namespace_field_accessor)

namespace_field_accessor: .NET REST namespaces expose each field/sub-resource as a readonly property; Python uses attribute access on the namespace instance. The same flatten-the-MRO pattern documented elsewhere also produces explicit `__init__` constructors and per-resource `base_path` getters since C# requires explicit constructors and doesn't carry class-level base_path attributes through inheritance.

signalwire.rest.namespaces.calling.CallingNamespace.client: namespace_field_accessor: .NET accessor for the parent client reference; Python uses a private attribute.
signalwire.rest.namespaces.calling.CallingNamespace.get_base_path: namespace_field_accessor: .NET method-style getter for the namespace base path; Python uses a class-level attribute.
signalwire.rest.namespaces.calling.CallingNamespace.project_id: namespace_field_accessor: .NET accessor for the project_id field; Python uses an instance attribute.
signalwire.rest.namespaces.calling.CallingNamespace.update_call: .NET helper for updating an in-flight call; Python clients use client.calls(sid).update().

signalwire.rest.namespaces.compat.CompatApplications.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatCalls.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatConferences.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatConferences.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.compat.CompatFaxes.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatLamlBins.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatMessages.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatNamespace.account_sid: namespace_field_accessor: .NET accessor for the account_sid field; Python uses an instance attribute.
signalwire.rest.namespaces.compat.CompatNamespace.accounts: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.applications: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.calls: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.conferences: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.faxes: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.laml_bins: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.messages: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.phone_numbers: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.queues: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.recordings: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.tokens: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatNamespace.transcriptions: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.compat.CompatPhoneNumbers.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.compat.CompatQueues.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatRecordings.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatRecordings.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.compat.CompatTokens.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatTokens.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.compat.CompatTranscriptions.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.compat.CompatTranscriptions.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.

signalwire.rest.namespaces.datasphere.DatasphereNamespace.documents: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.

signalwire.rest.namespaces.fabric.CallFlowsResource.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.fabric.CallFlowsResource.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.fabric.ConferenceRoomsResource.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.fabric.ConferenceRoomsResource.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.fabric.FabricAddresses.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.fabric.FabricAddresses.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.fabric.FabricNamespace.addresses: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.addresses_top_level: dotnet_typed_namespace_alias: .NET ships a typed alias for the top-level Addresses sub-resource (covers the cross-fabric `/addresses` endpoint); Python keeps a single `addresses` attribute and lets callers branch by argument.
signalwire.rest.namespaces.fabric.FabricNamespace.ai_agents: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.call_flows: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.call_flows_ops: dotnet_typed_namespace_alias: .NET ships a typed-ops alias for the CallFlows sub-resource; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.call_queues: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.client: namespace_field_accessor: .NET accessor for the parent client reference; Python uses a private attribute.
signalwire.rest.namespaces.fabric.FabricNamespace.conference_rooms: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.conference_rooms_ops: dotnet_typed_namespace_alias: .NET ships a typed-ops alias for the ConferenceRooms sub-resource; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.conversations: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.cxml_applications: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.cxml_applications_ops: dotnet_typed_namespace_alias: .NET ships a typed-ops alias for the CxmlApplications sub-resource; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.cxml_scripts: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.cxml_webhooks: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.dial_plans: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.freeclimb_apps: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.freeswitch_connectors: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.phone_numbers: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.relay_applications: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.resources: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.resources_generic: dotnet_typed_namespace_alias: .NET ships a typed alias for the GenericResources sub-resource; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.sip_endpoints: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.sip_gateways: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.sip_profiles: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.subscribers: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.subscribers_ops: dotnet_typed_namespace_alias: .NET ships a typed-ops alias for the Subscribers sub-resource; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.swml_scripts: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.swml_webhooks: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.tokens: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.FabricNamespace.tokens_api: dotnet_typed_namespace_alias: .NET ships a typed-api alias for the Tokens sub-resource; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.fabric.GenericResources.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.fabric.GenericResources.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.fabric.SubscribersResource.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.fabric.SubscribersResource.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.

signalwire.rest.namespaces.logs.ConferenceLogs.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.logs.ConferenceLogs.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.logs.FaxLogs.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.logs.FaxLogs.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.logs.LogsNamespace.conferences: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.logs.LogsNamespace.fax: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.logs.LogsNamespace.messages: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.logs.LogsNamespace.voice: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.logs.MessageLogs.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.logs.MessageLogs.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.logs.VoiceLogs.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.logs.VoiceLogs.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.

signalwire.rest.namespaces.number_groups.NumberGroupsResource.update: .NET port emits explicit CRUD where Python inherits via CrudResource.

signalwire.rest.namespaces.project.ProjectNamespace.tokens: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.

signalwire.rest.namespaces.queues.QueuesResource.update: .NET port emits explicit CRUD where Python inherits via CrudResource.

signalwire.rest.namespaces.registry.RegistryBrands.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.registry.RegistryBrands.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.registry.RegistryCampaigns.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.registry.RegistryCampaigns.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.registry.RegistryNamespace.brands: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.registry.RegistryNamespace.campaigns: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.registry.RegistryNamespace.numbers: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.registry.RegistryNamespace.orders: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.registry.RegistryNumbers.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.registry.RegistryNumbers.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.registry.RegistryOrders.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.registry.RegistryOrders.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.

signalwire.rest.namespaces.video.VideoConferenceTokens.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.video.VideoConferenceTokens.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.video.VideoConferences.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.video.VideoConferences.update: .NET port emits explicit CRUD where Python inherits via CrudResource.
signalwire.rest.namespaces.video.VideoNamespace.conference_tokens: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.video.VideoNamespace.conferences: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.video.VideoNamespace.room_recordings: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.video.VideoNamespace.room_sessions: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.video.VideoNamespace.room_tokens: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.video.VideoNamespace.rooms: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.video.VideoNamespace.streams: namespace_field_accessor: .NET sub-resource getter for the namespace; Python uses attribute access on the namespace instance.
signalwire.rest.namespaces.video.VideoRoomRecordings.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.video.VideoRoomRecordings.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.video.VideoRoomSessions.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.video.VideoRoomSessions.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.video.VideoRoomTokens.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.video.VideoRoomTokens.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.
signalwire.rest.namespaces.video.VideoRooms.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.video.VideoRooms.update: .NET port emits explicit CRUD where Python inherits via CrudResource.
signalwire.rest.namespaces.video.VideoStreams.__init__: .NET port emits an explicit constructor; Python's BaseResource.__init__ is inherited.
signalwire.rest.namespaces.video.VideoStreams.base_path: namespace_field_accessor: .NET accessor for the resource's base path; Python uses a class-level attribute.

### SkillRegistry .NET-specific accessors

signalwire.skills.registry.SkillRegistry.external_paths: idiomatic_getter: .NET SkillRegistry accessor for paths added via AddSearchPath; Python's equivalent state is private and accessed via the registry's internal list.

### ExecutionMode (dotnet helper class)

dotnet_helper_class: .NET ships ExecutionMode as a dedicated class with classmethod-style helpers (`get_execution_mode`, `is_serverless_mode`); Python ships these as module-level functions under `signalwire.utils`.

signalwire.utils.execution_mode.ExecutionMode: dotnet_helper_class: .NET wraps execution-mode helpers in a class; Python uses module-level functions in signalwire.utils.
signalwire.utils.execution_mode.ExecutionMode.get_execution_mode: dotnet_helper_class: .NET classmethod-style accessor; Python uses a module-level function `signalwire.utils.get_execution_mode()`.
signalwire.utils.execution_mode.ExecutionMode.is_serverless_mode: dotnet_helper_class: .NET classmethod-style accessor; Python uses a module-level function `signalwire.utils.is_serverless_mode()`.

### SchemaUtils / SchemaValidationError extra accessors

signalwire.utils.schema_utils.SchemaUtils.get_verb: idiomatic_getter: .NET SchemaUtils helper exposing per-verb metadata; Python keeps verb metadata internal and reaches in via the JSON schema only.
signalwire.utils.schema_utils.SchemaUtils.get_verb_names: idiomatic_getter: .NET SchemaUtils helper enumerating known verb names; Python keeps the equivalent internal.
signalwire.utils.schema_utils.SchemaUtils.is_valid_verb: idiomatic_getter: .NET SchemaUtils predicate for verb-name validation; Python infers from the schema lookup directly.
signalwire.utils.schema_utils.SchemaUtils.reset: dotnet_helper_class: .NET test-side helper to reset the cached schema state between tests; Python uses module reload.
signalwire.utils.schema_utils.SchemaUtils.verb_count: idiomatic_getter: .NET SchemaUtils helper exposing the number of registered verbs; Python keeps the equivalent internal.
signalwire.utils.schema_utils.SchemaValidationError.errors: idiomatic_getter: .NET typed accessor for the validation error list; Python keeps the equivalent on the exception's `args` tuple.
signalwire.utils.schema_utils.SchemaValidationError.verb_name: idiomatic_getter: .NET typed accessor for the verb name that failed validation; Python uses message-string parsing.

### UrlValidator (dotnet wrapper class)

dotnet_helper_class: .NET ships UrlValidator as a class with static-method-style helpers; Python uses module-level `validate_url(...)` and a private resolver hook in `signalwire.utils.url_validator`.

signalwire.utils.url_validator.UrlValidator: dotnet_helper_class: .NET wrapper class for URL-validation helpers; Python uses module-level functions.
signalwire.utils.url_validator.UrlValidator.validate_url: dotnet_helper_class: .NET method-style API for the same `validate_url` function Python ships at module level (`signalwire.utils.url_validator.validate_url`).
signalwire.utils.url_validator.UrlValidator.validate_url_with_resolved_addresses: dotnet_helper_class: .NET helper that exposes a 2-tuple (validated url, resolved addresses) for callers that need to log or audit DNS resolution; Python keeps the resolver hook private and tests patch it via `unittest.mock.patch`.
signalwire.core.security.webhook_middleware.WebhookValidationMiddleware: dotnet_idiom: middleware as a class (Python uses FastAPI dependency factory function)
signalwire.core.security.webhook_validator.WebhookValidator: dotnet_idiom_class_wrapper: static class containing validator functions (Python keeps them at module level)
signalwire.core.security.webhook_validator.WebhookValidator.validate_request: dotnet_idiom_class_wrapper: see WebhookValidator class entry
signalwire.core.security.webhook_validator.WebhookValidator.validate_webhook_signature: dotnet_idiom_class_wrapper: see WebhookValidator class entry
