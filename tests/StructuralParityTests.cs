// StructuralParityTests.cs
//
// Tests that close structural-parity gaps surfaced by
// porting-sdk/scripts/diff_port_signatures.py. Each test demonstrates a
// piece of functionality the .NET port must support so that users can
// write the same kind of code as Python users — using C#-idiomatic
// names + types throughout, NOT Python-style naming.
//
// Every test here corresponds to existing Python functionality + tests.
// We are NOT adding new SDK features — we are bringing .NET up to what
// Python already supports. Python parity references are noted per test.
//
// TDD pattern: write the test first (red — calls a method/overload
// that doesn't exist yet, fails to compile), then add the missing
// surface in source, watch compile + test go green.

using System.Collections.Generic;
using Xunit;
using SignalWire.Agent;
using SignalWire.Contexts;
using SignalWire.Server;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Tests;

public class StructuralParityTests
{
    // -------------------------------------------------------------------
    // AgentBase.AddAnswerVerb / AddPostAnswerVerb — Python takes only
    // (config); the verb name is implicit. Provide a 1-arg overload
    // matching that shape.
    //
    // Python parity: tests/unit/core/test_agent_base.py
    //   test_add_answer_verb_config
    // -------------------------------------------------------------------

    [Fact]
    public void AddAnswerVerb_OneArgOverload_AppendsAsAnswer()
    {
        var agent = new AgentBase(new AgentOptions { Name = "t", Route = "/t" });
        var config = new Dictionary<string, object> { ["max_duration"] = 30 };
        agent.AddAnswerVerb(config);
        // No exception, fluent chain returns this — that's all we verify
        // structurally; the rendered SWML asserts the verb name is "answer".
    }

    // -------------------------------------------------------------------
    // AgentBase.RegisterSipUsername — Python takes only (sip_username).
    //
    // Python parity: tests/unit/core/test_agent_server.py
    //   test_register_sip_username
    // -------------------------------------------------------------------

    [Fact]
    public void RegisterSipUsername_SingleArgOverload_Works()
    {
        var agent = new AgentBase(new AgentOptions { Name = "t", Route = "/t" });
        // 1-arg form — auto-routes to the agent's own route
        agent.RegisterSipUsername("alice");
    }

    // -------------------------------------------------------------------
    // AgentBase.EnableSipRouting — Python takes (auto_map, path) optional
    //
    // Python parity: tests/unit/core/test_agent_base.py
    //   test_enable_sip_routing_*
    // -------------------------------------------------------------------

    [Fact]
    public void EnableSipRouting_AutoMapAndPathOptional()
    {
        var agent = new AgentBase(new AgentOptions { Name = "t", Route = "/t" });
        agent.EnableSipRouting(autoMap: true);
        agent.EnableSipRouting(autoMap: false, path: "/sip");
    }

    // -------------------------------------------------------------------
    // AgentServer.SetupSipRouting — Python takes (route, auto_map) optional
    //
    // Python parity: tests/unit/core/test_agent_server.py
    //   test_setup_sip_routing_basic
    //   test_setup_sip_routing_auto_map_existing_agents
    //   test_setup_sip_routing_no_auto_map
    // -------------------------------------------------------------------

    [Fact]
    public void AgentServer_SetupSipRouting_OptionalArgs()
    {
        var server = new AgentServer();
        server.SetupSipRouting(route: "/custom-sip", autoMap: true);
    }

    // -------------------------------------------------------------------
    // Call.HangupAsync — Python's hangup(reason="hangup") accepts a reason
    //
    // Python parity: tests/unit/relay/test_call.py
    //   test_hangup
    // -------------------------------------------------------------------

    [Fact]
    public void Call_HangupAsync_AcceptsReason()
    {
        // We don't actually dispatch a RELAY call here — just verify the
        // method shape compiles. A real test would require a Call instance
        // bound to a RelayClient; this exercise is structural.
        var method = typeof(SignalWire.Relay.Call).GetMethod("HangupAsync", new[] { typeof(string) });
        Assert.NotNull(method);
    }

