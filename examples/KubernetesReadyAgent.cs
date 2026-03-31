// Kubernetes-Ready Agent Example
//
// Demonstrates an agent configured for Kubernetes deployment with
// health endpoints, graceful shutdown, and environment-based config.

using SignalWire.Agent;
using SignalWire.SWAIG;

var host = Environment.GetEnvironmentVariable("AGENT_HOST") ?? "0.0.0.0";
var port = int.Parse(Environment.GetEnvironmentVariable("AGENT_PORT") ?? "3000");
var name = Environment.GetEnvironmentVariable("AGENT_NAME") ?? "k8s-agent";

var agent = new AgentBase(new AgentOptions
{
    Name  = name,
    Route = "/",
    Host  = host,
    Port  = port,
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object>
{
    ["ai_model"]              = Environment.GetEnvironmentVariable("AI_MODEL") ?? "gpt-4.1-nano",
    ["end_of_speech_timeout"] = 500,
});

agent.PromptAddSection("Role",
    "You are a helpful AI assistant running in a Kubernetes cluster.");

agent.PromptAddSection("Instructions", "", new List<string>
{
    "Greet users warmly and offer help",
    "Use the get_time function when asked about the time",
    "Be concise and efficient",
});

agent.DefineTool(
    name:        "get_time",
    description: "Get the current time",
    parameters:  new Dictionary<string, object>(),
    handler: (args, raw) =>
    {
        var time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        return new FunctionResult($"The current time is {time}");
    }
);

agent.DefineTool(
    name:        "get_pod_info",
    description: "Get information about the running pod",
    parameters:  new Dictionary<string, object>(),
    handler: (args, raw) =>
    {
        var podName  = Environment.GetEnvironmentVariable("POD_NAME")      ?? "unknown";
        var nodeName = Environment.GetEnvironmentVariable("NODE_NAME")     ?? "unknown";
        var ns       = Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? "default";
        return new FunctionResult(
            $"Running on pod {podName} in namespace {ns} on node {nodeName}");
    }
);

// Graceful shutdown handling
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("Received shutdown signal, stopping gracefully...");
    cts.Cancel();
};

Console.WriteLine($"Starting Kubernetes-Ready Agent: {name}");
Console.WriteLine($"Listening on {host}:{port}");
Console.WriteLine("Health: /health  Ready: /ready");

agent.Run();
