using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Control background file playback (DataMap).</summary>
public sealed class PlayBackgroundFileSkill : SkillBase
{
    public override string Name => "play_background_file";
    public override string Description => "Control background file playback";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("files", out var f) && f is List<Dictionary<string, object>> fl && fl.Count > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("play_background_file");
        var files = Params.TryGetValue("files", out var f) && f is List<Dictionary<string, object>> fl ? fl : [];

        var actionEnum = new List<string>();
        var expressions = new List<Dictionary<string, object>>();

        foreach (var file in files)
        {
            var key = file.TryGetValue("key", out var k) ? k as string ?? "" : "";
            var url = file.TryGetValue("url", out var u) ? u as string ?? "" : "";
            var desc = file.TryGetValue("description", out var d) ? d as string ?? key : key;
            var wait = file.TryGetValue("wait", out var w) && w is true;

            if (key.Length == 0 || url.Length == 0) continue;

            actionEnum.Add("start_" + key);
            var actionKey = wait ? "play_background_file_wait" : "play_background_file";

            expressions.Add(new Dictionary<string, object>
            {
                ["string"] = "${args.action}",
                ["pattern"] = "start_" + key,
                ["output"] = new Dictionary<string, object>
                {
                    ["response"] = "Now playing: " + desc,
                    ["action"] = new List<Dictionary<string, object>> { new() { [actionKey] = url } },
                },
            });
        }

        actionEnum.Add("stop");
        expressions.Add(new Dictionary<string, object>
        {
            ["string"] = "${args.action}",
            ["pattern"] = "stop",
            ["output"] = new Dictionary<string, object>
            {
                ["response"] = "Stopping background playback.",
                ["action"] = new List<Dictionary<string, object>> { new() { ["stop_background_file"] = true } },
            },
        });

        var funcDef = new Dictionary<string, object>
        {
            ["function"] = toolName,
            ["purpose"] = "Control background file playback for " + toolName,
            ["argument"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["action"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The playback action to perform",
                        ["enum"] = actionEnum,
                    },
                },
                ["required"] = new List<string> { "action" },
            },
            ["data_map"] = new Dictionary<string, object>
            {
                ["expressions"] = expressions,
            },
        };

        Agent.RegisterSwaigFunction(funcDef);
    }
}
