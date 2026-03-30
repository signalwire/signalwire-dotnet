using System.Text.RegularExpressions;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Transfer calls between agents based on pattern matching (DataMap).</summary>
public sealed class SwmlTransferSkill : SkillBase
{
    public override string Name => "swml_transfer";
    public override string Description => "Transfer calls between agents based on pattern matching";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("transfers", out var t) && t is Dictionary<string, object> d && d.Count > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("transfer_call");
        var transfers = Params.TryGetValue("transfers", out var t) && t is Dictionary<string, object> d ? d : [];
        var description = Params.TryGetValue("description", out var desc) ? desc as string ?? "Transfer call based on pattern matching" : "Transfer call based on pattern matching";
        var paramName = Params.TryGetValue("parameter_name", out var pn) ? pn as string ?? "transfer_type" : "transfer_type";
        var paramDescription = Params.TryGetValue("parameter_description", out var pd) ? pd as string ?? "The type of transfer to perform" : "The type of transfer to perform";
        var defaultMessage = Params.TryGetValue("default_message", out var dm) ? dm as string ?? "Transferring your call, please hold." : "Transferring your call, please hold.";

        var transferKeys = transfers.Keys.ToList();

        var properties = new Dictionary<string, object>
        {
            [paramName] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = paramDescription,
                ["enum"] = transferKeys,
            },
        };
        var required = new List<string> { paramName };

        // Build DataMap expressions
        var expressions = new List<Dictionary<string, object>>();

        foreach (var (pattern, config) in transfers)
        {
            var cfg = config as Dictionary<string, object> ?? [];
            var url = cfg.TryGetValue("url", out var u) ? u as string ?? "" :
                      cfg.TryGetValue("address", out var a) ? a as string ?? "" : "";
            var message = cfg.TryGetValue("message", out var m) ? m as string ?? defaultMessage : defaultMessage;

            var expression = new Dictionary<string, object>
            {
                ["string"] = "${args." + paramName + "}",
                ["pattern"] = pattern,
                ["output"] = new Dictionary<string, object>
                {
                    ["response"] = message,
                    ["action"] = new List<Dictionary<string, object>>(),
                },
            };

            if (url.Length > 0)
            {
                var actionList = (List<Dictionary<string, object>>)((Dictionary<string, object>)expression["output"])["action"];
                if (url.StartsWith("http://") || url.StartsWith("https://"))
                {
                    actionList.Add(new Dictionary<string, object> { ["transfer_uri"] = url });
                }
                else
                {
                    actionList.Add(new Dictionary<string, object>
                    {
                        ["SWML"] = new Dictionary<string, object>
                        {
                            ["sections"] = new Dictionary<string, object>
                            {
                                ["main"] = new List<Dictionary<string, object>>
                                {
                                    new() { ["connect"] = new Dictionary<string, object> { ["to"] = url } },
                                },
                            },
                        },
                    });
                }
            }

            expressions.Add(expression);
        }

        var funcDef = new Dictionary<string, object>
        {
            ["function"] = toolName,
            ["purpose"] = description,
            ["argument"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
            },
            ["data_map"] = new Dictionary<string, object>
            {
                ["expressions"] = expressions,
            },
        };

        Agent.RegisterSwaigFunction(funcDef);
    }

    public override List<string> GetHints()
    {
        var hints = new List<string> { "transfer", "connect", "speak to", "talk to" };
        if (Params.TryGetValue("transfers", out var t) && t is Dictionary<string, object> transfers)
        {
            foreach (var key in transfers.Keys)
            {
                foreach (var word in Regex.Split(key, @"[\s_\-]+"))
                {
                    var trimmed = word.Trim();
                    if (trimmed.Length > 0 && !hints.Contains(trimmed))
                        hints.Add(trimmed);
                }
            }
        }
        return hints;
    }

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        var transfers = Params.TryGetValue("transfers", out var t) && t is Dictionary<string, object> d ? d : [];
        var destinations = new List<string>();
        foreach (var (pattern, config) in transfers)
        {
            var cfg = config as Dictionary<string, object> ?? [];
            var message = cfg.TryGetValue("message", out var m) ? m as string ?? "" : "";
            destinations.Add(message.Length > 0 ? $"{pattern} - {message}" : pattern);
        }

        var sections = new List<Dictionary<string, object>>
        {
            new()
            {
                ["title"] = "Transferring",
                ["body"] = "Available transfer destinations:",
                ["bullets"] = destinations,
            },
        };

        if (destinations.Count > 0)
        {
            sections.Add(new Dictionary<string, object>
            {
                ["title"] = "Transfer Instructions",
                ["body"] = "When the user wants to be transferred:",
                ["bullets"] = new List<string>
                {
                    "Confirm the transfer destination with the user before transferring.",
                    "Use the transfer tool with the appropriate transfer type.",
                },
            });
        }

        return sections;
    }
}