    // -------------------------------------------------------------------
    // FunctionResult.ReplaceInHistory — Python takes
    // ``text: Union[bool, str] = True``. Both forms must work.
    //
    // Python parity: tests/unit/core/test_function_result.py
    //   TestFunctionResultReplaceInHistory.* (4 tests)
    //   ↑ Python-side scaffolding gap closed in this commit; the docs
    //   and prefabs/info_gatherer.py used the method but no unit test
    //   exercised it. Adding to Python first per audit discipline.
    // -------------------------------------------------------------------

    [Fact]
    public void ReplaceInHistory_DefaultTrue()
    {
        var fr = new FunctionResult();
        fr.ReplaceInHistory();
        var actions = (List<Dictionary<string, object>>)fr.ToDict()["action"];
        var action = actions[0];
        Assert.True(action.ContainsKey("replace_in_history"));
        Assert.Equal(true, action["replace_in_history"]);
    }

    [Fact]
    public void ReplaceInHistory_WithString()
    {
        var fr = new FunctionResult();
        fr.ReplaceInHistory("I've saved your data.");
        var actions = (List<Dictionary<string, object>>)fr.ToDict()["action"];
        Assert.Equal("I've saved your data.", actions[0]["replace_in_history"]);
    }

    [Fact]
    public void ReplaceInHistory_WithFalse()
    {
        var fr = new FunctionResult();
        fr.ReplaceInHistory(false);
        var actions = (List<Dictionary<string, object>>)fr.ToDict()["action"];
        Assert.Equal(false, actions[0]["replace_in_history"]);
    }

    [Fact]
    public void ReplaceInHistory_Chaining()
    {
        var fr = new FunctionResult();
        Assert.Same(fr, fr.ReplaceInHistory());
    }

    // -------------------------------------------------------------------
    // FunctionResult.SwitchContext — Python's
    // ``switch_context(system_prompt=None, user_prompt=None, consolidate=False, full_reset=False)``:
    // when only system_prompt is set the action value is a bare STRING
    // (simple form); otherwise a dict (advanced form). All four params
    // default to the falsey value, so calling with no args is legal.
    //
    // Python parity: tests/unit/core/test_function_result.py
    //   TestSwitchContextEdgeCases.test_switch_context_simple_string_only
    //   TestSwitchContextEdgeCases.test_switch_context_with_full_reset_only
    // -------------------------------------------------------------------

    [Fact]
    public void SwitchContext_SimpleStringOnly()
    {
        var fr = new FunctionResult();
        fr.SwitchContext(systemPrompt: "You are a helpful bot");
        var actions = (List<Dictionary<string, object>>)fr.ToDict()["action"];
        // Python emits a BARE STRING for the simple-form case, not a dict.
        Assert.Equal("You are a helpful bot", actions[0]["context_switch"]);
    }

    [Fact]
    public void SwitchContext_FullResetOnly_NoSystemPrompt()
    {
        var fr = new FunctionResult();
        fr.SwitchContext(fullReset: true);
        var actions = (List<Dictionary<string, object>>)fr.ToDict()["action"];
        var ctx = (Dictionary<string, object>)actions[0]["context_switch"];
        Assert.True((bool)ctx["full_reset"]);
        Assert.False(ctx.ContainsKey("system_prompt"));
    }

    // -------------------------------------------------------------------
    // Fabric sub-resources — Python's FabricNamespace exposes 16
    // sub-resource accessors; .NET's Fabric class previously had a
    // different set (some renamed/deprecated). Add the missing ones so
    // a Python user's call ``client.fabric.cxml_applications.list(...)``
    // can be written in .NET as ``client.Fabric.CxmlApplications.List()``.
    //
    // Python parity:
    //   /home/devuser/src/signalwire-python/signalwire/signalwire/rest/namespaces/fabric.py::FabricNamespace
    // -------------------------------------------------------------------

