using System.Text;
using Xunit;
using SignalWire.Agent;
using SignalWire.SWML;
using SignalWire.SWAIG;
using SignalWire.Logging;

namespace SignalWire.Tests;

/// <summary>
/// Tests for <see cref="ParameterSchema"/> — the typed SWAIG tool-parameter
/// builder (Tier-2 idiom flagship).
///
/// Two contracts are exercised against real behavior (no mocks):
///   (a) byte-identical: the builder's <c>Build()</c> output equals the
///       equivalent hand-written <c>Dictionary&lt;string, object&gt;</c> params
///       — across every property kind, including an enum property — and the
///       inline required flags match. We assert deep structural equality on the
///       dictionaries AND identical JSON serialization (true byte parity).
///   (b) end-to-end: a real <c>DefineTool</c> using builder-built params is
///       rendered into SWML, and the parameters appear under the SWAIG
///       function's <c>argument.properties</c>; the function also dispatches.
/// </summary>
public class ParameterSchemaTests : IDisposable
{
    public ParameterSchemaTests()
    {
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        Environment.SetEnvironmentVariable("PORT", null);
    }

    public void Dispose()
    {
        Logger.Reset();
        Schema.Reset();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static AgentBase MakeAgent()
    {
        return new AgentBase(new AgentOptions
        {
            Name = "schema-agent",
            BasicAuthUser = "u",
            BasicAuthPassword = "p",
        });
    }

    private static Dictionary<string, object> ExtractAiVerb(Dictionary<string, object> swml)
    {
        var sections = (Dictionary<string, object>)swml["sections"];
        var main = sections["main"];
        if (main is List<Dictionary<string, object?>> typedList)
        {
            foreach (var verb in typedList)
                if (verb.ContainsKey("ai")) return (Dictionary<string, object>)verb["ai"]!;
        }
        else if (main is List<Dictionary<string, object>> untypedList)
        {
            foreach (var verb in untypedList)
                if (verb.ContainsKey("ai")) return (Dictionary<string, object>)verb["ai"];
        }
        throw new InvalidOperationException("AI verb not found in rendered SWML");
    }

    /// <summary>
    /// Order-insensitive canonical JSON for byte-parity comparison: recursively
    /// sorts object keys so that two semantically-identical maps serialize to
    /// the same string regardless of insertion order. (Insertion order already
    /// matches in these tests, but sorting makes the assertion about the data,
    /// not the iteration order.)
    /// </summary>
    private static string Canonical(object? value)
    {
        var sb = new StringBuilder();
        Write(value, sb);
        return sb.ToString();

        static void Write(object? v, StringBuilder sb)
        {
            switch (v)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case System.Collections.IDictionary dict:
                    var keys = new List<string>();
                    foreach (var k in dict.Keys) keys.Add(k!.ToString()!);
                    keys.Sort(StringComparer.Ordinal);
                    sb.Append('{');
                    for (var i = 0; i < keys.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append('"').Append(keys[i]).Append("\":");
                        Write(dict[keys[i]], sb);
                    }
                    sb.Append('}');
                    break;
                case System.Collections.IEnumerable seq:
                    sb.Append('[');
                    var first = true;
                    foreach (var item in seq)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(item, sb);
                    }
                    sb.Append(']');
                    break;
                default:
                    sb.Append(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }
        }
    }

    // ==================================================================
    //  (a) Byte-identical proof — every property kind incl. an enum
    // ==================================================================

    [Fact]
    public void Build_IsByteIdenticalToHandWrittenParams_AllKinds()
    {
        // --- Hand-written form: exactly what a developer writes today. ---
        var handWritten = new Dictionary<string, object>
        {
            ["service"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "The service",
                ["required"] = true,
            },
            ["count"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "How many",
            },
            ["ratio"] = new Dictionary<string, object>
            {
                ["type"] = "number",
                ["description"] = "A ratio",
                ["default"] = 1.5,
            },
            ["confirmed"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "Confirmed?",
            },
            ["date"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "A date",
                ["format"] = "date",
                ["required"] = true,
            },
            // Enum property — the closed set spelled out by hand as a List<string>.
            ["fmt"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Recording format",
                ["enum"] = new List<string> { "wav", "mp3", "mp4" },
            },
            ["tags"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["description"] = "Tag list",
                ["items"] = new Dictionary<string, object> { ["type"] = "string" },
            },
            ["address"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["description"] = "Mailing address",
                ["properties"] = new Dictionary<string, object>
                {
                    ["street"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Street",
                    },
                    ["zip"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "ZIP",
                    },
                },
            },
        };

        // --- Builder form: the same shape, type-safely. The enum property
        //     sources its closed set from the Tier-1 RecordFormat enum via
        //     ToWireName (→ ["wav","mp3","mp4"]). ---
        var built = ParameterSchema.Create()
            .String("service", "The service")
            .Integer("count", "How many")
            .Number("ratio", "A ratio", defaultValue: 1.5)
            .Boolean("confirmed", "Confirmed?")
            .String("date", "A date", format: "date")
            .Enum("fmt", typeof(RecordFormat), "Recording format")
            .Array("tags", "string", "Tag list")
            .Object("address", ParameterSchema.Create()
                .String("street", "Street")
                .String("zip", "ZIP"), "Mailing address")
            .Required("service", "date")
            .Build();

        // Deep structural equality of the dictionaries.
        Assert.Equal(handWritten, built);

        // True byte parity: identical canonical JSON.
        Assert.Equal(Canonical(handWritten), Canonical(built));

        // Spot-check the enum closed set came from the typed enum, not a literal.
        var fmt = (Dictionary<string, object>)built["fmt"];
        Assert.Equal(new List<string> { "wav", "mp3", "mp4" }, (List<string>)fmt["enum"]);

        // Inline required flags match the hand-written convention.
        Assert.True((bool)((Dictionary<string, object>)built["service"])["required"]);
        Assert.True((bool)((Dictionary<string, object>)built["date"])["required"]);
        Assert.False(((Dictionary<string, object>)built["count"]).ContainsKey("required"));

        // And the top-level required-array form is available for DataMap-style callers.
        Assert.Equal(new[] { "service", "date" }, ParameterSchema.Create()
            .String("service", "The service")
            .Integer("count", "How many")
            .Number("ratio", "A ratio", defaultValue: 1.5)
            .Boolean("confirmed", "Confirmed?")
            .String("date", "A date", format: "date")
            .Enum("fmt", typeof(RecordFormat), "Recording format")
            .Array("tags", "string", "Tag list")
            .Object("address", ParameterSchema.Create()
                .String("street", "Street")
                .String("zip", "ZIP"), "Mailing address")
            .Required("service", "date")
            .RequiredNames.ToArray());
    }

