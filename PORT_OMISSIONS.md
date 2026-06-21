# PORT_OMISSIONS.md (signalwire-dotnet)

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
- **`signalwire.agents.bedrock.*`** — Bedrock/AmazonBedrock prefab is
  Python-only per cross-port skip rule (de-prioritised; user feedback
  flagged the underlying stack as unimpressive).
- **`signalwire.cli.build_search.*` / `dokku.*` / `init_project.*` /
  `swaig_test_wrapper.*` / `test_swaig.*` / `types.*` / `simulation.*` /
  `execution.*` / `output.*` / `core.*`** — Python-CLI internal
  scaffolding. .NET CLI is binary-based (`dotnet swaig-test`).
- **`signalwire.livewire.*`** — LiveWire integration is Python-only.
- **`signalwire.mcp_gateway.*`** — Standalone MCP gateway server is
  Python-only; .NET ships the `mcp_gateway` skill only.
- **`signalwire.pom.pom_tool.*`** — Python CLI helper for rendering a
  POM file from disk; .NET ships POM in-process only.
  (`signalwire.pom.pom` itself IS implemented at
  `src/SignalWire/POM/PromptObjectModel.cs`.)
- **`signalwire.web.web_service.*`** — Internal Python WebService class;
  .NET integrates HTTP handling on Service directly.
- **`signalwire.utils.url_validator.*`** — Internal URL/SSRF validator;
  .NET inlines equivalent checks at call sites.
- **`signalwire.utils.schema_utils.*`** — .NET ships
  `SignalWire.SWML.Schema` with the same surface (recorded in
  PORT_ADDITIONS.md).

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
  `signalwire.rest.compat.*`, `signalwire.rest.fabric.*`,
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

