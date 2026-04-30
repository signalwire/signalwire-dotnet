# PORT_SIGNATURE_OMISSIONS.md

Documented signature divergences between this .NET port and the Python
reference. Each entry excuses signature drift on a symbol that exists in
both. (Names-only divergences live in PORT_OMISSIONS.md / PORT_ADDITIONS.md
and are inherited automatically by `diff_port_signatures.py`.)

Format:
    <fully.qualified.symbol>: <one-line rationale>

Excused divergences fall into two classes:

1. **Idiom-level divergences** (deliberate, not fixable without breaking
   the .NET port's API style):
   - .NET constructors take an Options object; Python uses kwargs.
   - .NET methods return `this` for fluent chaining; Python returns None.

2. **Port maintenance backlog** (tracked here; will be reduced over time
   as the .NET port catches up to Python signature parity).


## Idiom: .NET options-pattern constructors

signalwire.core.agent_base.AgentBase.__init__: .NET ctor takes Options object instead of kwargs
signalwire.core.contexts.ContextBuilder.__init__: .NET ctor takes Options object instead of kwargs
signalwire.core.contexts.GatherQuestion.__init__: .NET ctor takes Options object instead of kwargs
signalwire.core.security.session_manager.SessionManager.__init__: .NET ctor takes Options object instead of kwargs
signalwire.core.swml_service.SWMLService.__init__: .NET ctor takes Options object instead of kwargs
signalwire.prefabs.concierge.ConciergeAgent.__init__: .NET ctor takes Options object instead of kwargs
signalwire.prefabs.faq_bot.FAQBotAgent.__init__: .NET ctor takes Options object instead of kwargs
signalwire.prefabs.info_gatherer.InfoGathererAgent.__init__: .NET ctor takes Options object instead of kwargs
signalwire.prefabs.receptionist.ReceptionistAgent.__init__: .NET ctor takes Options object instead of kwargs
signalwire.prefabs.survey.SurveyAgent.__init__: .NET ctor takes Options object instead of kwargs
signalwire.relay.call.Call.__init__: .NET ctor takes Options object instead of kwargs
signalwire.relay.client.RelayClient.__init__: .NET ctor takes Options object instead of kwargs
signalwire.relay.message.Message.__init__: .NET ctor takes Options object instead of kwargs
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.__init__: .NET ctor takes Options object instead of kwargs
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.__init__: .NET ctor takes Options object instead of kwargs
signalwire.skills.spider.skill.SpiderSkill.__init__: .NET ctor takes Options object instead of kwargs
signalwire.skills.weather_api.skill.WeatherApiSkill.__init__: .NET ctor takes Options object instead of kwargs

## Idiom: .NET fluent API returns this for chaining

signalwire.agent_server.AgentServer.get_agents: .NET fluent API returns this for chaining
signalwire.agent_server.AgentServer.register: .NET fluent API returns this for chaining
signalwire.agent_server.AgentServer.register_sip_username: .NET fluent API returns this for chaining
signalwire.agent_server.AgentServer.unregister: .NET fluent API returns this for chaining

## Backlog: real signature divergences (246 symbols)

These are .NET port maintenance issues — parameter renames, missing
optional parameters, type imprecisions. Each line names the first drift
symptom for triage. Follow-up program: walk this list, fix in
src/SignalWire/ where reasonable, document the residual.

signalwire.agent_server.AgentServer.__init__: BACKLOG / param-mismatch/ param[2] (port)/ type 'int' vs 'optional<int>'; default 3000 vs None
signalwire.agent_server.AgentServer.setup_sip_routing: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 1/ reference=['self', 'route', 'auto_map'] po; return-mismatch/ returns 'void' vs '
signalwire.core.agent_base.AgentBase.add_answer_verb: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 3/ reference=['self', 'config'] port=['self',
signalwire.core.agent_base.AgentBase.add_post_ai_verb: BACKLOG / param-mismatch/ param[1] (verb_name)/ name 'verb_name' vs 'verb'; param-mismatch/ param[2] (config)/ type 'dict<string,any>' vs 'any'
signalwire.core.agent_base.AgentBase.add_post_answer_verb: BACKLOG / param-mismatch/ param[1] (verb_name)/ name 'verb_name' vs 'verb'; param-mismatch/ param[2] (config)/ type 'dict<string,any>' vs 'any'
signalwire.core.agent_base.AgentBase.add_pre_answer_verb: BACKLOG / param-mismatch/ param[1] (verb_name)/ name 'verb_name' vs 'verb'; param-mismatch/ param[2] (config)/ type 'dict<string,any>' vs 'any'
signalwire.core.agent_base.AgentBase.add_swaig_query_params: BACKLOG / param-mismatch/ param[1] (params)/ name 'params' vs 'parameters'
signalwire.core.agent_base.AgentBase.enable_sip_routing: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 1/ reference=['self', 'auto_map', 'path'] por
signalwire.core.agent_base.AgentBase.on_debug_event: BACKLOG / param-mismatch/ param[1] (handler)/ name 'handler' vs 'callback'; type 'class/Callable' vs 'call; return-mismatch/ returns 'class/Callable' 
signalwire.core.agent_base.AgentBase.on_summary: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 2/ reference=['self', 'summary', 'raw_data'] ; return-mismatch/ returns 'void' vs '
signalwire.core.agent_base.AgentBase.register_sip_username: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 3/ reference=['self', 'sip_username'] port=['
signalwire.core.contexts.Context.add_enter_filler: BACKLOG / param-mismatch/ param[1] (language_code)/ name 'language_code' vs 'lang'; param-mismatch/ param[2] (fillers)/ name 'fillers' vs 'text'; type
signalwire.core.contexts.Context.add_exit_filler: BACKLOG / param-mismatch/ param[1] (language_code)/ name 'language_code' vs 'lang'; param-mismatch/ param[2] (fillers)/ name 'fillers' vs 'text'; type
signalwire.core.contexts.Context.add_step: BACKLOG / param-count-mismatch/ reference has 7 param(s), port has 3/ reference=['self', 'name', 'task', 'bullet
signalwire.core.contexts.Context.set_enter_fillers: BACKLOG / param-mismatch/ param[1] (enter_fillers)/ name 'enter_fillers' vs 'fillers'
signalwire.core.contexts.Context.set_exit_fillers: BACKLOG / param-mismatch/ param[1] (exit_fillers)/ name 'exit_fillers' vs 'fillers'
signalwire.core.contexts.ContextBuilder.validate: BACKLOG / return-mismatch/ returns 'void' vs 'list<string>'
signalwire.core.contexts.GatherInfo.add_question: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 2/ reference=['self', 'key', 'question', 'kwa
signalwire.core.contexts.Step.add_gather_question: BACKLOG / param-count-mismatch/ reference has 7 param(s), port has 2/ reference=['self', 'key', 'question', 'typ
signalwire.core.contexts.Step.set_functions: BACKLOG / param-mismatch/ param[1] (functions)/ type 'union<list<string>,string>' vs 'any'
signalwire.core.contexts.Step.set_gather_info: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 2/ reference=['self', 'output_key', 'completi
signalwire.core.contexts.Step.set_reset_consolidate: BACKLOG / param-mismatch/ param[1] (consolidate)/ name 'consolidate' vs 'c'
signalwire.core.contexts.Step.set_reset_full_reset: BACKLOG / param-mismatch/ param[1] (full_reset)/ name 'full_reset' vs 'f'
signalwire.core.contexts.Step.set_reset_system_prompt: BACKLOG / param-mismatch/ param[1] (system_prompt)/ name 'system_prompt' vs 'sp'
signalwire.core.contexts.Step.set_reset_user_prompt: BACKLOG / param-mismatch/ param[1] (user_prompt)/ name 'user_prompt' vs 'up'
signalwire.core.data_map.DataMap.description: BACKLOG / param-mismatch/ param[1] (description)/ name 'description' vs 'desc'
signalwire.core.data_map.DataMap.expression: BACKLOG / param-mismatch/ param[2] (pattern)/ type 'union<class/Pattern,string>' vs 'string'; param-mismatch/ param[3] (output)/ type 'class/signalwir
signalwire.core.data_map.DataMap.fallback_output: BACKLOG / param-mismatch/ param[1] (result)/ type 'class/signalwire.core.function_result.FunctionResult' v
signalwire.core.data_map.DataMap.foreach: BACKLOG / param-mismatch/ param[1] (foreach_config)/ name 'foreach_config' vs 'config'
signalwire.core.data_map.DataMap.output: BACKLOG / param-mismatch/ param[1] (result)/ type 'class/signalwire.core.function_result.FunctionResult' v
signalwire.core.data_map.DataMap.parameter: BACKLOG / param-mismatch/ param[2] (param_type)/ name 'param_type' vs 'type'; param-mismatch/ param[5] (enum)/ name 'enum' vs 'enum_values'
signalwire.core.data_map.DataMap.purpose: BACKLOG / param-mismatch/ param[1] (description)/ name 'description' vs 'desc'
signalwire.core.data_map.DataMap.webhook: BACKLOG / param-mismatch/ param[4] (form_param)/ type 'optional<string>' vs 'string'; default None vs ''
signalwire.core.function_result.FunctionResult.__init__: BACKLOG / param-mismatch/ param[1] (response)/ type 'optional<string>' vs 'string'; default None vs ''
signalwire.core.function_result.FunctionResult.add_action: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 2/ reference=['self', 'name', 'data'] port=['
signalwire.core.function_result.FunctionResult.add_dynamic_hints: BACKLOG / param-mismatch/ param[1] (hints)/ type 'list<union<dict<string,any>,string>>' vs 'list<any>'
signalwire.core.function_result.FunctionResult.connect: BACKLOG / param-mismatch/ param[2] (final)/ default True vs False; param-mismatch/ param[3] (from_addr)/ name 'from_addr' vs 'from'; type 'optional<st
signalwire.core.function_result.FunctionResult.create_payment_action: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 4/ reference=['action_type', 'phrase'] port=[; return-mismatch/ returns 'dict<strin
signalwire.core.function_result.FunctionResult.create_payment_parameter: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 3/ reference=['name', 'value'] port=['name', ; return-mismatch/ returns 'dict<strin
signalwire.core.function_result.FunctionResult.create_payment_prompt: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 3/ reference=['for_situation', 'actions', 'ca
signalwire.core.function_result.FunctionResult.execute_rpc: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 3/ reference=['self', 'method', 'params', 'ca
signalwire.core.function_result.FunctionResult.join_conference: BACKLOG / param-count-mismatch/ reference has 19 param(s), port has 5/ reference=['self', 'name', 'muted', 'beep
signalwire.core.function_result.FunctionResult.pay: BACKLOG / param-count-mismatch/ reference has 20 param(s), port has 6/ reference=['self', 'payment_connector_url
signalwire.core.function_result.FunctionResult.record_call: BACKLOG / param-count-mismatch/ reference has 12 param(s), port has 5/ reference=['self', 'control_id', 'stereo'
signalwire.core.function_result.FunctionResult.remove_global_data: BACKLOG / param-mismatch/ param[1] (keys)/ type 'union<list<string>,string>' vs 'list<string>'
signalwire.core.function_result.FunctionResult.remove_metadata: BACKLOG / param-mismatch/ param[1] (keys)/ type 'union<list<string>,string>' vs 'list<string>'
signalwire.core.function_result.FunctionResult.replace_in_history: BACKLOG / param-mismatch/ param[1] (text)/ type 'union<bool,string>' vs 'string'; required False vs True; 
signalwire.core.function_result.FunctionResult.rpc_ai_message: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 3/ reference=['self', 'call_id', 'message_tex
signalwire.core.function_result.FunctionResult.rpc_dial: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 6/ reference=['self', 'to_number', 'from_numb
signalwire.core.function_result.FunctionResult.send_sms: BACKLOG / param-count-mismatch/ reference has 7 param(s), port has 6/ reference=['self', 'to_number', 'from_numb
signalwire.core.function_result.FunctionResult.set_end_of_speech_timeout: BACKLOG / param-mismatch/ param[1] (milliseconds)/ name 'milliseconds' vs 'ms'
signalwire.core.function_result.FunctionResult.set_post_process: BACKLOG / param-mismatch/ param[1] (post_process)/ name 'post_process' vs 'value'
signalwire.core.function_result.FunctionResult.set_response: BACKLOG / param-mismatch/ param[1] (response)/ name 'response' vs 'text'
signalwire.core.function_result.FunctionResult.set_speech_event_timeout: BACKLOG / param-mismatch/ param[1] (milliseconds)/ name 'milliseconds' vs 'ms'
signalwire.core.function_result.FunctionResult.stop_record_call: BACKLOG / param-mismatch/ param[1] (control_id)/ type 'optional<string>' vs 'string'; default None vs ''
signalwire.core.function_result.FunctionResult.stop_tap: BACKLOG / param-mismatch/ param[1] (control_id)/ type 'optional<string>' vs 'string'; default None vs ''
signalwire.core.function_result.FunctionResult.switch_context: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 6/ reference=['self', 'system_prompt', 'user_
signalwire.core.function_result.FunctionResult.swml_transfer: BACKLOG / param-mismatch/ param[2] (ai_response)/ required True vs False; default '<absent>' vs ''; param-mismatch/ param[3] (final)/ default True vs 
signalwire.core.function_result.FunctionResult.tap: BACKLOG / param-count-mismatch/ reference has 7 param(s), port has 5/ reference=['self', 'uri', 'control_id', 'd
signalwire.core.function_result.FunctionResult.toggle_functions: BACKLOG / param-mismatch/ param[1] (function_toggles)/ name 'function_toggles' vs 'toggles'; type 'list<di
signalwire.core.function_result.FunctionResult.wait_for_user: BACKLOG / param-mismatch/ param[3] (answer_first)/ type 'bool' vs 'optional<bool>'; default False vs None
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_function_include: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 2/ reference=['self', 'url', 'functions', 'me
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_internal_filler: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 2/ reference=['self', 'function_name', 'langu
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_language: BACKLOG / param-count-mismatch/ reference has 8 param(s), port has 4/ reference=['self', 'name', 'code', 'voice'
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_pattern_hint: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 2/ reference=['self', 'hint', 'pattern', 'rep
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_pronunciation: BACKLOG / param-mismatch/ param[2] (with_text)/ name 'with_text' vs 'with'; param-mismatch/ param[3] (ignore_case)/ name 'ignore_case' vs 'ignore'; ty
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.enable_debug_events: BACKLOG / param-mismatch/ param[1] (level)/ type 'int' vs 'string'; default 1 vs 'all'
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.set_internal_fillers: BACKLOG / param-mismatch/ param[1] (internal_fillers)/ name 'internal_fillers' vs 'fillers'; type 'dict<st
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.set_native_functions: BACKLOG / param-mismatch/ param[1] (function_names)/ name 'function_names' vs 'functions'
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.set_params: BACKLOG / param-mismatch/ param[1] (params)/ name 'params' vs 'parameters'
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.set_post_prompt_llm_params: BACKLOG / param-mismatch/ param[1] (params)/ name 'params' vs 'parameters'; kind 'var_keyword' vs 'positio
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.set_prompt_llm_params: BACKLOG / param-mismatch/ param[1] (params)/ name 'params' vs 'parameters'; kind 'var_keyword' vs 'positio
signalwire.core.mixins.prompt_mixin.PromptMixin.define_contexts: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 1/ reference=['self', 'contexts'] port=['self; return-mismatch/ returns 'union<clas
signalwire.core.mixins.prompt_mixin.PromptMixin.get_prompt: BACKLOG / return-mismatch/ returns 'union<list<dict<string,any>>,string>' vs 'any'
signalwire.core.mixins.prompt_mixin.PromptMixin.prompt_add_section: BACKLOG / param-count-mismatch/ reference has 7 param(s), port has 4/ reference=['self', 'title', 'body', 'bulle
signalwire.core.mixins.prompt_mixin.PromptMixin.prompt_add_subsection: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 4/ reference=['self', 'parent_title', 'title'
signalwire.core.mixins.prompt_mixin.PromptMixin.prompt_add_to_section: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 4/ reference=['self', 'title', 'body', 'bulle
signalwire.core.mixins.skill_mixin.SkillMixin.add_skill: BACKLOG / param-mismatch/ param[1] (skill_name)/ name 'skill_name' vs 'name'; param-mismatch/ param[2] (params)/ name 'params' vs 'parameters'
signalwire.core.mixins.skill_mixin.SkillMixin.has_skill: BACKLOG / param-mismatch/ param[1] (skill_name)/ name 'skill_name' vs 'name'
signalwire.core.mixins.skill_mixin.SkillMixin.remove_skill: BACKLOG / param-mismatch/ param[1] (skill_name)/ name 'skill_name' vs 'name'
signalwire.core.mixins.web_mixin.WebMixin.manual_set_proxy_url: BACKLOG / param-mismatch/ param[1] (proxy_url)/ name 'proxy_url' vs 'url'
signalwire.core.mixins.web_mixin.WebMixin.set_dynamic_config_callback: BACKLOG / param-mismatch/ param[1] (callback)/ type 'callable<list<dict<any,any>,dict<any,any>,dict<any,an
signalwire.core.security.session_manager.SessionManager.validate_token: BACKLOG / param-mismatch/ param[1] (call_id)/ name 'call_id' vs 'function_name'; param-mismatch/ param[2] (function_name)/ name 'function_name' vs 'ca
signalwire.core.skill_base.SkillBase.agent: BACKLOG / missing-reference/ in port, not in reference
signalwire.core.skill_base.SkillBase.get_parameter_schema: BACKLOG / param-mismatch/ param[0] (cls)/ name 'cls' vs 'self'; kind 'cls' vs 'self'; return-mismatch/ returns 'dict<string,dict<string,any>>' vs 'dic
signalwire.core.skill_base.SkillBase.params: BACKLOG / missing-reference/ in port, not in reference
signalwire.core.skill_base.SkillBase.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.core.skill_base.SkillBase.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.core.skill_base.SkillBase.validate_env_vars: BACKLOG / return-mismatch/ returns 'bool' vs 'list<string>'
signalwire.core.skill_manager.SkillManager.__init__: BACKLOG / param-mismatch/ param[1] (agent)/ type 'any' vs 'class/signalwire.core.agent_base.AgentBase'
signalwire.core.skill_manager.SkillManager.get_skill: BACKLOG / param-mismatch/ param[1] (skill_identifier)/ name 'skill_identifier' vs 'key'
signalwire.core.skill_manager.SkillManager.has_skill: BACKLOG / param-mismatch/ param[1] (skill_identifier)/ name 'skill_identifier' vs 'key'
signalwire.core.skill_manager.SkillManager.load_skill: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 3/ reference=['self', 'skill_name', 'skill_cl
signalwire.core.skill_manager.SkillManager.unload_skill: BACKLOG / param-mismatch/ param[1] (skill_identifier)/ name 'skill_identifier' vs 'key'
signalwire.core.swml_service.SWMLService.extract_sip_username: BACKLOG / param-mismatch/ param[0] (request_body)/ name 'request_body' vs 'body'; type 'dict<string,any>' 
signalwire.core.swml_service.SWMLService.get_basic_auth_credentials: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 1/ reference=['self', 'include_source'] port=; return-mismatch/ returns 'union<tupl
signalwire.core.swml_service.SWMLService.register_routing_callback: BACKLOG / param-mismatch/ param[1] (callback_fn)/ name 'callback_fn' vs 'path'; type 'callable<list<class/; param-mismatch/ param[2] (path)/ name 'pat
signalwire.core.swml_service.SWMLService.tools: BACKLOG / missing-reference/ in port, not in reference
signalwire.relay.call.Call.ai: BACKLOG / param-count-mismatch/ reference has 16 param(s), port has 2/ reference=['self', 'control_id', 'agent',; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.ai_hold: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 1/ reference=['self', 'timeout', 'prompt', 'k; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.ai_message: BACKLOG / param-count-mismatch/ reference has 6 param(s), port has 2/ reference=['self', 'message_text', 'role',; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.ai_unhold: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 1/ reference=['self', 'prompt', 'kwargs'] por; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.amazon_bedrock: BACKLOG / param-count-mismatch/ reference has 8 param(s), port has 2/ reference=['self', 'prompt', 'SWAIG', 'ai_; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.answer: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 1/ reference=['self', 'kwargs'] port=['self']; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.bind_digit: BACKLOG / param-count-mismatch/ reference has 7 param(s), port has 2/ reference=['self', 'digits', 'bind_method'; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.clear_digit_bindings: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 1/ reference=['self', 'realm', 'kwargs'] port; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.collect: BACKLOG / param-count-mismatch/ reference has 11 param(s), port has 2/ reference=['self', 'digits', 'speech', 'i; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.connect: BACKLOG / param-count-mismatch/ reference has 8 param(s), port has 2/ reference=['self', 'devices', 'ringback', ; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.denoise: BACKLOG / return-mismatch/ returns 'dict<any,any>' vs 'dict<string,any>'
signalwire.relay.call.Call.denoise_stop: BACKLOG / return-mismatch/ returns 'dict<any,any>' vs 'dict<string,any>'
signalwire.relay.call.Call.detect: BACKLOG / param-count-mismatch/ reference has 6 param(s), port has 2/ reference=['self', 'detect', 'timeout', 'c; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.disconnect: BACKLOG / return-mismatch/ returns 'dict<any,any>' vs 'dict<string,any>'
signalwire.relay.call.Call.echo: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 1/ reference=['self', 'timeout', 'status_url'; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.hangup: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 1/ reference=['self', 'reason'] port=['self']; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.hold: BACKLOG / return-mismatch/ returns 'dict<any,any>' vs 'dict<string,any>'
signalwire.relay.call.Call.join_conference: BACKLOG / param-count-mismatch/ reference has 22 param(s), port has 2/ reference=['self', 'name', 'muted', 'beep; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.join_room: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 2/ reference=['self', 'name', 'status_url', '; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.leave_conference: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 1/ reference=['self', 'conference_id', 'kwarg; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.leave_room: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 1/ reference=['self', 'kwargs'] port=['self']; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.live_transcribe: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 2/ reference=['self', 'action', 'kwargs'] por; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.live_translate: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 2/ reference=['self', 'action', 'status_url',; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.on: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 2/ reference=['self', 'event_type', 'handler'; return-mismatch/ returns 'void' vs '
signalwire.relay.call.Call.pay: BACKLOG / param-count-mismatch/ reference has 22 param(s), port has 2/ reference=['self', 'payment_connector_url; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.play: BACKLOG / param-count-mismatch/ reference has 8 param(s), port has 2/ reference=['self', 'media', 'volume', 'dir; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.play_and_collect: BACKLOG / param-count-mismatch/ reference has 7 param(s), port has 2/ reference=['self', 'media', 'collect', 'vo; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.queue_enter: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 2/ reference=['self', 'queue_name', 'control_; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.queue_leave: BACKLOG / param-count-mismatch/ reference has 6 param(s), port has 1/ reference=['self', 'queue_name', 'control_; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.receive_fax: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 2/ reference=['self', 'control_id', 'on_compl; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.record: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 2/ reference=['self', 'audio', 'control_id', ; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.refer: BACKLOG / param-count-mismatch/ reference has 4 param(s), port has 2/ reference=['self', 'device', 'status_url',; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.send_digits: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 2/ reference=['self', 'digits', 'control_id']; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.send_fax: BACKLOG / param-count-mismatch/ reference has 7 param(s), port has 2/ reference=['self', 'document', 'identity',; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.stream: BACKLOG / param-count-mismatch/ reference has 12 param(s), port has 2/ reference=['self', 'url', 'name', 'codec'; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.tap: BACKLOG / param-count-mismatch/ reference has 6 param(s), port has 2/ reference=['self', 'tap', 'device', 'contr; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.transcribe: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 2/ reference=['self', 'control_id', 'status_u; return-mismatch/ returns 'class/sign
signalwire.relay.call.Call.transfer: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 2/ reference=['self', 'dest', 'kwargs'] port=; return-mismatch/ returns 'dict<any,a
signalwire.relay.call.Call.unhold: BACKLOG / return-mismatch/ returns 'dict<any,any>' vs 'dict<string,any>'
signalwire.relay.call.Call.user_event: BACKLOG / param-count-mismatch/ reference has 3 param(s), port has 2/ reference=['self', 'event', 'kwargs'] port; return-mismatch/ returns 'dict<any,a
signalwire.relay.client.RelayClient.dial: BACKLOG / param-count-mismatch/ reference has 5 param(s), port has 2/ reference=['self', 'devices', 'tag', 'max_
signalwire.relay.client.RelayClient.execute: BACKLOG / param-mismatch/ param[2] (params)/ name 'params' vs 'params_'; type 'dict<string,any>' vs 'optio; return-mismatch/ returns 'dict<any,any>' v
signalwire.relay.client.RelayClient.on_call: BACKLOG / param-mismatch/ param[1] (handler)/ name 'handler' vs 'callback'; type 'class/signalwire.relay.c; return-mismatch/ returns 'class/signalwire
signalwire.relay.client.RelayClient.on_message: BACKLOG / param-mismatch/ param[1] (handler)/ name 'handler' vs 'callback'; type 'class/signalwire.relay.c; return-mismatch/ returns 'class/signalwire
signalwire.relay.client.RelayClient.send_message: BACKLOG / param-count-mismatch/ reference has 9 param(s), port has 2/ reference=['self', 'to_number', 'from_numb
signalwire.relay.message.Message.on: BACKLOG / param-mismatch/ param[1] (handler)/ name 'handler' vs 'callback'; type 'class/Callable' vs 'call; return-mismatch/ returns 'void' vs 'class/
signalwire.relay.message.Message.result: BACKLOG / missing-reference/ in port, not in reference
signalwire.relay.message.Message.wait: BACKLOG / param-mismatch/ param[1] (timeout)/ name 'timeout' vs 'timeout_seconds'; type 'optional<float>' ; return-mismatch/ returns 'class/signalwire
signalwire.search.DocumentProcessor.__init__: BACKLOG / missing-port/ in reference, not in port
signalwire.search.IndexBuilder.__init__: BACKLOG / missing-port/ in reference, not in port
signalwire.search.SearchEngine.__init__: BACKLOG / missing-port/ in reference, not in port
signalwire.search.SearchService.__init__: BACKLOG / missing-port/ in reference, not in port
signalwire.search.preprocess_document_content: BACKLOG / missing-port/ in reference, not in port
signalwire.search.preprocess_query: BACKLOG / missing-port/ in reference, not in port
signalwire.search.search_service.SearchRequest.__init__: BACKLOG / missing-port/ in reference, not in port
signalwire.search.search_service.SearchResponse.__init__: BACKLOG / missing-port/ in reference, not in port
signalwire.search.search_service.SearchResult.__init__: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.builtin.data_sphere_serverless_skill.DataSphereServerlessSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_serverless_skill.DataSphereServerlessSkill.description: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_serverless_skill.DataSphereServerlessSkill.get_global_data: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_serverless_skill.DataSphereServerlessSkill.get_prompt_sections: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_serverless_skill.DataSphereServerlessSkill.name: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_serverless_skill.DataSphereServerlessSkill.register_tools: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_serverless_skill.DataSphereServerlessSkill.setup: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_serverless_skill.DataSphereServerlessSkill.supports_multiple_instances: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_skill.DataSphereSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_skill.DataSphereSkill.description: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_skill.DataSphereSkill.get_global_data: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_skill.DataSphereSkill.get_prompt_sections: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_skill.DataSphereSkill.name: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_skill.DataSphereSkill.register_tools: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_skill.DataSphereSkill.setup: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.data_sphere_skill.DataSphereSkill.supports_multiple_instances: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.mcp_gateway_skill.MCPGatewaySkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.mcp_gateway_skill.MCPGatewaySkill.description: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.mcp_gateway_skill.MCPGatewaySkill.get_global_data: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.mcp_gateway_skill.MCPGatewaySkill.get_hints: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.mcp_gateway_skill.MCPGatewaySkill.get_prompt_sections: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.mcp_gateway_skill.MCPGatewaySkill.name: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.mcp_gateway_skill.MCPGatewaySkill.register_tools: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.mcp_gateway_skill.MCPGatewaySkill.setup: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.swml_transfer_skill.SWMLTransferSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.swml_transfer_skill.SWMLTransferSkill.description: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.swml_transfer_skill.SWMLTransferSkill.get_hints: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.swml_transfer_skill.SWMLTransferSkill.get_prompt_sections: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.swml_transfer_skill.SWMLTransferSkill.name: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.swml_transfer_skill.SWMLTransferSkill.register_tools: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.swml_transfer_skill.SWMLTransferSkill.setup: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.builtin.swml_transfer_skill.SWMLTransferSkill.supports_multiple_instances: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.datasphere.skill.DataSphereSkill.get_global_data: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.datasphere.skill.DataSphereSkill.get_prompt_sections: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.datasphere.skill.DataSphereSkill.register_tools: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.datasphere.skill.DataSphereSkill.setup: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.get_global_data: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.get_prompt_sections: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.register_tools: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.setup: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.google_maps.skill.GoogleMapsSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.google_maps.skill.GoogleMapsSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.google_maps.skill.GoogleMapsSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.info_gatherer.skill.InfoGathererSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.info_gatherer.skill.InfoGathererSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.info_gatherer.skill.InfoGathererSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.joke.skill.JokeSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.joke.skill.JokeSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.joke.skill.JokeSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.math.skill.MathSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.math.skill.MathSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.math.skill.MathSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.get_global_data: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.get_hints: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.get_prompt_sections: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.register_tools: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.setup: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.registry.SkillRegistry.instance: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.registry.SkillRegistry.list_skills: BACKLOG / return-mismatch/ returns 'list<dict<string,string>>' vs 'list<string>'
signalwire.skills.registry.SkillRegistry.register_skill: BACKLOG / param-count-mismatch/ reference has 2 param(s), port has 3/ reference=['self', 'skill_class'] port=['s
signalwire.skills.spider.skill.SpiderSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.spider.skill.SpiderSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.get_hints: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.get_prompt_sections: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.register_tools: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.setup: BACKLOG / missing-port/ in reference, not in port
signalwire.skills.weather_api.skill.WeatherApiSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.weather_api.skill.WeatherApiSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.web_search.skill.WebSearchSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.web_search.skill.WebSearchSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.web_search.skill.WebSearchSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.get_prompt_sections: BACKLOG / return-mismatch/ returns 'list<any>' vs 'list<dict<string,any>>'
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.register_tools: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 2/ reference=['self'] port=['self', 'agent']
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.setup: BACKLOG / param-count-mismatch/ reference has 1 param(s), port has 3/ reference=['self'] port=['self', 'agent', 
signalwire.swml.verb_info.VerbInfo.<clone>$: BACKLOG / missing-reference/ in port, not in reference
signalwire.swml.verb_info.VerbInfo.__init__: BACKLOG / missing-reference/ in port, not in reference
signalwire.swml.verb_info.VerbInfo.deconstruct: BACKLOG / missing-reference/ in port, not in reference
signalwire.swml.verb_info.VerbInfo.definition: BACKLOG / missing-reference/ in port, not in reference
signalwire.swml.verb_info.VerbInfo.name: BACKLOG / missing-reference/ in port, not in reference
signalwire.swml.verb_info.VerbInfo.schema_name: BACKLOG / missing-reference/ in port, not in reference
