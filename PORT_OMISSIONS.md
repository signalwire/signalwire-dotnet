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
signalwire.livewire.AgentHandoff.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentHandoff: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.llm_node: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.on_enter: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.on_exit: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.on_user_turn_completed: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentServer.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentServer: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentServer.rtc_session: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentSession.generate_reply: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentSession.history: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentSession.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentSession.interrupt: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.session: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentSession: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentSession.say: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentSession.start: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentSession.update_agent: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.AgentSession.userdata: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.stt_node: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.tts_node: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.update_instructions: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Agent.update_tools: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.ChatContext.append: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.ChatContext.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.ChatContext: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.function_tool: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.InferenceLLM.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.InferenceLLM: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.InferenceSTT.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.InferenceSTT: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.InferenceTTS.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.InferenceTTS: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.JobContext.connect: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.JobContext.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.JobContext: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.JobContext.wait_for_participant: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.JobProcess.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.JobProcess: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.CartesiaTTS.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.CartesiaTTS: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.DeepgramSTT.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.DeepgramSTT: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.ElevenLabsTTS.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.ElevenLabsTTS: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.OpenAILLM.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.OpenAILLM: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.SileroVAD.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.SileroVAD: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.plugins.SileroVAD.load: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.Room: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.run_app: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.RunContext.__init__: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.RunContext: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.RunContext.userdata: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.StopResponse: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
signalwire.livewire.ToolError: approved: livewire is LiveKit-agents-compat; LiveKit ships no .NET agents SDK (only Python + Node/TS), so it is not ported to .NET — invented surface otherwise (user, 2026-07)
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
signalwire.relay.call.AIAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.CollectAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.CollectAction.volume: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.DetectAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.FaxAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PayAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PlayAction.pause: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PlayAction.resume: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PlayAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.PlayAction.volume: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.RecordAction.pause: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.RecordAction.resume: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.RecordAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.StandaloneCollectAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.StreamAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.TapAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.call.TranscribeAction.stop: Action subclass methods live under SignalWire.Relay in .NET; Python lists them under signalwire.relay.call
signalwire.relay.client.RelayClient.__aenter__: impossible: Python async-context-manager protocol dunder; C# uses IAsyncDisposable / await using on the client instead (TS/PHP omit likewise)
signalwire.relay.client.RelayClient.__aexit__: impossible: Python async-context-manager protocol dunder; C# uses IAsyncDisposable / await using on the client instead (TS/PHP omit likewise)
signalwire.relay.client.RelayClient.__del__: impossible: Python finalizer dunder; C# uses IAsyncDisposable/Dispose deterministic cleanup instead (TS/PHP omit likewise)
signalwire.rest._base.CrudResource.get: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest._base.CrudResource.list: Internal Python REST base; .NET ships HttpClient/CrudResource directly under SignalWire.REST
signalwire.rest.call_handler.PhoneCallHandler: Phone-binding helper; .NET inlines the wire values on PhoneNumbers helpers (recorded in PORT_ADDITIONS.md)
signalwire.rest._pagination.PaginatedIterator.__iter__: impossible: Python iterator-protocol dunder; C# PaginatedIterator implements IAsyncEnumerable (await foreach) instead — no __iter__/__next__ equivalent (TS/PHP omit likewise)
signalwire.rest._pagination.PaginatedIterator.__next__: impossible: Python iterator-protocol dunder; C# PaginatedIterator implements IAsyncEnumerable (await foreach) instead — no __iter__/__next__ equivalent (TS/PHP omit likewise)
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
signalwire.skills.google_maps.skill.GoogleMapsClient.compute_route: Internal Python helper class; .NET inlines equivalent calls on GoogleMapsSkill
signalwire.skills.google_maps.skill.GoogleMapsClient.__init__: Internal Python helper class; .NET inlines equivalent calls on GoogleMapsSkill
signalwire.skills.google_maps.skill.GoogleMapsClient: Internal Python helper class; .NET inlines equivalent calls on GoogleMapsSkill
signalwire.skills.google_maps.skill.GoogleMapsClient.validate_address: Internal Python helper class; .NET inlines equivalent calls on GoogleMapsSkill
signalwire.skills.mcp_gateway.skill.MCPGatewaySkill.get_parameter_schema: Internal MCP gateway helpers; .NET inlines on McpGatewaySkill
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
signalwire.agent_server.AgentServer.app: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
signalwire.core.skill_base.SkillBase.logger: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
signalwire.core.swml_service.SWMLService.schema_utils: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
signalwire.core.swml_service.SWMLService.security: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
signalwire.core.swml_service.SWMLService.verb_registry: .NET keeps this as private/internal state; Python exposes it as a @property accessor for introspection