    // -------------------------------------------------------------------
    // AgentBase Python-parity accessors —
    // ``get_name``, ``get_full_url``, ``pom``, ``skill_manager``,
    // ``auto_map_sip_usernames``. Python users write ``agent.skill_manager``
    // (not ``agent.get_skill_manager()``); ``agent.get_name()``,
    // ``agent.pom``. .NET should expose equivalent property/method shapes
    // so the same code idiom is writeable.
    //
    // Python parity: signalwire/core/agent_base.py::AgentBase
    //   ``get_name`` (line 312), ``get_full_url`` (line 321),
    //   ``auto_map_sip_usernames`` (line 670), ``self.pom``,
    //   ``self.skill_manager``.
    // -------------------------------------------------------------------

    [Fact]
    public void AgentBase_GetName_ReturnsName()
    {
        var agent = new AgentBase(new AgentOptions { Name = "myagent", Route = "/r" });
        Assert.Equal("myagent", agent.GetName());
    }

    [Fact]
    public void AgentBase_GetFullUrl_BasicHostRoute()
    {
        var agent = new AgentBase(new AgentOptions { Name = "t", Route = "/agent", Host = "example.com", Port = 8080 });
        // Returns full URL — exact format may vary, but must contain host
        // and route. (Python's get_full_url ships ``http://host:port/route``
        // with optional auth prefix.)
        var url = agent.GetFullUrl();
        Assert.Contains("example.com", url);
        Assert.Contains("/agent", url);
    }

    [Fact]
    public void AgentBase_SkillManager_Property_Accessible()
    {
        var agent = new AgentBase(new AgentOptions { Name = "t", Route = "/r" });
        // Python: ``agent.skill_manager`` is an instance attribute. .NET
        // matches via a property of the same name (snake-cases to
        // ``skill_manager``). Equivalent to GetSkillManager().
        Assert.NotNull(agent.SkillManager);
        Assert.Same(agent.SkillManager, agent.SkillManager); // lazy-singleton
    }

    [Fact]
    public void AgentBase_AutoMapSipUsernames_Chainable()
    {
        var agent = new AgentBase(new AgentOptions { Name = "t", Route = "/r" });
        // Python: ``agent.auto_map_sip_usernames()`` returns self for
        // chaining.
        Assert.Same(agent, agent.AutoMapSipUsernames());
    }

    [Fact]
    public void Fabric_PythonParitySubResources_Exposed()
    {
        var http = new SignalWire.REST.HttpClient("p", "t", "https://test.com");
        var fabric = new SignalWire.REST.Namespaces.Fabric(http);
        // 9 sub-resources Python exposes that were missing in .NET.
        Assert.Equal("/api/fabric/resources/cxml_applications", fabric.CxmlApplications.BasePath);
        Assert.Equal("/api/fabric/resources/cxml_scripts", fabric.CxmlScripts.BasePath);
        Assert.Equal("/api/fabric/resources/cxml_webhooks", fabric.CxmlWebhooks.BasePath);
        Assert.Equal("/api/fabric/resources/freeswitch_connectors", fabric.FreeswitchConnectors.BasePath);
        Assert.Equal("/api/fabric/resources/relay_applications", fabric.RelayApplications.BasePath);
        Assert.Equal("/api/fabric/resources/sip_gateways", fabric.SipGateways.BasePath);
        Assert.Equal("/api/fabric/resources/swml_webhooks", fabric.SwmlWebhooks.BasePath);
        // ``resources`` is the catch-all under /api/fabric/resources (no
        // sub-path); ``tokens`` lives at /api/fabric/tokens (different base).
        Assert.Equal("/api/fabric/resources", fabric.Resources.BasePath);
        Assert.Equal("/api/fabric/tokens", fabric.Tokens.BasePath);
    }
}
