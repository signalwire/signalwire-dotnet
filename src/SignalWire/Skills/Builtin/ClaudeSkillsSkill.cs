using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Load Claude SKILL.md files as agent tools.</summary>
public sealed class ClaudeSkillsSkill : SkillBase
{
    public override string Name => "claude_skills";
    public override string Description => "Load Claude SKILL.md files as agent tools";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("skills_path", out var sp) && sp is string s && s.Length > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var skillsPath = Params.TryGetValue("skills_path", out var sp) ? sp as string ?? "" : "";
        var toolPrefix = Params.TryGetValue("tool_prefix", out var tp) ? tp as string ?? "claude_" : "claude_";
        var responsePrefix = Params.TryGetValue("response_prefix", out var rp) ? rp as string ?? "" : "";
        var responsePostfix = Params.TryGetValue("response_postfix", out var rpf) ? rpf as string ?? "" : "";

        var toolName = toolPrefix + "skill";

        DefineTool(
            toolName,
            "Execute a Claude skill from " + skillsPath,
            new Dictionary<string, object>
            {
                ["arguments"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Arguments to pass to the skill",
                    ["required"] = true,
                },
                ["section"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Optional section of the skill to invoke",
                },
            },
            (args, rawData) =>
            {
                var result = new FunctionResult();
                var arguments = args.TryGetValue("arguments", out var a) ? a as string ?? "" : "";
                var section = args.TryGetValue("section", out var s) ? s as string ?? "" : "";

                var response = "";
                if (responsePrefix.Length > 0) response += responsePrefix + " ";
                response += $"Claude skill execution from \"{skillsPath}\"";
                if (section.Length > 0) response += $" (section: {section})";
                response += $" with arguments: {arguments}. ";
                response += "In production, this would parse SKILL.md files with YAML frontmatter and execute them.";
                if (responsePostfix.Length > 0) response += " " + responsePostfix;

                result.SetResponse(response);
                return result;
            });
    }

    public override List<string> GetHints() => ["claude", "skill"];

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        var skillsPath = Params.TryGetValue("skills_path", out var sp) ? sp as string ?? "" : "";

        return [new Dictionary<string, object>
        {
            ["title"] = "Claude Skills",
            ["body"] = "You have access to Claude skills loaded from " + skillsPath + ".",
            ["bullets"] = new List<string>
            {
                "Use claude skill tools to execute specialized tasks.",
                "Pass arguments as a string describing what you need.",
                "Optionally specify a section to target a specific part of the skill.",
            },
        }];
    }
}
