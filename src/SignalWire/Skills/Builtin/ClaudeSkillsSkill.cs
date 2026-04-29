using System.Text;
using System.Text.RegularExpressions;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>
/// Load Claude SKILL.md files as agent tools.
///
/// Mirrors signalwire-python's <c>signalwire.skills.claude_skills.skill</c>.
/// At setup time, the skill walks <c>skills_path</c> for child directories
/// containing <c>SKILL.md</c>; each such file is parsed for YAML
/// frontmatter (the bit between two <c>---</c> lines) plus a markdown
/// body. Each loaded skill becomes one SWAIG tool whose handler returns
/// the body with three substitutions:
///
/// <list type="bullet">
///   <item><c>$ARGUMENTS</c> / <c>$ARGUMENTS[N]</c> / <c>$N</c> — the
///     <c>arguments</c> string passed to the tool, optionally split into
///     positional pieces by whitespace.</item>
///   <item><c>${CLAUDE_SKILL_DIR}</c> — absolute path to the skill's
///     directory.</item>
///   <item><c>${CLAUDE_SESSION_ID}</c> — call id from raw_data.</item>
/// </list>
///
/// The full Python implementation also runs a frontmatter-driven invocation
/// gate (skip-tool / skip-prompt), tolerates supporting reference sections,
/// and offers an opt-in shell-injection preprocessor (<c>!`cmd`</c>). The
/// .NET port ships the discovery/parse/handler core; the optional shell
/// preprocessor is left out by design (set <c>allow_shell_injection</c>
/// to a no-op — the surface is preserved for future expansion).
/// </summary>
public sealed class ClaudeSkillsSkill : SkillBase
{
    private static readonly Regex IndexedArgRegex = new(@"\$ARGUMENTS\[(\d+)\]", RegexOptions.Compiled);
    private static readonly Regex ShorthandArgRegex = new(@"\$(\d+)(?!\d)", RegexOptions.Compiled);
    private static readonly Regex BareArgsRegex = new(@"\$ARGUMENTS(?!\[)", RegexOptions.Compiled);

    private record ParsedSkill(string Name, string? Description, string Body, string SkillDir,
        Dictionary<string, string> Sections, bool SkipTool, bool SkipPrompt, string? ArgumentHint);

    private List<ParsedSkill> _skills = new();
    private string _toolPrefix = "claude_";
    private string _responsePrefix = "";
    private string _responsePostfix = "";

    public override string Name => "claude_skills";
    public override string Description => "Load Claude SKILL.md files as agent tools";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        var skillsPath = parameters.TryGetValue("skills_path", out var sp) ? sp as string ?? "" : "";
        if (skillsPath.Length == 0) return false;
        skillsPath = Path.GetFullPath(skillsPath);
        if (!Directory.Exists(skillsPath)) return false;

        _toolPrefix = parameters.TryGetValue("tool_prefix", out var tp) ? tp as string ?? "claude_" : "claude_";
        _responsePrefix = parameters.TryGetValue("response_prefix", out var rp) ? rp as string ?? "" : "";
        _responsePostfix = parameters.TryGetValue("response_postfix", out var rpf) ? rpf as string ?? "" : "";

        var includes = parameters.TryGetValue("include", out var inc) && inc is List<string> il && il.Count > 0
            ? il : ["*"];
        var excludes = parameters.TryGetValue("exclude", out var exc) && exc is List<string> el ? el : [];
        var ignoreInvocationControl = parameters.TryGetValue("ignore_invocation_control", out var iic) && iic is true;

