// PromptObjectModelTest.cs
//
// Cross-port parity tests for SignalWire.POM.PromptObjectModel. These
// assert the EXACT byte-for-byte rendered shape of markdown / XML /
// JSON / YAML output so .NET stays in lock-step with the reference
// Python module ``signalwire.pom.pom`` and other ports.
//
// Source-of-truth: signalwire-python/tests/unit/pom/test_pom_render_parity.py
// — every test case here is a 1:1 port of a TestXxx class in that file.

using System.Collections.Generic;
using SignalWire.POM;
using Xunit;

namespace SignalWire.Tests.POM;

[Trait("Category", "POM")]
public class PromptObjectModelTest
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] GuestAGuestBArray = new[] { "GuestA", "GuestB" };
    private static readonly string[] OneTwoThreeArray = new[] { "one", "two", "three" };
    private static readonly string[] TitleBodyArray = new[] { "title", "body" };
    // ----------------------------------------------------------------
    // Empty POM
    // ----------------------------------------------------------------

    [Fact]
    public void EmptyPom_RenderMarkdown_IsEmptyString()
    {
        var pom = new PromptObjectModel();
        Assert.Equal("", pom.RenderMarkdown());
    }

    [Fact]
    public void EmptyPom_RenderXml_IsJustPromptTags()
    {
        var pom = new PromptObjectModel();
        var expected = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<prompt>\n</prompt>";
        Assert.Equal(expected, pom.RenderXml());
    }

    [Fact]
    public void EmptyPom_ToJson_IsEmptyArray()
    {
        var pom = new PromptObjectModel();
        Assert.Equal("[]", pom.ToJson());
    }

    [Fact]
    public void EmptyPom_ToYaml_IsEmptyArrayLine()
    {
        var pom = new PromptObjectModel();
        Assert.Equal("[]\n", pom.ToYaml());
    }

    // ----------------------------------------------------------------
    // Single section with title + body
    // ----------------------------------------------------------------

    [Fact]
    public void SimpleSection_RenderMarkdown_ExactShape()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("Greeting", body: "Hello world");
        var expected = "## Greeting\n\nHello world\n";
        Assert.Equal(expected, pom.RenderMarkdown());
    }

    [Fact]
    public void SimpleSection_RenderXml_ExactShape()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("Greeting", body: "Hello world");
        var expected =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<prompt>\n" +
            "  <section>\n" +
            "    <title>Greeting</title>\n" +
            "    <body>Hello world</body>\n" +
            "  </section>\n" +
            "</prompt>";
        Assert.Equal(expected, pom.RenderXml());
    }

    // ----------------------------------------------------------------
    // Section with bullets
    // ----------------------------------------------------------------

    [Fact]
    public void Bullets_RenderMarkdown_ExactShape()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("Goals", body: "Be helpful",
            bullets: new List<string> { "Be concise", "Be clear" });
        var expected = "## Goals\n\nBe helpful\n\n- Be concise\n- Be clear\n";
        Assert.Equal(expected, pom.RenderMarkdown());
    }

    [Fact]
    public void Bullets_RenderXml_ExactShape()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("Goals", body: "Be helpful",
            bullets: new List<string> { "Be concise", "Be clear" });
        var expected =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<prompt>\n" +
            "  <section>\n" +
            "    <title>Goals</title>\n" +
            "    <body>Be helpful</body>\n" +
            "    <bullets>\n" +
            "      <bullet>Be concise</bullet>\n" +
            "      <bullet>Be clear</bullet>\n" +
            "    </bullets>\n" +
            "  </section>\n" +
            "</prompt>";
        Assert.Equal(expected, pom.RenderXml());
    }

    // ----------------------------------------------------------------
    // Subsections
    // ----------------------------------------------------------------

    [Fact]
    public void Subsections_RenderMarkdown_ExactShape()
    {
        var pom = new PromptObjectModel();
        var s = pom.AddSection("Top", body: "Top body");
        s.AddSubsection("Sub1", body: "Sub1 body",
            bullets: new List<string> { "a", "b" });
        var expected = "## Top\n\nTop body\n\n### Sub1\n\nSub1 body\n\n- a\n- b\n";
        Assert.Equal(expected, pom.RenderMarkdown());
    }

    [Fact]
    public void Subsections_RenderXml_ExactShape()
    {
        var pom = new PromptObjectModel();
        var s = pom.AddSection("Top", body: "Top body");
        s.AddSubsection("Sub1", body: "Sub1 body",
            bullets: new List<string> { "a", "b" });
        var expected =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<prompt>\n" +
            "  <section>\n" +
            "    <title>Top</title>\n" +
            "    <body>Top body</body>\n" +
            "    <subsections>\n" +
            "      <section>\n" +
            "        <title>Sub1</title>\n" +
            "        <body>Sub1 body</body>\n" +
            "        <bullets>\n" +
            "          <bullet>a</bullet>\n" +
            "          <bullet>b</bullet>\n" +
            "        </bullets>\n" +
            "      </section>\n" +
            "    </subsections>\n" +
            "  </section>\n" +
            "</prompt>";
        Assert.Equal(expected, pom.RenderXml());
    }

    // ----------------------------------------------------------------
    // Numbered top-level sections — sibling propagation
    // ----------------------------------------------------------------

    [Fact]
    public void Numbered_RenderMarkdown_PropagatesToSiblings()
    {
        // Once any sibling is numbered=true, all siblings (without
        // explicit numbered=false) get numbered.
        var pom = new PromptObjectModel();
        pom.AddSection("S1", body: "b1", numbered: true);
        pom.AddSection("S2", body: "b2");
        var expected = "## 1. S1\n\nb1\n\n## 2. S2\n\nb2\n";
        Assert.Equal(expected, pom.RenderMarkdown());
    }

    [Fact]
    public void Numbered_RenderXml_Propagates()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("S1", body: "b1", numbered: true);
        pom.AddSection("S2", body: "b2");
        var expected =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<prompt>\n" +
            "  <section>\n" +
            "    <title>1. S1</title>\n" +
            "    <body>b1</body>\n" +
            "  </section>\n" +
            "  <section>\n" +
            "    <title>2. S2</title>\n" +
            "    <body>b2</body>\n" +
            "  </section>\n" +
            "</prompt>";
        Assert.Equal(expected, pom.RenderXml());
    }

    // ----------------------------------------------------------------
    // Numbered bullets
    // ----------------------------------------------------------------

    [Fact]
    public void NumberedBullets_RenderMarkdown_ExactShape()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("X",
            bullets: new List<string> { "one", "two" },
            numberedBullets: true);
        var expected = "## X\n\n1. one\n2. two\n";
        Assert.Equal(expected, pom.RenderMarkdown());
    }

    [Fact]
    public void NumberedBullets_RenderXml_UsesIdAttr()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("X",
            bullets: new List<string> { "one", "two" },
            numberedBullets: true);
        var expected =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<prompt>\n" +
            "  <section>\n" +
            "    <title>X</title>\n" +
            "    <bullets>\n" +
            "      <bullet id=\"1\">one</bullet>\n" +
            "      <bullet id=\"2\">two</bullet>\n" +
            "    </bullets>\n" +
            "  </section>\n" +
            "</prompt>";
        Assert.Equal(expected, pom.RenderXml());
    }

    // ----------------------------------------------------------------
    // JSON / YAML round-trip with exact key order
    // ----------------------------------------------------------------

    [Fact]
    public void ToJson_ExactShape()
    {
        var pom = new PromptObjectModel();
        var s = pom.AddSection("A", body: "ab");
        s.AddSubsection("A1", body: "a1b", bullets: new List<string> { "x" });
        var expected =
            "[\n" +
            "  {\n" +
            "    \"title\": \"A\",\n" +
            "    \"body\": \"ab\",\n" +
            "    \"subsections\": [\n" +
            "      {\n" +
            "        \"title\": \"A1\",\n" +
            "        \"body\": \"a1b\",\n" +
            "        \"bullets\": [\n" +
            "          \"x\"\n" +
            "        ]\n" +
            "      }\n" +
            "    ]\n" +
            "  }\n" +
            "]";
        Assert.Equal(expected, pom.ToJson());
    }

    [Fact]
    public void ToYaml_ExactShape()
    {
        var pom = new PromptObjectModel();
        var s = pom.AddSection("A", body: "ab");
        s.AddSubsection("A1", body: "a1b", bullets: new List<string> { "x" });
        var expected =
            "- title: A\n" +
            "  body: ab\n" +
            "  subsections:\n" +
            "  - title: A1\n" +
            "    body: a1b\n" +
            "    bullets:\n" +
            "    - x\n";
        Assert.Equal(expected, pom.ToYaml());
    }

    /// <summary>
    /// POM serialization is WIRE OUTPUT and must be LF-only on every platform, as
    /// the reference is: Python's json.dumps / yaml.dump hardcode "\n". Both
    /// System.Text.Json's indent newline (pre-.NET-9) and YamlDotNet default to
    /// Environment.NewLine, so this shipped CRLF on Windows.
    ///
    /// The ExactShape tests above already encode LF, but they can only catch this
    /// when the suite RUNS on Windows — which only the multi-OS nightly does (run
    /// 30908589549 caught it there). This one names the invariant directly so the
    /// intent survives, and it fails on a CRLF-emitting build from any host that
    /// sets Environment.NewLine to CRLF.
    /// </summary>
    [Fact]
    public void Serialization_IsLfOnly_OnEveryPlatform()
    {
        var pom = new PromptObjectModel();
        var s = pom.AddSection("A", body: "ab");
        s.AddSubsection("A1", body: "a1b", bullets: new List<string> { "x" });

        var json = pom.ToJson();
        var yaml = pom.ToYaml();

        Assert.DoesNotContain("\r", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", yaml, StringComparison.Ordinal);
        // And the newlines are really there — a no-CR assertion on single-line
        // output would pass vacuously.
        Assert.Contains("\n", json, StringComparison.Ordinal);
        Assert.Contains("\n", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void FromJson_RoundTripPreservesStructure()
    {
        var pom = new PromptObjectModel();
        var s = pom.AddSection("A", body: "ab");
        s.AddSubsection("A1", body: "a1b", bullets: new List<string> { "x", "y" });
        var jsonStr = pom.ToJson();
        var restored = PromptObjectModel.FromJson(jsonStr);
        Assert.Equal(jsonStr, restored.ToJson());
    }

    [Fact]
    public void FromYaml_RoundTripPreservesStructure()
    {
        var pom = new PromptObjectModel();
        var s = pom.AddSection("A", body: "ab");
        s.AddSubsection("A1", body: "a1b", bullets: new List<string> { "x", "y" });
        var yamlStr = pom.ToYaml();
        var restored = PromptObjectModel.FromYaml(yamlStr);
        Assert.Equal(yamlStr, restored.ToYaml());
    }

    // ----------------------------------------------------------------
    // FindSection recursion
    // ----------------------------------------------------------------

    [Fact]
    public void FindSection_TopLevel()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("One", body: "b1");
        pom.AddSection("Two", body: "b2");
        var s = pom.FindSection("Two");
        Assert.NotNull(s);
        Assert.Equal("b2", s!.Body);
    }

    [Fact]
    public void FindSection_RecursesIntoSubsections()
    {
        var pom = new PromptObjectModel();
        var s = pom.AddSection("Outer", body: "ob");
        s.AddSubsection("Inner", body: "ib");
        var found = pom.FindSection("Inner");
        Assert.NotNull(found);
        Assert.Equal("ib", found!.Body);
    }

    [Fact]
    public void FindSection_ReturnsNullForMissing()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("Only", body: "b");
        Assert.Null(pom.FindSection("Missing"));
    }

    // ----------------------------------------------------------------
    // AddPomAsSubsection
    // ----------------------------------------------------------------

    [Fact]
    public void AddPomAsSubsection_ToExistingSectionByTitle()
    {
        var host = new PromptObjectModel();
        host.AddSection("Host", body: "hb");

        var guest = new PromptObjectModel();
        guest.AddSection("Guest", body: "gb");

        host.AddPomAsSubsection("Host", guest);
        var hostSection = host.FindSection("Host");
        Assert.NotNull(hostSection);
        Assert.Single(hostSection!.Subsections);
        Assert.Equal("Guest", hostSection.Subsections[0].Title);
        Assert.Equal("gb", hostSection.Subsections[0].Body);
    }

    [Fact]
    public void AddPomAsSubsection_ToSectionObjectDirectly()
    {
        var host = new PromptObjectModel();
        var target = host.AddSection("Host", body: "hb");

        var guest = new PromptObjectModel();
        guest.AddSection("GuestA", body: "ab");
        guest.AddSection("GuestB", body: "bb");

        host.AddPomAsSubsection(target, guest);
        Assert.Equal(GuestAGuestBArray,
            target.Subsections.Select(s => s.Title).ToArray());
    }

    [Fact]
    public void AddPomAsSubsection_ThrowsOnMissingTitle()
    {
        var host = new PromptObjectModel();
        host.AddSection("Real", body: "b");
        var guest = new PromptObjectModel();
        guest.AddSection("G", body: "gb");
        Assert.Throws<System.ArgumentException>(() =>
            host.AddPomAsSubsection("Missing", guest));
    }

    // ----------------------------------------------------------------
    // Section.AddBody / AddBullets / AddSubsection
    // ----------------------------------------------------------------

    [Fact]
    public void Section_AddBody_Replaces()
    {
        var s = new Section("X", body: "initial");
        s.AddBody("replacement");
        Assert.Equal("replacement", s.Body);
    }

    [Fact]
    public void Section_AddBullets_Appends()
    {
        var s = new Section("X");
        s.AddBullets(new List<string> { "one" });
        s.AddBullets(new List<string> { "two", "three" });
        Assert.Equal(OneTwoThreeArray, s.Bullets.ToArray());
    }

    [Fact]
    public void Section_AddSubsection_ReturnsSection()
    {
        var parent = new Section("P");
        var child = parent.AddSubsection("C", body: "cb");
        Assert.IsType<Section>(child);
        Assert.Equal("C", child.Title);
        Assert.Single(parent.Subsections);
        Assert.Same(child, parent.Subsections[0]);
    }

    [Fact]
    public void Section_AddSubsection_RequiresTitle()
    {
        var parent = new Section("P");
        Assert.Throws<System.ArgumentException>(() =>
#pragma warning disable CS8625 // passing null IS the assertion: this proves
        // AddSubsection rejects a null title with ArgumentException.
            parent.AddSubsection(title: null!));
#pragma warning restore CS8625
    }

    // ----------------------------------------------------------------
    // PromptObjectModel.AddSection: only first may have null title
    // ----------------------------------------------------------------

    [Fact]
    public void AddSection_FirstUntitledOk_SecondUntitledThrows()
    {
        var pom = new PromptObjectModel();
        // First untitled is fine.
        pom.AddSection(title: null, body: "first");
        // Adding a second untitled throws.
        Assert.Throws<System.ArgumentException>(() =>
            pom.AddSection(title: null, body: "second"));
    }

    // ----------------------------------------------------------------
    // ToDict key order + omission semantics
    // ----------------------------------------------------------------

    [Fact]
    public void ToDict_OmitsEmptyFields_PreservesKeyOrder()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("Only", body: "b");
        var dicts = pom.ToDict();
        Assert.Single(dicts);
        var keys = dicts[0].Keys.ToList();
        // Section with only title + body emits exactly those two keys
        // in that order — no empty bullets/subsections/numbered.
        Assert.Equal(TitleBodyArray, keys.ToArray());
    }

    [Fact]
    public void ToDict_NumberedFalse_OmitsNumberedKey()
    {
        // Python only emits "numbered" when it's truthy.
        var pom = new PromptObjectModel();
        pom.AddSection("S", body: "b", numbered: false);
        var dicts = pom.ToDict();
        Assert.False(dicts[0].ContainsKey("numbered"));
    }

    [Fact]
    public void ToDict_NumberedTrue_EmitsNumberedKey()
    {
        var pom = new PromptObjectModel();
        pom.AddSection("S", body: "b", numbered: true);
        var dicts = pom.ToDict();
        Assert.True(dicts[0].ContainsKey("numbered"));
        Assert.Equal(true, dicts[0]["numbered"]);
    }
}
