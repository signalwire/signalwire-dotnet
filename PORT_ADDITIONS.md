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