signalwire.add_skill_directory: Module-level skill directory loader (Python uses pkgutil); .NET registers skills in SkillRegistry directly
signalwire.agents.bedrock.BedrockAgent: Bedrock prefab is Python-only per cross-port skip rule
signalwire.agents.bedrock.BedrockAgent.__init__: Bedrock prefab is Python-only per cross-port skip rule
signalwire.agents.bedrock.BedrockAgent.__repr__: Bedrock prefab is Python-only per cross-port skip rule
signalwire.agents.bedrock.BedrockAgent.set_inference_params: Bedrock prefab is Python-only per cross-port skip rule
signalwire.agents.bedrock.BedrockAgent.set_llm_model: Bedrock prefab is Python-only per cross-port skip rule
signalwire.agents.bedrock.BedrockAgent.set_llm_temperature: Bedrock prefab is Python-only per cross-port skip rule
signalwire.agents.bedrock.BedrockAgent.set_post_prompt_llm_params: Bedrock prefab is Python-only per cross-port skip rule
signalwire.agents.bedrock.BedrockAgent.set_prompt_llm_params: Bedrock prefab is Python-only per cross-port skip rule
signalwire.agents.bedrock.BedrockAgent.set_voice: Bedrock prefab is Python-only per cross-port skip rule
signalwire.agent_server.AgentServer.register_global_routing_callback: Python helpers; .NET AgentServer uses ServeStatic and route-scoped callbacks; Run is on AgentBase
signalwire.agent_server.AgentServer.run: Python helpers; .NET AgentServer uses ServeStatic and route-scoped callbacks; Run is on AgentBase
signalwire.agent_server.AgentServer.serve_static_files: Python helpers; .NET AgentServer uses ServeStatic and route-scoped callbacks; Run is on AgentBase
signalwire.cli.build_search.console_entry_point: Python-only search CLI; .NET ships swaig-test only
signalwire.cli.build_search.main: Python-only search CLI; .NET ships swaig-test only
signalwire.cli.build_search.migrate_command: Python-only search CLI; .NET ships swaig-test only
signalwire.cli.build_search.remote_command: Python-only search CLI; .NET ships swaig-test only
signalwire.cli.build_search.search_command: Python-only search CLI; .NET ships swaig-test only
signalwire.cli.build_search.validate_command: Python-only search CLI; .NET ships swaig-test only
signalwire.cli.core.agent_loader.discover_agents_in_file: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.agent_loader.discover_services_in_file: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.agent_loader.load_agent_from_file: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.agent_loader.load_service_from_file: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.argparse_helpers.CustomArgumentParser.error: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.argparse_helpers.CustomArgumentParser.__init__: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.argparse_helpers.CustomArgumentParser.parse_args: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.argparse_helpers.CustomArgumentParser.print_usage: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.argparse_helpers.CustomArgumentParser: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.argparse_helpers.parse_function_arguments: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.dynamic_config.apply_dynamic_config: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.service_loader.discover_agents_in_file: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.service_loader.load_agent_from_file: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.service_loader.load_and_simulate_service: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.service_loader.ServiceCapture.capture: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.service_loader.ServiceCapture.__init__: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.service_loader.ServiceCapture: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.core.service_loader.simulate_request_to_service: Python-CLI internal scaffolding; .NET CLI is binary-based
signalwire.cli.dokku.cmd_config: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.cmd_deploy: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.cmd_init: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.cmd_logs: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.cmd_scale: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.Colors: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.DokkuProjectGenerator.generate: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.DokkuProjectGenerator.__init__: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.DokkuProjectGenerator: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.generate_password: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.main: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.print_error: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.print_header: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.print_step: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.print_success: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.print_warning: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.prompt: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.dokku.prompt_yes_no: Python-only Dokku project generator; .NET uses dotnet new
signalwire.cli.execution.datamap_exec.execute_datamap_function: Python-CLI execution helpers; .NET CLI is binary-based
signalwire.cli.execution.datamap_exec.simple_template_expand: Python-CLI execution helpers; .NET CLI is binary-based
signalwire.cli.execution.webhook_exec.execute_external_webhook_function: Python-CLI execution helpers; .NET CLI is binary-based
signalwire.cli.init_project.Colors: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.generate_password: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.get_agent_template: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.get_app_template: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.get_env_credentials: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.get_readme_template: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.get_test_template: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.get_web_index_template: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.main: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.mask_token: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.print_error: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.print_step: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.print_success: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.print_warning: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.ProjectGenerator.generate: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.ProjectGenerator.__init__: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.ProjectGenerator: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.prompt_multiselect: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.prompt: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.prompt_select: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.prompt_yes_no: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.run_interactive: Python-only project generator; .NET uses dotnet new
signalwire.cli.init_project.run_quick: Python-only project generator; .NET uses dotnet new
signalwire.cli.output.output_formatter.display_agent_tools: Python-CLI output helpers; .NET CLI uses Console directly
signalwire.cli.output.output_formatter.format_result: Python-CLI output helpers; .NET CLI uses Console directly
signalwire.cli.output.swml_dump.handle_dump_swml: Python-CLI output helpers; .NET CLI uses Console directly
signalwire.cli.output.swml_dump.setup_output_suppression: Python-CLI output helpers; .NET CLI uses Console directly
signalwire.cli.simulation.data_generation.adapt_for_call_type: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_generation.generate_comprehensive_post_data: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_generation.generate_fake_node_id: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_generation.generate_fake_sip_from: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_generation.generate_fake_sip_to: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_generation.generate_fake_swml_post_data: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_generation.generate_fake_uuid: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_generation.generate_minimal_post_data: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_overrides.apply_convenience_mappings: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_overrides.apply_overrides: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_overrides.parse_value: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.data_overrides.set_nested_value: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.create_mock_request: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.load_env_file: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockHeaders.__contains__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockHeaders.__getitem__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockHeaders.get: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockHeaders.__init__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockHeaders.items: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockHeaders.keys: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockHeaders: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockHeaders.values: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockQueryParams.__contains__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockQueryParams.__getitem__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockQueryParams.get: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockQueryParams.__init__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockQueryParams.items: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockQueryParams.keys: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockQueryParams: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockQueryParams.values: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockRequest.body: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockRequest.client: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockRequest.__init__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockRequest.json: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockRequest: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockURL.__init__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockURL: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.MockURL.__str__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.ServerlessSimulator.activate: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.ServerlessSimulator.add_override: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.ServerlessSimulator.deactivate: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.ServerlessSimulator.get_current_env: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.ServerlessSimulator.__init__: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.simulation.mock_env.ServerlessSimulator: Python-CLI simulation helpers; .NET CLI uses different simulation code paths
signalwire.cli.swaig_test_wrapper.main: Python-only CLI shim; .NET ships swaig-test as a binary
signalwire.cli.test_swaig.console_entry_point: Python-only entry point; .NET ships swaig-test as a binary
signalwire.cli.test_swaig.main: Python-only entry point; .NET ships swaig-test as a binary
signalwire.cli.test_swaig.print_help_examples: Python-only entry point; .NET ships swaig-test as a binary
signalwire.cli.test_swaig.print_help_platforms: Python-only entry point; .NET ships swaig-test as a binary
signalwire.cli.types.AgentInfo: Python CLI internal types; .NET CLI types are language-private
signalwire.cli.types.CallData: Python CLI internal types; .NET CLI types are language-private
signalwire.cli.types.DataMapConfig: Python CLI internal types; .NET CLI types are language-private
signalwire.cli.types.FunctionInfo: Python CLI internal types; .NET CLI types are language-private
signalwire.cli.types.PostData: Python CLI internal types; .NET CLI types are language-private
signalwire.cli.types.VarsData: Python CLI internal types; .NET CLI types are language-private
signalwire.core.agent_base.AgentBase.auto_map_sip_usernames: Python helpers; .NET exposes Name and inherits GetFullUrl from Service. AutoMapSipUsernames is recorded as a feature gap
signalwire.core.agent_base.AgentBase.get_full_url: Python helpers; .NET exposes Name and inherits GetFullUrl from Service. AutoMapSipUsernames is recorded as a feature gap
signalwire.core.agent_base.AgentBase.get_name: Python helpers; .NET exposes Name and inherits GetFullUrl from Service. AutoMapSipUsernames is recorded as a feature gap
signalwire.core.agent.prompt.manager.PromptManager.define_contexts: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.get_contexts: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.get_post_prompt: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.get_prompt: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.get_raw_prompt: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.__init__: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.prompt_add_section: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.prompt_add_subsection: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.prompt_add_to_section: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.prompt_has_section: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.set_post_prompt: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.set_prompt_pom: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.prompt.manager.PromptManager.set_prompt_text: Python's prompt sub-module; .NET integrates prompt management on AgentBase directly
signalwire.core.agent.tools.decorator.ToolDecorator.create_class_decorator: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.decorator.ToolDecorator.create_instance_decorator: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.decorator.ToolDecorator: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.registry.ToolRegistry.define_tool: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.registry.ToolRegistry.get_all_functions: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.registry.ToolRegistry.get_function: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.registry.ToolRegistry.has_function: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.registry.ToolRegistry.__init__: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.registry.ToolRegistry: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.registry.ToolRegistry.register_class_decorated_tools: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.registry.ToolRegistry.register_swaig_function: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.registry.ToolRegistry.remove_function: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.type_inference.create_typed_handler_wrapper: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.agent.tools.type_inference.infer_schema: Python's tools sub-module (decorators / type inference); .NET uses explicit DefineTool calls
signalwire.core.auth_handler.AuthHandler.flask_decorator: Internal Python auth helper; .NET inlines on Service.CheckBasicAuth
signalwire.core.auth_handler.AuthHandler.get_auth_info: Internal Python auth helper; .NET inlines on Service.CheckBasicAuth
signalwire.core.auth_handler.AuthHandler.get_fastapi_dependency: Internal Python auth helper; .NET inlines on Service.CheckBasicAuth
signalwire.core.auth_handler.AuthHandler.__init__: Internal Python auth helper; .NET inlines on Service.CheckBasicAuth
signalwire.core.auth_handler.AuthHandler: Internal Python auth helper; .NET inlines on Service.CheckBasicAuth
signalwire.core.auth_handler.AuthHandler.verify_api_key: Internal Python auth helper; .NET inlines on Service.CheckBasicAuth
signalwire.core.auth_handler.AuthHandler.verify_basic_auth: Internal Python auth helper; .NET inlines on Service.CheckBasicAuth
signalwire.core.auth_handler.AuthHandler.verify_bearer_token: Internal Python auth helper; .NET inlines on Service.CheckBasicAuth
signalwire.core.config_loader.ConfigLoader.find_config_file: Internal Python config loader; .NET reads env vars directly
signalwire.core.config_loader.ConfigLoader.get_config_file: Internal Python config loader; .NET reads env vars directly
signalwire.core.config_loader.ConfigLoader.get_config: Internal Python config loader; .NET reads env vars directly
signalwire.core.config_loader.ConfigLoader.get: Internal Python config loader; .NET reads env vars directly
signalwire.core.config_loader.ConfigLoader.get_section: Internal Python config loader; .NET reads env vars directly
signalwire.core.config_loader.ConfigLoader.has_config: Internal Python config loader; .NET reads env vars directly
signalwire.core.config_loader.ConfigLoader.__init__: Internal Python config loader; .NET reads env vars directly
signalwire.core.config_loader.ConfigLoader: Internal Python config loader; .NET reads env vars directly
signalwire.core.config_loader.ConfigLoader.merge_with_env: Internal Python config loader; .NET reads env vars directly
signalwire.core.config_loader.ConfigLoader.substitute_vars: Internal Python config loader; .NET reads env vars directly
signalwire.core.contexts.ContextBuilder.__init__: Python uses explicit __init__; .NET ContextBuilder uses default constructor on AgentBase.DefineContexts()
signalwire.core.contexts.create_simple_context: Python module-level convenience; .NET callers use AgentBase.DefineContexts().AddContext('default')
signalwire.core.data_map.create_expression_tool: Python module-level convenience builders; .NET ships static methods on DataMap with the same names
signalwire.core.data_map.create_simple_api_tool: Python module-level convenience builders; .NET ships static methods on DataMap with the same names
signalwire.core.logging_config.configure_logging: Python module-level helpers; .NET ships Logger as a class instead of free functions
signalwire.core.logging_config.get_execution_mode: Python module-level helpers; .NET ships Logger as a class instead of free functions
signalwire.core.logging_config.get_logger: Python module-level helpers; .NET ships Logger as a class instead of free functions
signalwire.core.logging_config.reset_logging_configuration: Python module-level helpers; .NET ships Logger as a class instead of free functions
signalwire.core.logging_config.strip_control_chars: Python module-level helpers; .NET ships Logger as a class instead of free functions
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.add_mcp_server: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.ai_config_mixin.AIConfigMixin.enable_mcp_server: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.auth_mixin.AuthMixin.get_basic_auth_credentials: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.auth_mixin.AuthMixin.validate_basic_auth: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.prompt_mixin.PromptMixin.contexts: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.prompt_mixin.PromptMixin.get_post_prompt: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.prompt_mixin.PromptMixin.set_prompt_pom: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.serverless_mixin.ServerlessMixin.handle_serverless_request: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.state_mixin.StateMixin.validate_tool_token: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.tool_mixin.ToolMixin.define_tool: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.tool_mixin.ToolMixin.define_tools: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.tool_mixin.ToolMixin.on_function_call: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.tool_mixin.ToolMixin.register_swaig_function: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.tool_mixin.ToolMixin.tool: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.web_mixin.WebMixin.as_router: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.web_mixin.WebMixin.enable_debug_routes: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.web_mixin.WebMixin.get_app: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.web_mixin.WebMixin.on_request: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.web_mixin.WebMixin.on_swml_request: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.web_mixin.WebMixin.register_routing_callback: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.web_mixin.WebMixin.run: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.web_mixin.WebMixin.serve: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.mixins.web_mixin.WebMixin.setup_graceful_shutdown: Python flattens 9 mixins onto AgentBase; .NET projects them via MIXIN_PROJECTIONS in enumerate_surface.py
signalwire.core.pom_builder.PomBuilder.add_section: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.add_subsection: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.add_to_section: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.from_sections: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.get_section: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.has_section: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.__init__: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.render_markdown: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.render_xml: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.to_dict: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.pom_builder.PomBuilder.to_json: Internal POM builder; .NET emits POM JSON via prompt_add_section helpers on AgentBase
signalwire.core.security_config.SecurityConfig.get_basic_auth: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig.get_cors_config: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig.get_security_headers: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig.get_ssl_context_kwargs: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig.get_url_scheme: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig.__init__: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig.load_from_env: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig.log_config: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig.should_allow_host: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security_config.SecurityConfig.validate_ssl_config: Internal Python security defaults; .NET uses CryptographicOperations.FixedTimeEquals throughout
signalwire.core.security.session_manager.SessionManager.activate_session: Python session helpers; .NET ships CreateSession + ValidateToken with equivalent semantics, other helpers folded into the same flow
signalwire.core.security.session_manager.SessionManager.create_tool_token: Python session helpers; .NET ships CreateSession + ValidateToken with equivalent semantics, other helpers folded into the same flow
signalwire.core.security.session_manager.SessionManager.debug_token: Python session helpers; .NET ships CreateSession + ValidateToken with equivalent semantics, other helpers folded into the same flow
signalwire.core.security.session_manager.SessionManager.end_session: Python session helpers; .NET ships CreateSession + ValidateToken with equivalent semantics, other helpers folded into the same flow
signalwire.core.security.session_manager.SessionManager.generate_token: Python session helpers; .NET ships CreateSession + ValidateToken with equivalent semantics, other helpers folded into the same flow
signalwire.core.security.session_manager.SessionManager.get_session_metadata: Python session helpers; .NET ships CreateSession + ValidateToken with equivalent semantics, other helpers folded into the same flow
signalwire.core.security.session_manager.SessionManager.set_session_metadata: Python session helpers; .NET ships CreateSession + ValidateToken with equivalent semantics, other helpers folded into the same flow
signalwire.core.security.session_manager.SessionManager.validate_tool_token: Python session helpers; .NET ships CreateSession + ValidateToken with equivalent semantics, other helpers folded into the same flow
signalwire.core.security.webhook_middleware.make_webhook_validation_dependency: Python ships a FastAPI dependency-factory function; .NET ships an equivalent constructable WebhookValidationMiddleware class (recorded in PORT_ADDITIONS.md) since the .NET HTTP integration is HttpListener-based, not async-FastAPI-based
signalwire.core.skill_base.SkillBase.define_tool: Python convenience method that delegates to AgentBase; .NET ships protected DefineTool() helper inside SkillBase that does the same thing (visible to subclasses, not external callers)
signalwire.core.skill_base.SkillBase.get_skill_data: Python convenience helpers; .NET inlines equivalent logic on individual skills
signalwire.core.skill_base.SkillBase.__init__: Python uses an explicit __init__; .NET SkillBase uses the protected Wire(agent, params) flow set up by SkillManager
signalwire.core.skill_base.SkillBase.update_skill_data: Python convenience helpers; .NET inlines equivalent logic on individual skills
signalwire.core.skill_base.SkillBase.validate_packages: Python convenience helpers; .NET inlines equivalent logic on individual skills
signalwire.core.skill_manager.SkillManager.list_loaded_skills: Python convenience; .NET ships ListSkills returning the same data
signalwire.core.skill_manager.SkillManager.load_skill: Python convenience; .NET ships AddSkill on AgentBase that delegates to SkillManager
signalwire.core.swaig_function.SWAIGFunction.__call__: Internal Python helper class for SWAIG function defs; .NET stores them as Dictionary<string,object>
signalwire.core.swaig_function.SWAIGFunction.execute: Internal Python helper class for SWAIG function defs; .NET stores them as Dictionary<string,object>
signalwire.core.swaig_function.SWAIGFunction.__init__: Internal Python helper class for SWAIG function defs; .NET stores them as Dictionary<string,object>
signalwire.core.swaig_function.SWAIGFunction: Internal Python helper class for SWAIG function defs; .NET stores them as Dictionary<string,object>
signalwire.core.swaig_function.SWAIGFunction.to_swaig: Internal Python helper class for SWAIG function defs; .NET stores them as Dictionary<string,object>
signalwire.core.swaig_function.SWAIGFunction.validate_args: Internal Python helper class for SWAIG function defs; .NET stores them as Dictionary<string,object>
signalwire.core.swml_builder.SWMLBuilder.add_section: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.ai: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.answer: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.build: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.__getattr__: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.hangup: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.__init__: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.play: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.render: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.reset: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_builder.SWMLBuilder.say: Internal Python builder; .NET ships Document under signalwire.swml.document (PORT_ADDITIONS)
signalwire.core.swml_handler.AIVerbHandler.build_config: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.AIVerbHandler.get_verb_name: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.AIVerbHandler: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.AIVerbHandler.validate_config: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.SWMLVerbHandler.build_config: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.SWMLVerbHandler.get_verb_name: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.SWMLVerbHandler: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.SWMLVerbHandler.validate_config: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.VerbHandlerRegistry.get_handler: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.VerbHandlerRegistry.has_handler: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.VerbHandlerRegistry.__init__: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.VerbHandlerRegistry: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_handler.VerbHandlerRegistry.register_handler: Internal Python ABC for handlers; .NET integrates handlers on Service directly
signalwire.core.swml_renderer.SwmlRenderer: Internal Python renderer; .NET renders via Document.ToDict and JsonSerializer
signalwire.core.swml_renderer.SwmlRenderer.render_function_response_swml: Internal Python renderer; .NET renders via Document.ToDict and JsonSerializer
signalwire.core.swml_renderer.SwmlRenderer.render_swml: Internal Python renderer; .NET renders via Document.ToDict and JsonSerializer
signalwire.core.swml_service.SWMLService.add_section: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.add_verb: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.add_verb_to_section: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.as_router: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.full_validation_enabled: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.__getattr__: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.get_basic_auth_credentials: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.get_document: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.manual_set_proxy_url: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.on_request: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.register_verb_handler: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.render_document: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.reset_document: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.serve: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.core.swml_service.SWMLService.stop: Python's SWMLService methods Python ships under signalwire.core.swml_service; .NET ships them under SignalWire.SWML.Service (renamed via CLASS_RENAME_MAP)
signalwire.list_skills: Module-level convenience; .NET uses SkillRegistry.Instance.ListSkills
signalwire.list_skills_with_params: Module-level convenience; .NET uses SkillRegistry.Instance.ListSkills + GetParameterSchema
signalwire.livewire.AgentHandoff.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentHandoff: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.llm_node: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.on_enter: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.on_exit: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.on_user_turn_completed: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentServer.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentServer: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentServer.rtc_session: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentSession.generate_reply: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentSession.history: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentSession.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentSession.interrupt: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.session: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentSession: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentSession.say: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentSession.start: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentSession.update_agent: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.AgentSession.userdata: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.stt_node: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.tts_node: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.update_instructions: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Agent.update_tools: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.ChatContext.append: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.ChatContext.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.ChatContext: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.function_tool: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.InferenceLLM.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.InferenceLLM: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.InferenceSTT.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.InferenceSTT: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.InferenceTTS.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.InferenceTTS: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.JobContext.connect: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.JobContext.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.JobContext: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.JobContext.wait_for_participant: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.JobProcess.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.JobProcess: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.CartesiaTTS.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.CartesiaTTS: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.DeepgramSTT.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.DeepgramSTT: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.ElevenLabsTTS.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.ElevenLabsTTS: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.OpenAILLM.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.OpenAILLM: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.SileroVAD.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.SileroVAD: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.plugins.SileroVAD.load: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.Room: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.run_app: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.RunContext.__init__: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.RunContext: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.RunContext.userdata: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.StopResponse: LiveWire integration is Python-only; not exposed cross-port
signalwire.livewire.ToolError: LiveWire integration is Python-only; not exposed cross-port
signalwire.mcp_gateway.gateway_service.main: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.gateway_service.MCPGateway.__init__: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.gateway_service.MCPGateway.run: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.gateway_service.MCPGateway.shutdown: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.gateway_service.MCPGateway: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPClient.call_method: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPClient.call_tool: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPClient.get_tools: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPClient.__init__: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPClient: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPClient.start: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPClient.stop: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPManager.create_client: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPManager.get_service: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPManager.get_service_tools: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPManager.__init__: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPManager.list_services: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPManager.shutdown: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPManager: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPManager.validate_services: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPService.__hash__: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPService.__post_init__: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.mcp_manager.MCPService: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.Session.is_alive: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.Session.is_expired: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.SessionManager.close_session: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.SessionManager.create_session: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.SessionManager.get_service_session_count: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.SessionManager.get_session: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.SessionManager.__init__: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.SessionManager.list_sessions: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.SessionManager.shutdown: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.SessionManager: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.Session: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.mcp_gateway.session_manager.Session.touch: Standalone MCP gateway server is Python-only; .NET ships the mcp_gateway skill only
signalwire.pom.pom_tool.detect_file_format: Python CLI helper for rendering a POM file from disk; .NET ships POM in-process only
signalwire.pom.pom_tool.load_pom: Python CLI helper for rendering a POM file from disk; .NET ships POM in-process only
signalwire.pom.pom_tool.main: Python CLI helper for rendering a POM file from disk; .NET ships POM in-process only
signalwire.pom.pom_tool.render_pom: Python CLI helper for rendering a POM file from disk; .NET ships POM in-process only
signalwire.prefabs.concierge.ConciergeAgent.check_availability: Registered as DefineTool callbacks (not public methods) per .NET idiom; Python registers them via decorators that produce both methods and tools
signalwire.prefabs.concierge.ConciergeAgent.get_directions: Registered as DefineTool callbacks (not public methods) per .NET idiom; Python registers them via decorators that produce both methods and tools
signalwire.prefabs.concierge.ConciergeAgent.on_summary: Inherited from AgentBase.OnSummary; .NET enumerator emits methods on the declaring class only
signalwire.prefabs.faq_bot.FAQBotAgent.on_summary: Inherited from AgentBase.OnSummary; .NET enumerator emits methods on the declaring class only
signalwire.prefabs.faq_bot.FAQBotAgent.search_faqs: Registered as DefineTool callback (not public method) per .NET idiom
signalwire.prefabs.info_gatherer.InfoGathererAgent.on_swml_request: Python override hook; .NET overrides RenderSwml on the AgentBase subclass
signalwire.prefabs.info_gatherer.InfoGathererAgent.set_question_callback: Python helper for swapping the per-step question source; .NET callers subclass InfoGathererAgent and override the question list
signalwire.prefabs.info_gatherer.InfoGathererAgent.start_questions: Registered as DefineTool callbacks (not public methods) per .NET idiom
signalwire.prefabs.info_gatherer.InfoGathererAgent.submit_answer: Registered as DefineTool callbacks (not public methods) per .NET idiom
signalwire.prefabs.receptionist.ReceptionistAgent.on_summary: Inherited from AgentBase.OnSummary; .NET enumerator emits methods on the declaring class only
signalwire.prefabs.survey.SurveyAgent.log_response: Registered as DefineTool callbacks (not public methods) per .NET idiom
signalwire.prefabs.survey.SurveyAgent.on_summary: Inherited from AgentBase.OnSummary; .NET enumerator emits methods on the declaring class only
signalwire.prefabs.survey.SurveyAgent.validate_response: Registered as DefineTool callbacks (not public methods) per .NET idiom
signalwire.register_skill: Module-level convenience; .NET uses SkillRegistry.Instance.RegisterSkill
signalwire.relay.call.Action: Action class lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.Action.__init__: Action constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.Action.is_done: Python @property; .NET exposes IsDone as a public property
signalwire.relay.call.Action.wait: Python uses wait/WaitAsync overloads; .NET ships Action.WaitAsync explicitly
signalwire.relay.call.AIAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.AIAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.AIAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.Call.pass_: Python uses `pass_` to avoid keyword clash; .NET has no clash and exposes Pass on Call
signalwire.relay.call.Call.__repr__: Python __repr__ helper; .NET uses ToString instead
signalwire.relay.call.Call.wait_for_ended: Python utility; .NET uses Action.WaitAsync / Call.OnEventCallback patterns
signalwire.relay.call.Call.wait_for: Python utility; .NET uses Action.WaitAsync / Call.OnEventCallback patterns
signalwire.relay.call.CollectAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.CollectAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.CollectAction.start_input_timers: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.CollectAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.CollectAction.volume: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.DetectAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.DetectAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.DetectAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.FaxAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.FaxAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.FaxAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PayAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.PayAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.PayAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PlayAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.PlayAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.PlayAction.pause: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PlayAction.resume: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PlayAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PlayAction.volume: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.RecordAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.RecordAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.RecordAction.pause: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.RecordAction.resume: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.RecordAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.StandaloneCollectAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.StandaloneCollectAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.StandaloneCollectAction.start_input_timers: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.StandaloneCollectAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.StreamAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.StreamAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.StreamAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.TapAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.TapAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.TapAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.TranscribeAction: Action subclass lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call (recorded in PORT_ADDITIONS.md)
signalwire.relay.call.TranscribeAction.__init__: Action subclass constructor lives under SignalWire.Relay in .NET; Python lists it under signalwire.relay.call
signalwire.relay.call.TranscribeAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.client.RelayClient.__aenter__: Python async-context-manager helper; .NET uses IDisposable / explicit DisconnectAsync
signalwire.relay.client.RelayClient.__aexit__: Python async-context-manager helper; .NET uses IDisposable / explicit DisconnectAsync
signalwire.relay.client.RelayClient.__del__: Python finalizer; .NET uses Dispose pattern (not part of public surface)
signalwire.relay.client.RelayClient.relay_protocol: Python @property accessor; .NET exposes Protocol as a public property
signalwire.relay.client.RelayError.__init__: Python relay error class; .NET surfaces RPC errors as InvalidOperationException
signalwire.relay.client.RelayError: Python relay error class; .NET surfaces RPC errors as InvalidOperationException
signalwire.relay.event.CallingErrorEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.CallingErrorEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.CallReceiveEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.CallReceiveEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.CallStateEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.CallStateEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.CollectEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.CollectEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.ConferenceEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.ConferenceEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.ConnectEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.ConnectEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.DenoiseEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.DenoiseEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.DetectEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.DetectEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.DialEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.DialEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.EchoEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.EchoEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.FaxEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.FaxEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.HoldEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.HoldEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.MessageReceiveEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.MessageReceiveEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.MessageStateEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.MessageStateEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.parse_event: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.PayEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.PayEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.PlayEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.PlayEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.QueueEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.QueueEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.RecordEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.RecordEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.ReferEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.ReferEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.RelayEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.RelayEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.SendDigitsEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.SendDigitsEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.StreamEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.StreamEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.TapEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.TapEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.TranscribeEvent.from_payload: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.event.TranscribeEvent: Python relay event helpers; .NET ships Event class with same surface (recorded in PORT_ADDITIONS.md)
signalwire.relay.message.Message.is_done: Python @property; .NET exposes IsDone as a public property
signalwire.relay.message.Message.__repr__: Python __repr__ helper; .NET uses ToString instead (Object override, not surface)
signalwire.rest._base.BaseResource.__init__: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.BaseResource: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.CrudResource.create: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.CrudResource.delete: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.CrudResource.get: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.CrudResource: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.CrudResource.list: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.CrudResource.update: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.CrudWithAddresses: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.CrudWithAddresses.list_addresses: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.HttpClient.delete: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.HttpClient.get: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.HttpClient.__init__: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.HttpClient: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.HttpClient.patch: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.HttpClient.post: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.HttpClient.put: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.SignalWireRestError.__init__: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.SignalWireRestError: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest.call_handler.PhoneCallHandler: Phone-binding helper; .NET inlines the wire values on PhoneNumbers helpers (recorded in PORT_ADDITIONS.md)
signalwire.RestClient: Module-level re-export in Python; .NET ships SignalWire.REST.RestClient
signalwire.rest.client.RestClient.__init__: Python module-level RestClient ctor; .NET ships RestClient under SignalWire.REST.RestClient
signalwire.rest.client.RestClient: Python module-level RestClient ctor; .NET ships RestClient under SignalWire.REST.RestClient
signalwire.rest.namespaces.addresses.AddressesResource.create: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.addresses.AddressesResource.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.addresses.AddressesResource.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.addresses.AddressesResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.addresses.AddressesResource.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.addresses.AddressesResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.ai_hold: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.ai_message: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.ai_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.ai_unhold: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.collect: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.collect_start_input_timers: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.collect_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.denoise: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.denoise_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.detect: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.detect_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.dial: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.disconnect: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.end: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.live_transcribe: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.live_translate: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.play_pause: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.play: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.play_resume: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.play_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.play_volume: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.receive_fax_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.record_pause: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.record: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.record_resume: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.record_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.refer: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.send_fax_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.stream: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.stream_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.tap: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.tap_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.transcribe: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.transcribe_stop: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.transfer: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.calling.CallingNamespace.user_event: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.chat.ChatResource.create_token: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.chat.ChatResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.chat.ChatResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatAccounts.create: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatAccounts.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatAccounts.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatAccounts.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatAccounts: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatAccounts.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatApplications: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatApplications.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatCalls: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatCalls.start_recording: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatCalls.start_stream: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatCalls.stop_stream: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatCalls.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatCalls.update_recording: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.delete_recording: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.get_participant: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.get_recording: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.list_participants: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.list_recordings: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.remove_participant: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.start_stream: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.stop_stream: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.update_participant: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatConferences.update_recording: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatFaxes.delete_media: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatFaxes.get_media: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatFaxes.list_media: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatFaxes: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatFaxes.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatLamlBins: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatLamlBins.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatMessages.delete_media: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatMessages.get_media: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatMessages.list_media: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatMessages: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatMessages.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatNamespace.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatNamespace: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.import_number: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.list_available_countries: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.purchase: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.search_local: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.search_toll_free: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatPhoneNumbers.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatQueues.dequeue_member: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatQueues.get_member: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatQueues.list_members: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatQueues: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatQueues.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatRecordings.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatRecordings.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatRecordings.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatRecordings: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatTokens.create: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatTokens.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatTokens: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatTokens.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatTranscriptions.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatTranscriptions.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatTranscriptions.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.compat.CompatTranscriptions: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.datasphere.DatasphereDocuments.delete_chunk: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.datasphere.DatasphereDocuments.get_chunk: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.datasphere.DatasphereDocuments.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.datasphere.DatasphereDocuments.list_chunks: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.datasphere.DatasphereDocuments: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.datasphere.DatasphereDocuments.search: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.datasphere.DatasphereNamespace.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.datasphere.DatasphereNamespace: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.AutoMaterializedWebhook.create: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.AutoMaterializedWebhook: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.CallFlowsResource.deploy_version: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.CallFlowsResource.list_addresses: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.CallFlowsResource.list_versions: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.CallFlowsResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.ConferenceRoomsResource.list_addresses: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.ConferenceRoomsResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.CxmlApplicationsResource.create: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.CxmlApplicationsResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.CxmlWebhooksResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricAddresses.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricAddresses.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricAddresses: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricNamespace.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricNamespace: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricResourcePUT: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricTokens.create_embed_token: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricTokens.create_guest_token: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricTokens.create_invite_token: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricTokens.create_subscriber_token: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricTokens.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricTokens: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.FabricTokens.refresh_subscriber_token: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.GenericResources.assign_domain_application: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.GenericResources.assign_phone_route: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.GenericResources.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.GenericResources.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.GenericResources.list_addresses: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.GenericResources.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.GenericResources: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.SubscribersResource.create_sip_endpoint: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.SubscribersResource.delete_sip_endpoint: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.SubscribersResource.get_sip_endpoint: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.SubscribersResource.list_sip_endpoints: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.SubscribersResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.SubscribersResource.update_sip_endpoint: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.fabric.SwmlWebhooksResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.imported_numbers.ImportedNumbersResource.create: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.imported_numbers.ImportedNumbersResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.imported_numbers.ImportedNumbersResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.ConferenceLogs.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.ConferenceLogs: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.FaxLogs.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.FaxLogs.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.FaxLogs: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.LogsNamespace.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.LogsNamespace: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.MessageLogs.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.MessageLogs.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.MessageLogs: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.VoiceLogs.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.VoiceLogs.list_events: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.VoiceLogs.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.logs.VoiceLogs: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.lookup.LookupResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.lookup.LookupResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.lookup.LookupResource.phone_number: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.mfa.MfaResource.call: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.mfa.MfaResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.mfa.MfaResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.mfa.MfaResource.sms: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.mfa.MfaResource.verify: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.number_groups.NumberGroupsResource.add_membership: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.number_groups.NumberGroupsResource.delete_membership: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.number_groups.NumberGroupsResource.get_membership: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.number_groups.NumberGroupsResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.number_groups.NumberGroupsResource.list_memberships: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.number_groups.NumberGroupsResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource.search: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource.set_ai_agent: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource.set_call_flow: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource.set_cxml_application: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource.set_cxml_webhook: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource.set_relay_application: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource.set_relay_topic: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource.set_swml_webhook: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.project.ProjectNamespace.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.project.ProjectNamespace: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.project.ProjectTokens.create: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.project.ProjectTokens.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.project.ProjectTokens.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.project.ProjectTokens: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.project.ProjectTokens.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.pubsub.PubSubResource.create_token: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.pubsub.PubSubResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.pubsub.PubSubResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.queues.QueuesResource.get_member: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.queues.QueuesResource.get_next_member: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.queues.QueuesResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.queues.QueuesResource.list_members: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.queues.QueuesResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.recordings.RecordingsResource.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.recordings.RecordingsResource.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.recordings.RecordingsResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.recordings.RecordingsResource.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.recordings.RecordingsResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryBrands.create_campaign: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryBrands.create: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryBrands.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryBrands.list_campaigns: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryBrands.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryBrands: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryCampaigns.create_order: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryCampaigns.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryCampaigns.list_numbers: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryCampaigns.list_orders: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryCampaigns: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryCampaigns.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryNamespace.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryNamespace: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryNumbers.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryNumbers: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryOrders.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.registry.RegistryOrders: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.short_codes.ShortCodesResource.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.short_codes.ShortCodesResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.short_codes.ShortCodesResource.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.short_codes.ShortCodesResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.short_codes.ShortCodesResource.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.sip_profile.SipProfileResource.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.sip_profile.SipProfileResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.sip_profile.SipProfileResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.sip_profile.SipProfileResource.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.verified_callers.VerifiedCallersResource.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.verified_callers.VerifiedCallersResource: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.verified_callers.VerifiedCallersResource.redial_verification: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.verified_callers.VerifiedCallersResource.submit_verification: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoConferences.create_stream: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoConferences.list_conference_tokens: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoConferences.list_streams: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoConferences: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoConferenceTokens.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoConferenceTokens: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoConferenceTokens.reset: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoNamespace.__init__: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoNamespace: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomRecordings.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomRecordings.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomRecordings.list_events: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomRecordings.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomRecordings: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRooms.create_stream: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomSessions.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomSessions.list_events: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomSessions.list_members: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomSessions.list: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomSessions.list_recordings: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomSessions: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRooms.list_streams: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRooms: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomTokens.create: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoRoomTokens: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoStreams.delete: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoStreams.get: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoStreams: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest.namespaces.video.VideoStreams.update: Per-namespace helpers Python pre-shaped; .NET groups them under SignalWire.REST.Namespaces.Calling/Fabric (recorded in PORT_ADDITIONS.md)
signalwire.rest._pagination.PaginatedIterator.__init__: Python pagination iterator class; .NET callers paginate by repeated CrudResource.List(page=...) calls
signalwire.rest._pagination.PaginatedIterator.__iter__: Python pagination iterator class; .NET callers paginate by repeated CrudResource.List(page=...) calls
signalwire.rest._pagination.PaginatedIterator.__next__: Python pagination iterator class; .NET callers paginate by repeated CrudResource.List(page=...) calls
signalwire.rest._pagination.PaginatedIterator: Python pagination iterator class; .NET callers paginate by repeated CrudResource.List(page=...) calls
signalwire.search.document_processor.DocumentProcessor.create_chunks: search subsystem; not ported per skip list
signalwire.search.document_processor.DocumentProcessor.__init__: search subsystem; not ported per skip list
signalwire.search.document_processor.DocumentProcessor: search subsystem; not ported per skip list
signalwire.search.index_builder.IndexBuilder.build_index_from_sources: search subsystem; not ported per skip list
signalwire.search.index_builder.IndexBuilder.build_index: search subsystem; not ported per skip list
signalwire.search.index_builder.IndexBuilder.__init__: search subsystem; not ported per skip list
signalwire.search.index_builder.IndexBuilder: search subsystem; not ported per skip list
signalwire.search.index_builder.IndexBuilder.validate_index: search subsystem; not ported per skip list
signalwire.search.migration.SearchIndexMigrator.get_index_info: search subsystem; not ported per skip list
signalwire.search.migration.SearchIndexMigrator.__init__: search subsystem; not ported per skip list
signalwire.search.migration.SearchIndexMigrator.migrate_pgvector_to_sqlite: search subsystem; not ported per skip list
signalwire.search.migration.SearchIndexMigrator.migrate_sqlite_to_pgvector: search subsystem; not ported per skip list
signalwire.search.migration.SearchIndexMigrator: search subsystem; not ported per skip list
signalwire.search.models.resolve_model_alias: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorBackend.close: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorBackend.create_schema: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorBackend.delete_collection: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorBackend.get_stats: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorBackend.__init__: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorBackend.list_collections: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorBackend: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorBackend.store_chunks: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorSearchBackend.close: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorSearchBackend.fetch_candidates: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorSearchBackend.get_stats: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorSearchBackend.__init__: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorSearchBackend.search: search subsystem; not ported per skip list
signalwire.search.pgvector_backend.PgVectorSearchBackend: search subsystem; not ported per skip list
signalwire.search.query_processor.detect_language: search subsystem; not ported per skip list
signalwire.search.query_processor.ensure_nltk_resources: search subsystem; not ported per skip list
signalwire.search.query_processor.get_synonyms: search subsystem; not ported per skip list
signalwire.search.query_processor.get_wordnet_pos: search subsystem; not ported per skip list
signalwire.search.query_processor.load_spacy_model: search subsystem; not ported per skip list
signalwire.search.query_processor.preprocess_document_content: search subsystem; not ported per skip list
signalwire.search.query_processor.preprocess_query: search subsystem; not ported per skip list
signalwire.search.query_processor.remove_duplicate_words: search subsystem; not ported per skip list
signalwire.search.query_processor.set_global_model: search subsystem; not ported per skip list
signalwire.search.query_processor.vectorize_query: search subsystem; not ported per skip list
signalwire.search.search_engine.SearchEngine.get_stats: search subsystem; not ported per skip list
signalwire.search.search_engine.SearchEngine.__init__: search subsystem; not ported per skip list
signalwire.search.search_engine.SearchEngine.search: search subsystem; not ported per skip list
signalwire.search.search_engine.SearchEngine: search subsystem; not ported per skip list
signalwire.search.search_service.SearchService.__init__: search subsystem; not ported per skip list
signalwire.search.search_service.SearchService.search_direct: search subsystem; not ported per skip list
signalwire.search.search_service.SearchService: search subsystem; not ported per skip list
signalwire.search.search_service.SearchService.start: search subsystem; not ported per skip list
signalwire.search.search_service.SearchService.stop: search subsystem; not ported per skip list
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.get_instance_key: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.get_tools: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.api_ninjas_trivia.skill.ApiNinjasTriviaSkill.__init__: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.get_instance_key: Internal Python helpers under claude_skills; .NET inlines on ClaudeSkillsSkill
signalwire.skills.claude_skills.skill.ClaudeSkillsSkill.get_parameter_schema: Internal Python helpers under claude_skills; .NET inlines on ClaudeSkillsSkill
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.get_hints: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.get_instance_key: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datasphere_serverless.skill.DataSphereServerlessSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datasphere.skill.DataSphereSkill.cleanup: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datasphere.skill.DataSphereSkill.get_hints: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datasphere.skill.DataSphereSkill.get_instance_key: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datasphere.skill.DataSphereSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datetime.skill.DateTimeSkill: Class capitalisation differs in Python (DateTimeSkill) vs .NET (DatetimeSkill); .NET emits the .NET form (PORT_ADDITIONS lists the .NET name)
signalwire.skills.datetime.skill.DateTimeSkill.get_hints: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datetime.skill.DateTimeSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datetime.skill.DateTimeSkill.get_prompt_sections: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datetime.skill.DateTimeSkill.register_tools: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.datetime.skill.DateTimeSkill.setup: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.google_maps.skill.GoogleMapsClient.compute_route: Internal Python helper class; .NET inlines equivalent calls on GoogleMapsSkill
signalwire.skills.google_maps.skill.GoogleMapsClient.__init__: Internal Python helper class; .NET inlines equivalent calls on GoogleMapsSkill
signalwire.skills.google_maps.skill.GoogleMapsClient: Internal Python helper class; .NET inlines equivalent calls on GoogleMapsSkill
signalwire.skills.google_maps.skill.GoogleMapsClient.validate_address: Internal Python helper class; .NET inlines equivalent calls on GoogleMapsSkill
signalwire.skills.google_maps.skill.GoogleMapsSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.info_gatherer.skill.InfoGathererSkill.get_instance_key: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.info_gatherer.skill.InfoGathererSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.joke.skill.JokeSkill.get_hints: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.joke.skill.JokeSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.math.skill.MathSkill.get_hints: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.math.skill.MathSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.get_parameter_schema: Internal MCP gateway helpers; .NET inlines on McpGatewaySkill
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.cleanup: native vector search is part of the search subsystem; not ported per skip list
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.get_global_data: native vector search is part of the search subsystem; not ported per skip list
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.get_instance_key: native vector search is part of the search subsystem; not ported per skip list
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.get_parameter_schema: native vector search is part of the search subsystem; not ported per skip list
signalwire.skills.native_vector_search.skill.NativeVectorSearchSkill.get_prompt_sections: native vector search is part of the search subsystem; not ported per skip list
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.get_instance_key: Internal helper; .NET inlines on PlayBackgroundFileSkill
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.get_parameter_schema: Internal helper; .NET inlines on PlayBackgroundFileSkill
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.get_tools: Internal helper; .NET inlines on PlayBackgroundFileSkill
signalwire.skills.play_background_file.skill.PlayBackgroundFileSkill.__init__: Internal helper; .NET inlines on PlayBackgroundFileSkill
signalwire.skills.registry.SkillRegistry.add_skill_directory: Python registry helpers; .NET ships SkillRegistry with equivalent methods
signalwire.skills.registry.SkillRegistry.discover_skills: Python registry helpers; .NET ships SkillRegistry with equivalent methods
signalwire.skills.registry.SkillRegistry.get_all_skills_schema: Python registry helpers; .NET ships SkillRegistry with equivalent methods
signalwire.skills.registry.SkillRegistry.get_skill_class: Python registry helpers; .NET ships SkillRegistry with equivalent methods
signalwire.skills.registry.SkillRegistry.__init__: Python registry helpers; .NET ships SkillRegistry with equivalent methods
signalwire.skills.registry.SkillRegistry.list_all_skill_sources: Python registry helpers; .NET ships SkillRegistry with equivalent methods
signalwire.skills.spider.skill.SpiderSkill.cleanup: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.spider.skill.SpiderSkill.get_instance_key: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.spider.skill.SpiderSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.spider.skill.SpiderSkill.__init__: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.get_instance_key: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.swml_transfer.skill.SWMLTransferSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.weather_api.skill.WeatherApiSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.weather_api.skill.WeatherApiSkill.get_tools: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.weather_api.skill.WeatherApiSkill.__init__: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.web_search.skill.GoogleSearchScraper.extract_html_content: Internal Python scraper class; .NET inlines equivalent functionality on WebSearchSkill
signalwire.skills.web_search.skill.GoogleSearchScraper.extract_reddit_content: Internal Python scraper class; .NET inlines equivalent functionality on WebSearchSkill
signalwire.skills.web_search.skill.GoogleSearchScraper.extract_text_from_url: Internal Python scraper class; .NET inlines equivalent functionality on WebSearchSkill
signalwire.skills.web_search.skill.GoogleSearchScraper.__init__: Internal Python scraper class; .NET inlines equivalent functionality on WebSearchSkill
signalwire.skills.web_search.skill.GoogleSearchScraper: Internal Python scraper class; .NET inlines equivalent functionality on WebSearchSkill
signalwire.skills.web_search.skill.GoogleSearchScraper.is_reddit_url: Internal Python scraper class; .NET inlines equivalent functionality on WebSearchSkill
signalwire.skills.web_search.skill.GoogleSearchScraper.search_and_scrape_best: Internal Python scraper class; .NET inlines equivalent functionality on WebSearchSkill
signalwire.skills.web_search.skill.GoogleSearchScraper.search_and_scrape: Internal Python scraper class; .NET inlines equivalent functionality on WebSearchSkill
signalwire.skills.web_search.skill.GoogleSearchScraper.search_google: Internal Python scraper class; .NET inlines equivalent functionality on WebSearchSkill
signalwire.skills.web_search.skill_improved.GoogleSearchScraper.extract_text_from_url: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.GoogleSearchScraper.__init__: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.GoogleSearchScraper: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.GoogleSearchScraper.search_and_scrape_best: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.GoogleSearchScraper.search_and_scrape: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.GoogleSearchScraper.search_google: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.WebSearchSkill.get_global_data: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.WebSearchSkill.get_hints: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.WebSearchSkill.get_instance_key: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.WebSearchSkill.get_parameter_schema: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.WebSearchSkill.get_prompt_sections: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.WebSearchSkill: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.WebSearchSkill.register_tools: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_improved.WebSearchSkill.setup: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.GoogleSearchScraper.extract_text_from_url: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.GoogleSearchScraper.__init__: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.GoogleSearchScraper: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.GoogleSearchScraper.search_and_scrape: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.GoogleSearchScraper.search_google: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.WebSearchSkill.get_global_data: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.WebSearchSkill.get_hints: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.WebSearchSkill.get_instance_key: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.WebSearchSkill.get_parameter_schema: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.WebSearchSkill.get_prompt_sections: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.WebSearchSkill: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.WebSearchSkill.register_tools: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill_original.WebSearchSkill.setup: Python-experimental skill variants; .NET ships canonical skill only
signalwire.skills.web_search.skill.WebSearchSkill.get_hints: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.web_search.skill.WebSearchSkill.get_instance_key: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.web_search.skill.WebSearchSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.get_hints: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.get_parameter_schema: Inherited from SkillBase; .NET enumerator emits methods on the declaring class only — these resolve via base-class inheritance at runtime (recorded in PORT_ADDITIONS.md as port-only convention)
signalwire.skills.wikipedia_search.skill.WikipediaSearchSkill.search_wiki: Python module-level convenience; .NET defines search_wiki as the registered tool name (not a class method)
signalwire.utils.is_serverless_mode: Detection helper; .NET ships SignalWire.Serverless.Adapter.Detect under a different module path
signalwire.utils.schema_utils.SchemaUtils.full_validation_available: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.generate_method_body: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.generate_method_signature: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.get_all_verb_names: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.get_verb_parameters: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.get_verb_properties: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.get_verb_required_properties: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.__init__: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.load_schema: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.validate_document: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaUtils.validate_verb: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaValidationError.__init__: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.schema_utils.SchemaValidationError: Internal SWML schema helpers; .NET ships SignalWire.SWML.Schema with the same surface (recorded in PORT_ADDITIONS.md)
signalwire.utils.url_validator.validate_url: Internal URL/SSRF validator; .NET inlines equivalent checks at call sites
signalwire.web.web_service.WebService.add_directory: Internal Python WebService class; .NET integrates HTTP handling on Service directly
signalwire.web.web_service.WebService.__init__: Internal Python WebService class; .NET integrates HTTP handling on Service directly
signalwire.web.web_service.WebService: Internal Python WebService class; .NET integrates HTTP handling on Service directly
signalwire.web.web_service.WebService.remove_directory: Internal Python WebService class; .NET integrates HTTP handling on Service directly
signalwire.web.web_service.WebService.start: Internal Python WebService class; .NET integrates HTTP handling on Service directly
signalwire.web.web_service.WebService.stop: Internal Python WebService class; .NET integrates HTTP handling on Service directly
signalwire.agent_server.AgentServer.app: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
signalwire.core.skill_base.SkillBase.logger: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
signalwire.core.swml_service.SWMLService.schema_utils: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
signalwire.core.swml_service.SWMLService.security: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
signalwire.core.swml_service.SWMLService.verb_registry: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
signalwire.core.security.webhook_validator.validate_webhook_signature: idiomatic_divergence: implemented as static method on the WebhookValidator class (language idiom); see PORT_ADDITIONS.md
signalwire.core.security.webhook_validator.validate_request: idiomatic_divergence: implemented as static method on the WebhookValidator class (language idiom); see PORT_ADDITIONS.md
signalwire.core.security.security_utils.filter_sensitive_headers: idiomatic_divergence: implemented as static method SecurityUtils.FilterSensitiveHeaders (language idiom); see PORT_ADDITIONS.md
signalwire.core.security.security_utils.redact_url: idiomatic_divergence: implemented as static method SecurityUtils.RedactUrl (language idiom); see PORT_ADDITIONS.md
signalwire.core.security.security_utils.is_valid_hostname: idiomatic_divergence: implemented as static method SecurityUtils.IsValidHostname (language idiom); see PORT_ADDITIONS.md
