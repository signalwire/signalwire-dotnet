// Quickstart: the minimal AI agent from the top-level README.
//
// Each agent is a self-contained microservice that generates SWML and handles
// SWAIG tool calls. The SignalWire platform runs the entire AI pipeline (STT,
// LLM, TTS) -- your agent just defines the behavior.

// region: agent
using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions { Name = "my-agent", Route = "/agent" });

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.PromptAddSection("Role", "You are a helpful assistant.");

agent.DefineTool(
    name:        "get_time",
    description: "Get the current time",
    parameters:  new Dictionary<string, object>(),
    handler:     (args, rawData) => new FunctionResult($"The time is {DateTime.Now:HH:mm:ss}"));

agent.Run();
// endregion: agent
