// EmitSkills — the .NET port's SKILL-DUMP program for the cross-port
// SKILL-CONTRACT differ (porting-sdk/scripts/diff_skill_contracts.py).
//
// The sibling of tools/EmitCorpus, for built-in SKILLS rather than
// FunctionResult. For each covered skill it looks up the factory in the
// SkillRegistry, instantiates it with the canonical config from the shared
// corpus (porting-sdk/scripts/skill_contract_corpus.py — the single source of
// truth), runs Wire() + Setup() + RegisterTools() onto a throwaway AgentBase,
// reads the registered tools back (each tool's "argument" is the wrapped
// {type:object, properties[, required]}), and prints ONE JSON object mapping
//
//     skill-id -> [ { "name": ..., "parameters": {...} }, ... ]
//
// to stdout. The differ parses it and structurally compares each skill's tool
// contract against the Python reference. The differ normalises both sides
// (flat vs wrapped params, per-param vs tool-level required, enum order); this
// program emits each tool's "function" name and its "argument" verbatim.
// DESCRIPTIONS are not part of the compared contract.
//
// CONTRACT (mirrors the per-port dump contract in the differ's --help):
//   - The id set MUST equal corpus_ids() (the differ rejects a mismatch).
//   - Only stdout carries the JSON object; logs/build chatter go to stderr
//     (scripts/emit-skills.sh routes MSBuild to stderr; this program writes
//     only the JSON to stdout).

using System.Diagnostics;
using System.Text.Json;
using SignalWire.Agent;
using SignalWire.Skills;

static void Die(string msg)
{
    Console.Error.WriteLine($"emit-skills: {msg}");
    Environment.Exit(1);
}

// ── Load the shared corpus via the porting-sdk python script ───────────────
// porting-sdk resolved via $PORTING_SDK / $PORTING_SDK_PATH or sibling ../porting-sdk.
static string ResolveCorpusScript()
{
    var bases = new List<string>();
    foreach (var var in new[] { "PORTING_SDK", "PORTING_SDK_PATH" })
    {
        var v = Environment.GetEnvironmentVariable(var);
        if (!string.IsNullOrEmpty(v))
        {
            bases.Add(v);
        }
    }
    bases.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", "porting-sdk"));
    foreach (var b in bases)
    {
        var script = Path.Combine(b, "scripts", "skill_contract_corpus.py");
        if (File.Exists(script))
        {
            return script;
        }
    }
    Die("cannot locate porting-sdk/scripts/skill_contract_corpus.py "
        + "(set PORTING_SDK / PORTING_SDK_PATH or clone porting-sdk adjacent)");
    return ""; // unreachable
}

var corpusScript = ResolveCorpusScript();
var psi = new ProcessStartInfo("python3", $"\"{corpusScript}\"")
{
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
};
var proc = Process.Start(psi) ?? throw new InvalidOperationException("failed to launch python3");
var corpusJson = proc.StandardOutput.ReadToEnd();
proc.WaitForExit();
if (proc.ExitCode != 0)
{
    Die($"corpus script failed: {proc.StandardError.ReadToEnd()}");
}

using var corpusDoc = JsonDocument.Parse(corpusJson);
var corpus = corpusDoc.RootElement.GetProperty("corpus");

// ── For each covered skill: instantiate, register, read tools back ─────────
var registry = SkillRegistry.Instance;
var result = new Dictionary<string, List<object>>();

foreach (var entry in corpus.EnumerateArray())
{
    var id = entry.GetProperty("id").GetString()!;
    var skillName = entry.GetProperty("skill").GetString()!;
    var config = JsonConfigToDict(entry.GetProperty("config"));

    var factory = registry.GetFactory(skillName);
    if (factory is null)
    {
        Die($"no registered factory for covered skill '{skillName}'");
    }
    var skill = factory!();

    var agent = new AgentBase(new AgentOptions { Name = "emit-skills", Route = "/emit" });
    skill.Wire(agent, config);
    if (!skill.Setup(agent, config))
    {
        Die($"skill '{skillName}' Setup() returned false with the corpus config "
            + "— config drift between the corpus and the port.");
    }
    skill.RegisterTools(agent);

    var contracts = new List<object>();
    foreach (var def in agent.Tools)
    {
        var name = def.TryGetValue("function", out var n) ? n as string : null;
        if (name is null)
        {
            continue;
        }
        object parameters = def.TryGetValue("argument", out var arg)
            ? arg
            : new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() };
        contracts.Add(new Dictionary<string, object> { ["name"] = name, ["parameters"] = parameters });
    }
    result[id] = contracts;
}

Console.WriteLine(JsonSerializer.Serialize(result, EmitSkillsJson.Options));

// Convert the corpus JSON config object into the Dictionary<string,object>
// shape skills expect (recursively, matching how the platform hands config in).
static Dictionary<string, object> JsonConfigToDict(JsonElement obj)
{
    var dict = new Dictionary<string, object>();
    foreach (var prop in obj.EnumerateObject())
    {
        dict[prop.Name] = JsonToObject(prop.Value);
    }
    return dict;
}

static object JsonToObject(JsonElement el) => el.ValueKind switch
{
    JsonValueKind.Object => JsonConfigToDict(el),
    JsonValueKind.Array => JsonArrayToList(el),
    JsonValueKind.String => el.GetString()!,
    JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
    JsonValueKind.True => true,
    JsonValueKind.False => false,
    _ => null!,
};

// Match how the platform hands config to skills: an array of objects arrives as
// List<Dictionary<string,object>> (skills pattern-match on that exact type, e.g.
// info_gatherer's `questions`, play_background's `files`). A non-object array
// stays List<object>.
static object JsonArrayToList(JsonElement el)
{
    var items = el.EnumerateArray().Select(JsonToObject).ToList();
    if (items.Count > 0 && items.All(x => x is Dictionary<string, object>))
    {
        return items.Cast<Dictionary<string, object>>().ToList();
    }
    return items;
}


/// <summary>Serializer options, cached in a static so they are allocated once (CA1869).</summary>
internal static class EmitSkillsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
