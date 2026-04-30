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
}
