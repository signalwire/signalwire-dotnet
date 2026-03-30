using System.Text.Json;
using Xunit;
using SignalWire.SWML;

namespace SignalWire.Tests;

public class SWMLDocumentTests : IDisposable
{
    public void Dispose()
    {
        // Document has no singletons or env vars, but IDisposable
        // is kept for consistency across all SWML test classes.
    }

    [Fact]
    public void DefaultVersion()
    {
        var doc = new Document();
        Assert.Equal("1.0.0", doc.Version);
    }

    [Fact]
    public void DefaultMainSection()
    {
        var doc = new Document();
        Assert.True(doc.HasSection("main"));
        Assert.Empty(doc.GetVerbs("main"));
    }

    [Fact]
    public void AddSection_NewSection_ReturnsTrue()
    {
        var doc = new Document();
        Assert.True(doc.AddSection("custom"));
        Assert.True(doc.HasSection("custom"));
        Assert.Empty(doc.GetVerbs("custom"));
    }

    [Fact]
    public void AddSection_Duplicate_ReturnsFalse()
    {
        var doc = new Document();
        Assert.True(doc.AddSection("custom"));
        Assert.False(doc.AddSection("custom"));
    }

    [Fact]
    public void HasSection_False()
    {
        var doc = new Document();
        Assert.False(doc.HasSection("nonexistent"));
    }

    [Fact]
    public void AddVerb_AppendsToMain()
    {
        var doc = new Document();
        doc.AddVerb("answer", new Dictionary<string, object> { ["max_duration"] = 3600 });

        var verbs = doc.GetVerbs("main");
        Assert.Single(verbs);

        var verb = verbs[0];
        Assert.True(verb.ContainsKey("answer"));
    }

    [Fact]
    public void AddVerbToSection_CustomSection()
    {
        var doc = new Document();
        doc.AddSection("custom");
        doc.AddVerbToSection("custom", "play", new Dictionary<string, object> { ["url"] = "https://example.com/audio.mp3" });

        var verbs = doc.GetVerbs("custom");
        Assert.Single(verbs);
        Assert.True(verbs[0].ContainsKey("play"));
    }

    [Fact]
    public void AddVerbToSection_NonexistentSection_Throws()
    {
        var doc = new Document();
        Assert.Throws<InvalidOperationException>(() =>
            doc.AddVerbToSection("missing", "answer", new Dictionary<string, object>()));
    }

    [Fact]
    public void AddRawVerb_AppendsPreformatted()
    {
        var doc = new Document();
        var raw = new Dictionary<string, object> { ["answer"] = new Dictionary<string, object> { ["max_duration"] = 3600 } };
        doc.AddRawVerb("main", raw);

        var verbs = doc.GetVerbs("main");
        Assert.Single(verbs);
        Assert.True(verbs[0].ContainsKey("answer"));
    }

    [Fact]
    public void MultipleVerbs_PreserveOrder()
    {
        var doc = new Document();
        doc.AddVerb("answer", new Dictionary<string, object> { ["max_duration"] = 3600 });
        doc.AddVerb("hangup", new Dictionary<string, object>());

        var verbs = doc.GetVerbs("main");
        Assert.Equal(2, verbs.Count);
        Assert.True(verbs[0].ContainsKey("answer"));
        Assert.True(verbs[1].ContainsKey("hangup"));
    }

    [Fact]
    public void SleepVerb_TakesInteger()
    {
        var doc = new Document();
        doc.AddVerb("sleep", 2000);

        var verbs = doc.GetVerbs("main");
        Assert.Single(verbs);
        Assert.True(verbs[0].ContainsKey("sleep"));
        Assert.Equal(2000, verbs[0]["sleep"]);
    }

    [Fact]
    public void ClearSection_EmptiesButKeepsSection()
    {
        var doc = new Document();
        doc.AddVerb("answer", new Dictionary<string, object>());
        doc.AddVerb("hangup", new Dictionary<string, object>());
        Assert.Equal(2, doc.GetVerbs("main").Count);

        doc.ClearSection("main");
        Assert.Empty(doc.GetVerbs("main"));
        Assert.True(doc.HasSection("main"));
    }

    [Fact]
    public void Reset_ReturnsToInitialState()
    {
        var doc = new Document();
        doc.AddSection("custom");
        doc.AddVerb("answer", new Dictionary<string, object>());
        doc.AddVerbToSection("custom", "hangup", new Dictionary<string, object>());

        doc.Reset();
        Assert.True(doc.HasSection("main"));
        Assert.False(doc.HasSection("custom"));
        Assert.Empty(doc.GetVerbs("main"));
    }

    [Fact]
    public void ToDict_CorrectStructure()
    {
        var doc = new Document();
        doc.AddVerb("answer", new Dictionary<string, object> { ["max_duration"] = 3600 });
        doc.AddVerb("hangup", new Dictionary<string, object>());

        var dict = doc.ToDict();
        Assert.Equal("1.0.0", dict["version"]);
        Assert.True(dict.ContainsKey("sections"));

        var sections = (Dictionary<string, List<Dictionary<string, object>>>)dict["sections"];
        Assert.True(sections.ContainsKey("main"));
        Assert.Equal(2, sections["main"].Count);
    }

    [Fact]
    public void Render_ProducesValidJson()
    {
        var doc = new Document();
        doc.AddVerb("hangup", new Dictionary<string, object>());

        var json = doc.Render();
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        Assert.Equal("1.0.0", root.GetProperty("version").GetString());
        Assert.Equal(1, root.GetProperty("sections").GetProperty("main").GetArrayLength());
    }

    [Fact]
    public void RenderPretty_ContainsNewlines()
    {
        var doc = new Document();
        doc.AddVerb("hangup", new Dictionary<string, object>());

        var json = doc.RenderPretty();
        Assert.Contains("\n", json);

        var parsed = JsonDocument.Parse(json);
        Assert.Equal("1.0.0", parsed.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void JsonRoundTrip_Validation()
    {
        var doc = new Document();
        doc.AddVerb("answer", new Dictionary<string, object> { ["max_duration"] = 3600 });
        doc.AddVerb("sleep", 2000);
        doc.AddVerb("hangup", new Dictionary<string, object>());

        var json = doc.Render();
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        Assert.Equal("1.0.0", root.GetProperty("version").GetString());

        var verbs = root.GetProperty("sections").GetProperty("main");
        Assert.Equal(3, verbs.GetArrayLength());

        // First verb: answer
        Assert.True(verbs[0].TryGetProperty("answer", out _));
        // Second verb: sleep with integer value
        Assert.True(verbs[1].TryGetProperty("sleep", out var sleepVal));
        Assert.Equal(2000, sleepVal.GetInt32());
        // Third verb: hangup
        Assert.True(verbs[2].TryGetProperty("hangup", out _));
    }

    [Fact]
    public void GetVerbs_ReturnsCopy_MutationDoesNotAffectOriginal()
    {
        var doc = new Document();
        doc.AddVerb("answer", new Dictionary<string, object>());

        var verbs = doc.GetVerbs("main");
        verbs.Add(new Dictionary<string, object> { ["extra"] = "should not affect original" });

        Assert.Single(doc.GetVerbs("main"));
    }

    [Fact]
    public void GetVerbs_NonexistentSection_ReturnsEmpty()
    {
        var doc = new Document();
        Assert.Empty(doc.GetVerbs("missing"));
    }
}