        _skills.Clear();
        foreach (var dir in Directory.EnumerateDirectories(skillsPath))
        {
            var skillFile = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillFile)) continue;

            var name = new DirectoryInfo(dir).Name;
            if (!Matches(name, includes, excludes)) continue;

            var parsed = Parse(skillFile, name, ignoreInvocationControl);
            if (parsed is not null) _skills.Add(parsed);
        }
        return true;
    }

    public override void RegisterTools(AgentBase agent)
    {
        foreach (var skill in _skills)
        {
            if (skill.SkipTool) continue;

            var toolName = _toolPrefix + Sanitize(skill.Name);
            var description = skill.Description ?? $"Use the {skill.Name} skill";
            var parameters = new Dictionary<string, object>
            {
                ["arguments"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = skill.ArgumentHint ?? "Arguments or context to pass to the skill",
                    ["required"] = true,
                },
            };
            if (skill.Sections.Count > 0)
            {
                parameters["section"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Which reference section to load",
                    ["enum"] = skill.Sections.Keys.OrderBy(k => k).ToList(),
                };
            }

            DefineTool(toolName, description, parameters, MakeHandler(skill));
        }
    }

    private Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> MakeHandler(ParsedSkill skill)
    {
        return (args, rawData) =>
        {
            var section = args.TryGetValue("section", out var s) ? s as string ?? "" : "";
            var arguments = args.TryGetValue("arguments", out var a) ? a as string ?? "" : "";

            string content;
            if (section.Length > 0 && skill.Sections.TryGetValue(section, out var sectionPath))
            {
                try { content = File.ReadAllText(sectionPath, Encoding.UTF8); }
                catch (Exception) { content = $"Error loading section '{section}'"; }
            }
            else
            {
                content = skill.Body;
            }

            content = SubstituteVariables(content, skill.SkillDir, rawData);
            content = SubstituteArguments(content, arguments);

            if (_responsePrefix.Length > 0 || _responsePostfix.Length > 0)
            {
                var parts = new List<string>();
                if (_responsePrefix.Length > 0) parts.Add(_responsePrefix);
                parts.Add(content);
                if (_responsePostfix.Length > 0) parts.Add(_responsePostfix);
                content = string.Join("\n\n", parts);
            }
            return new FunctionResult(content);
        };
    }

    private static ParsedSkill? Parse(string skillFile, string fallbackName, bool ignoreInvocationControl)
    {
        string raw;
        try { raw = File.ReadAllText(skillFile, Encoding.UTF8); }
        catch { return null; }

        string? name = null, description = null, argumentHint = null;
        var disableModel = false;
        var userInvocable = true;
        string body;

        if (raw.StartsWith("---"))
        {
            var rest = raw[3..];
            var endIdx = rest.IndexOf("\n---", StringComparison.Ordinal);
            if (endIdx >= 0)
            {
                var fm = rest[..endIdx];
                var afterIdx = endIdx + 4;
                while (afterIdx < rest.Length && (rest[afterIdx] == '\r' || rest[afterIdx] == '\n')) afterIdx++;
                body = rest[afterIdx..].TrimStart();

                foreach (var line in fm.Split('\n'))
                {
                    var trimmed = line.TrimEnd('\r');
                    var colonIdx = trimmed.IndexOf(':');
                    if (colonIdx <= 0) continue;
                    var key = trimmed[..colonIdx].Trim();
                    var value = trimmed[(colonIdx + 1)..].Trim();
                    if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
                    else if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'') value = value[1..^1];
                    switch (key.ToLowerInvariant())
                    {
                        case "name": name = value; break;
                        case "description": description = value; break;
                        case "argument-hint": argumentHint = value; break;
                        case "disable-model-invocation":
                            disableModel = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase); break;
                        case "user-invocable":
                            userInvocable = !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase); break;
                    }
                }
            }
            else
            {
                body = raw.Trim();
            }
        }
        else
        {
            body = raw.Trim();
        }

        if (string.IsNullOrEmpty(name)) name = fallbackName;

        var skillDir = Path.GetDirectoryName(skillFile) ?? "";
        var sections = DiscoverSections(skillDir);

        bool skipTool, skipPrompt;
        if (ignoreInvocationControl)
        {
            skipTool = false;
            skipPrompt = false;
        }
        else if (disableModel)
        {
            skipTool = true;
            skipPrompt = true;
        }
        else if (!userInvocable)
        {
            skipTool = true;
            skipPrompt = false;
        }
        else
        {
            skipTool = false;
            skipPrompt = false;
        }

        return new ParsedSkill(name!, description, body, skillDir, sections, skipTool, skipPrompt, argumentHint);
    }

    private static Dictionary<string, string> DiscoverSections(string skillDir)
    {
        var sections = new Dictionary<string, string>();
        if (!Directory.Exists(skillDir)) return sections;
        foreach (var path in Directory.EnumerateFiles(skillDir, "*.md", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(path);
            if (string.Equals(fileName, "SKILL.md", StringComparison.OrdinalIgnoreCase)) continue;
            var relative = Path.GetRelativePath(skillDir, path).Replace(Path.DirectorySeparatorChar, '/');
            var stem = Path.ChangeExtension(relative, null);
            sections[stem] = path;
        }
        return sections;
    }

    private static bool Matches(string name, List<string> includes, List<string> excludes)
    {
        foreach (var pattern in excludes)
        {
            if (GlobMatch(pattern, name)) return false;
        }
        foreach (var pattern in includes)
        {
            if (GlobMatch(pattern, name)) return true;
        }
        return false;
    }

    private static bool GlobMatch(string pattern, string name)
    {
        // Translate fnmatch-style glob to regex (only supports * and ? — the
        // common cases used in claude_skills include/exclude lists).
        var sb = new StringBuilder("^");
        foreach (var c in pattern)
        {
            sb.Append(c switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(c.ToString()),
            });
        }
        sb.Append('$');
        return Regex.IsMatch(name, sb.ToString(), RegexOptions.IgnoreCase);
    }

    private static string Sanitize(string raw)
    {
        var lower = raw.ToLowerInvariant();
        var withUnderscores = Regex.Replace(lower, @"[-\s]+", "_");
        var stripped = Regex.Replace(withUnderscores, @"[^a-z0-9_]", "");
        if (stripped.Length > 0 && char.IsDigit(stripped[0])) stripped = "_" + stripped;
        return stripped.Length > 0 ? stripped : "unnamed";
    }

    private static string SubstituteVariables(string content, string skillDir, Dictionary<string, object?> rawData)
    {
        content = content.Replace("${CLAUDE_SKILL_DIR}", skillDir);
        var sessionId = rawData.TryGetValue("call_id", out var cid) && cid is string cidStr ? cidStr : "";
        content = content.Replace("${CLAUDE_SESSION_ID}", sessionId);
        return content;
    }

    private static string SubstituteArguments(string body, string arguments)
    {
        arguments ??= "";
        var hasBareArguments = BareArgsRegex.IsMatch(body);
        var positional = string.IsNullOrWhiteSpace(arguments)
            ? Array.Empty<string>()
            : Regex.Split(arguments, @"\s+");

        var result = IndexedArgRegex.Replace(body, m =>
        {
            var idx = int.Parse(m.Groups[1].Value);
            return idx < positional.Length ? positional[idx] : "";
        });
        result = ShorthandArgRegex.Replace(result, m =>
        {
            var idx = int.Parse(m.Groups[1].Value);
            return idx < positional.Length ? positional[idx] : "";
        });
        result = result.Replace("$ARGUMENTS", arguments);
        if (!hasBareArguments && arguments.Length > 0)
        {
            result += $"\n\nARGUMENTS: {arguments}";
        }
        return result;
    }

    public override List<string> GetHints()
    {
        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in _skills)
        {
            foreach (var word in skill.Name.Replace('-', ' ').Replace('_', ' ').Split(' ',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                hints.Add(word);
            }
        }
        return hints.ToList();
    }

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];
        var sections = new List<Dictionary<string, object>>();
        foreach (var skill in _skills)
        {
            if (skill.SkipPrompt) continue;
            var body = skill.Body;
            if (skill.Sections.Count > 0 && !skill.SkipTool)
            {
                var sectionList = string.Join(", ", skill.Sections.Keys.OrderBy(k => k));
                body += $"\n\nAvailable reference sections: {sectionList}";
                body += $"\nCall {_toolPrefix}{Sanitize(skill.Name)}(section=\"<name>\") to load a section.";
            }
            sections.Add(new Dictionary<string, object>
            {
                ["title"] = skill.Name,
                ["body"] = body,
            });
        }
        return sections;
    }

    public override string GetInstanceKey()
    {
        var skillsPath = Params.TryGetValue("skills_path", out var sp) ? sp as string ?? "default" : "default";
        return $"claude_skills_{skillsPath.GetHashCode():x}";
    }
}