    [Fact]
    public void Enum_EachTier1EnumProducesItsWireNames()
    {
        // RecordFormat / RecordDirection / TapDirection / Codec — each closed
        // set is sourced from the enum's ToWireName, not a re-typed string list.
        var p = ParameterSchema.Create()
            .Enum("format", typeof(RecordFormat), "fmt")
            .Enum("rec_dir", typeof(RecordDirection), "record direction")
            .Enum("tap_dir", typeof(TapDirection), "tap direction")
            .Enum("codec", typeof(Codec), "codec")
            .Build();

        Assert.Equal(new List<string> { "wav", "mp3", "mp4" },
            (List<string>)((Dictionary<string, object>)p["format"])["enum"]);
        Assert.Equal(new List<string> { "speak", "listen", "both" },
            (List<string>)((Dictionary<string, object>)p["rec_dir"])["enum"]);
        // tap uses "hear" where record_call uses "listen" — distinct vocab.
        Assert.Equal(new List<string> { "speak", "hear", "both" },
            (List<string>)((Dictionary<string, object>)p["tap_dir"])["enum"]);
        Assert.Equal(new List<string> { "PCMU", "PCMA" },
            (List<string>)((Dictionary<string, object>)p["codec"])["enum"]);
    }

    [Fact]
    public void Build_ReturnsFreshCopy_NotBuilderState()
    {
        var builder = ParameterSchema.Create().String("a", "first");
        var first = builder.Build();
        ((Dictionary<string, object>)first["a"])["mutated"] = true;
        first["injected"] = 1;

        var second = builder.Build();
        Assert.False(second.ContainsKey("injected"));
        Assert.False(((Dictionary<string, object>)second["a"]).ContainsKey("mutated"));
    }

    [Fact]
    public void Required_OnUnknownProperty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ParameterSchema.Create().String("a", "x").Required("missing"));
    }

    [Fact]
    public void Enum_OnNonEnumType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ParameterSchema.Create().Enum("x", typeof(string), "bad"));
    }

    // ==================================================================
    //  (b) End-to-end: real DefineTool with builder params, render+invoke
    // ==================================================================

    [Fact]
    public void DefineTool_WithBuilderParams_AppearsInRenderedSwaigJson()
    {
        var agent = MakeAgent();

        var parameters = ParameterSchema.Create()
            .String("service", "The service to book")
            .String("date", "YYYY-MM-DD")
            .Enum("format", typeof(RecordFormat), "Recording format")
            .Required("service", "date")
            .Build();

        var invokedWith = new Dictionary<string, object>();
        agent.DefineTool(
            "book_appointment",
            "Book an appointment for a service on a date",
            parameters,
            (args, raw) =>
            {
                foreach (var kv in args) invokedWith[kv.Key] = kv.Value;
                return new FunctionResult($"Booked {args["service"]} on {args["date"]}");
            });

        // --- Render and locate this tool in the SWAIG functions array. ---
        var ai = ExtractAiVerb(agent.RenderSwml());
        var swaig = (Dictionary<string, object>)ai["SWAIG"];
        var functions = (List<Dictionary<string, object>>)swaig["functions"];
        var fn = functions.Single(f => (string)f["function"] == "book_appointment");

        // The argument wrapper carries our properties under .properties.
        var argument = (Dictionary<string, object>)fn["argument"];
        Assert.Equal("object", argument["type"]);
        var properties = (Dictionary<string, object>)argument["properties"];

        // DefineTool lifts per-property `required: true` into the top-level
        // JSON-Schema `required` array (standard form), so the rendered
        // properties no longer carry the per-property flag.
        var serviceProp = (Dictionary<string, object>)properties["service"];
        Assert.Equal("string", serviceProp["type"]);
        Assert.Equal("The service to book", serviceProp["description"]);
        Assert.False(serviceProp.ContainsKey("required"));

        // The required names are lifted to argument.required (in declared order).
        Assert.Equal(new List<string> { "service", "date" }, (List<string>)argument["required"]);

        var formatProp = (Dictionary<string, object>)properties["format"];
        Assert.Equal(new List<string> { "wav", "mp3", "mp4" }, (List<string>)formatProp["enum"]);

        // --- And the function actually dispatches with those args. ---
        var result = agent.OnFunctionCall("book_appointment",
            new Dictionary<string, object> { ["service"] = "haircut", ["date"] = "2026-07-01" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Equal("Booked haircut on 2026-07-01", result!.ToDict()["response"]);
        Assert.Equal("haircut", invokedWith["service"]);
        Assert.Equal("2026-07-01", invokedWith["date"]);
    }
}
