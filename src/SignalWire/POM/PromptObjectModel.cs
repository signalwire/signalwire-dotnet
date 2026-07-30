// PromptObjectModel.cs
//
// Cross-language port of Python's signalwire.pom.pom — the structured
// prompt-object data model. Users build a hierarchical prompt as a tree
// of sections with title/body/bullets/subsections, then render to
// markdown, XML, or a serializable dict for transport.
//
// Mirrors the Python public surface AND the exact byte-for-byte output
// of render_markdown / render_xml / to_json / to_yaml. See
// tests/POM/PromptObjectModelTest.cs and Python's
// tests/unit/pom/test_pom_render_parity.py for the reference shapes.
//
//   - PromptObjectModel: AddSection, FindSection, RenderMarkdown,
//     RenderXml, ToDict, ToJson, FromJson, ToYaml, FromYaml,
//     AddPomAsSubsection, Sections, Debug.
//   - Section: Title, Body, Bullets, Subsections, Numbered,
//     NumberedBullets, AddBody, AddBullets, AddSubsection,
//     RenderMarkdown, RenderXml, ToDict.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SignalWire.POM;

/// <summary>
/// One node of a <see cref="PromptObjectModel"/> tree: a title, a body, a
/// bullet list, and nested subsections. Content methods
/// (<see cref="AddBody"/>, <see cref="AddBullets"/>) return <c>this</c> for
/// chaining, while <see cref="AddSubsection"/> returns the <i>new</i> child
/// so it can be configured in turn.
///
/// <para>Note the asymmetry between the two content setters:
/// <see cref="AddBody"/> <b>replaces</b> the body, whereas
/// <see cref="AddBullets"/> <b>appends</b> to the existing bullets.</para>
///
/// <para><b>Titles.</b> A subsection must always have a title —
/// <see cref="AddSubsection"/> throws on an explicit null. Only a
/// top-level section may be untitled, and only the first one; that rule is
/// enforced by <see cref="PromptObjectModel.AddSection"/>.</para>
///
/// <para><b>Numbering</b> is decided per sibling group, not per section.
/// If any section in a group has <see cref="Numbered"/> <c>== true</c>,
/// the whole group is numbered except members that explicitly set it to
/// <c>false</c> — which is why the property is tri-state
/// (null = inherit). <see cref="NumberedBullets"/> is independent and
/// applies only to this section's own bullets. An untitled section
/// carrying no section number does not open a new heading level: its
/// children render at the same level, so an untitled wrapper adds
/// structure without adding depth.</para>
///
/// <para><b>Rendering</b> is byte-for-byte identical to the Python SDK in
/// all three forms — markdown, XML, and dict. Note that
/// <see cref="RenderXml"/> performs <b>no</b> XML escaping (matching the
/// Python SDK), so a body containing <c>&lt;</c> or <c>&amp;</c> produces
/// output that is not well-formed XML.</para>
/// </summary>
public class Section
{
    /// <summary>Section title. Null for the (allowed) first untitled
    /// top-level section.
    /// </summary>
    public string? Title { get; set; }

    public string Body { get; set; }

    private readonly List<string> _bullets;

    public IReadOnlyList<string> Bullets => _bullets;

    /// <summary>Mutable backing store for <see cref="Bullets"/>, used by
    /// builders within the assembly to append bullets in place.</summary>
    internal List<string> BulletsMutable => _bullets;

    /// <summary>Three-state numbering: null = inherit, true = force on,
    /// false = force off.
    ///  Sibling propagation: if any sibling at the
    /// same level has Numbered==true, all siblings get numbered unless
    /// they have Numbered==false.</summary>
    public bool? Numbered { get; set; }

    public bool NumberedBullets { get; set; }

    private readonly List<Section> _subsections;

    public IReadOnlyList<Section> Subsections => _subsections;

    /// <summary>Mutable backing store for <see cref="Subsections"/>, used by
    /// builders within the assembly to append subsections in place.</summary>
    internal List<Section> SubsectionsMutable => _subsections;

    public Section(
        string? title = null,
        string body = "",
        IReadOnlyList<string>? bullets = null,
        bool? numbered = null,
        bool numberedBullets = false)
    {
        Title = title;
        Body = body;
        _bullets = bullets is null ? new List<string>() : new List<string>(bullets);
        Numbered = numbered;
        NumberedBullets = numberedBullets;
        _subsections = new List<Section>();
    }

    /// <summary>Set or replace this section's body text.</summary>
    public Section AddBody(string body)
    {
        Body = body;
        return this;
    }

    /// <summary>Append bullets to this section.</summary>
    public Section AddBullets(IReadOnlyList<string> bullets)
    {
        ArgumentNullException.ThrowIfNull(bullets);
        _bullets.AddRange(bullets);
        return this;
    }

    /// <summary>Add a subsection under this section, returning the new
    /// Section.</summary>
    public Section AddSubsection(
        string title,
        string body = "",
        IReadOnlyList<string>? bullets = null,
        bool numbered = false,
        bool numberedBullets = false)
    {
        // The reference REQUIRES a title (``add_subsection(self, title: str, *, …)``)
        // and raises ValueError when it is None. Omitting the argument is now a
        // C# compile error; an EXPLICIT null still raises the port's ValueError
        // analogue (ArgumentException), preserving the reference's behaviour.
        if (title is null)
            throw new ArgumentException("Subsections must have a title", nameof(title));
        var sub = new Section(title, body, bullets, numbered, numberedBullets);
        _subsections.Add(sub);
        return sub;
    }

    /// <summary>Render this section as a markdown fragment, indented at
    /// the given header level (default 2). Mirrors Python's
    /// ``Section.render_markdown`` exactly.</summary>
    public string RenderMarkdown(int level = 2, IReadOnlyList<int>? sectionNumber = null)
    {
        sectionNumber ??= new List<int>();
        var md = new List<string>();

        // Title with optional numbering
        if (Title is not null)
        {
            string prefix = "";
            if (sectionNumber.Count > 0)
                prefix = string.Join(".", sectionNumber.Select(n => n.ToString(CultureInfo.InvariantCulture))) + ". ";
            md.Add($"{new string('#', level)} {prefix}{Title}\n");
        }

        // Body
        if (!string.IsNullOrEmpty(Body))
            md.Add($"{Body}\n");

        // Bullets
        for (int i = 0; i < Bullets.Count; i++)
        {
            if (NumberedBullets)
                md.Add($"{i + 1}. {Bullets[i]}");
            else
                md.Add($"- {Bullets[i]}");
        }
        if (Bullets.Count > 0)
            md.Add("");

        // Sibling-numbering propagation: once any subsection in this
        // group has Numbered==true, all siblings get numbered unless
        // they explicitly set Numbered==false.
        bool anySubsectionNumbered = Subsections.Any(s => s.Numbered == true);

        for (int i = 0; i < Subsections.Count; i++)
        {
            var subsection = Subsections[i];
            IReadOnlyList<int> newSectionNumber;
            int nextLevel;
            if (Title is not null || sectionNumber.Count > 0)
            {
                if (anySubsectionNumbered && subsection.Numbered != false)
                {
                    newSectionNumber = new List<int>(sectionNumber) { i + 1 };
                }
                else
                {
                    newSectionNumber = sectionNumber;
                }
                nextLevel = level + 1;
            }
            else
            {
                newSectionNumber = sectionNumber;
                nextLevel = level;
            }
            md.Add(subsection.RenderMarkdown(nextLevel, newSectionNumber));
        }

        return string.Join("\n", md);
    }

    /// <summary>Render this section as an XML fragment. Mirrors
    /// Python's ``Section.render_xml`` exactly (no HTML escaping; uses
    /// <c>&lt;bullets&gt;</c> / <c>&lt;subsections&gt;</c> wrapping
    /// containers).</summary>
    public string RenderXml(int indent = 0, IReadOnlyList<int>? sectionNumber = null)
    {
        sectionNumber ??= new List<int>();
        string indentStr = new string(' ', indent * 2);
        var xml = new List<string>();

        xml.Add($"{indentStr}<section>");

        if (Title is not null)
        {
            string prefix = "";
            if (sectionNumber.Count > 0)
                prefix = string.Join(".", sectionNumber.Select(n => n.ToString(CultureInfo.InvariantCulture))) + ". ";
            xml.Add($"{indentStr}  <title>{prefix}{Title}</title>");
        }

        if (!string.IsNullOrEmpty(Body))
            xml.Add($"{indentStr}  <body>{Body}</body>");

        if (Bullets.Count > 0)
        {
            xml.Add($"{indentStr}  <bullets>");
            for (int i = 0; i < Bullets.Count; i++)
            {
                if (NumberedBullets)
                    xml.Add($"{indentStr}    <bullet id=\"{i + 1}\">{Bullets[i]}</bullet>");
                else
                    xml.Add($"{indentStr}    <bullet>{Bullets[i]}</bullet>");
            }
            xml.Add($"{indentStr}  </bullets>");
        }

        if (Subsections.Count > 0)
        {
            xml.Add($"{indentStr}  <subsections>");
            bool anySubsectionNumbered = Subsections.Any(s => s.Numbered == true);

            for (int i = 0; i < Subsections.Count; i++)
            {
                var subsection = Subsections[i];
                IReadOnlyList<int> newSectionNumber;
                if (Title is not null || sectionNumber.Count > 0)
                {
                    if (anySubsectionNumbered && subsection.Numbered != false)
                        newSectionNumber = new List<int>(sectionNumber) { i + 1 };
                    else
                        newSectionNumber = sectionNumber;
                }
                else
                {
                    newSectionNumber = sectionNumber;
                }
                xml.Add(subsection.RenderXml(indent + 2, newSectionNumber));
            }
            xml.Add($"{indentStr}  </subsections>");
        }

        xml.Add($"{indentStr}</section>");
        return string.Join("\n", xml);
    }

    /// <summary>Serialize to a Dictionary suitable for JSON. Emits
    /// keys in this exact order: title, body, bullets, subsections,
    /// numbered, numberedBullets — and only when non-empty / non-null /
    /// non-default.</summary>
    public Dictionary<string, object> ToDict()
    {
        var d = new Dictionary<string, object>();

        if (Title is not null)
            d["title"] = Title;
        if (!string.IsNullOrEmpty(Body))
            d["body"] = Body;
        if (Bullets.Count > 0)
            d["bullets"] = new List<string>(Bullets);
        if (Subsections.Count > 0)
            d["subsections"] = Subsections.Select(s => s.ToDict()).ToList();
        if (Numbered == true)
            d["numbered"] = true;
        if (NumberedBullets)
            d["numberedBullets"] = true;

        return d;
    }
}

/// <summary>
/// A structured AI prompt held as a tree of <see cref="Section"/>s, rendered
/// on demand to markdown, XML, or a serializable dict. This is the data
/// model behind the SWML <c>ai</c> verb's <c>prompt.pom</c> form — the
/// alternative to handing the engine one opaque prompt string.
///
/// <para>Build it directly with <see cref="AddSection"/> (which returns the
/// new section for further nesting), or fluently through
/// <see cref="PomBuilder"/>. Round-trip it with
/// <see cref="ToJson"/>/<see cref="FromJson"/> or
/// <see cref="ToYaml"/>/<see cref="FromYaml"/>, and compose two models with
/// <see cref="AddPomAsSubsection(string, PromptObjectModel)"/>.</para>
///
/// <para><b>Only the first top-level section may be untitled</b>;
/// <see cref="AddSection"/> throws <see cref="ArgumentException"/> for an
/// untitled section added after any other. <see cref="FindSection"/>
/// searches the whole tree depth-first and returns the first title match,
/// so duplicate titles are resolvable only by holding the
/// <see cref="Section"/> reference.</para>
///
/// <para><b>The exact output is a contract, not a convenience:</b> markdown,
/// XML, JSON, and YAML output are matched byte-for-byte against the Python
/// SDK's <c>render_markdown</c> / <c>render_xml</c> / <c>to_json</c> /
/// <c>to_yaml</c>, so a prompt built here and one built with any other
/// SignalWire SDK produce identical engine input. Changing the rendering is
/// a breaking wire change.</para>
///
/// <para><see cref="Debug"/> is carried on the model but does not affect
/// rendering.</para>
/// </summary>
public class PromptObjectModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly List<Section> _sections;
    public bool Debug { get; set; }

    public IReadOnlyList<Section> Sections => _sections;

    public PromptObjectModel(bool debug = false)
    {
        Debug = debug;
        _sections = new List<Section>();
    }

    /// <summary>Add a top-level section to the model, returning the new
    /// Section. Only the first added section may have a null title.
    /// </summary>
    public Section AddSection(
        string? title = null,
        string body = "",
        IReadOnlyList<string>? bullets = null,
        bool? numbered = null,
        bool numberedBullets = false)
    {
        if (title is null && _sections.Count > 0)
            throw new ArgumentException("Only the first section can have no title");

        var s = new Section(title, body, bullets, numbered, numberedBullets);
        _sections.Add(s);
        return s;
    }

    /// <summary>Recursively find a section by title. Returns null if not
    /// found.</summary>
    public Section? FindSection(string title)
    {
        foreach (var s in _sections)
        {
            var match = FindSectionRecursive(s, title);
            if (match is not null) return match;
        }
        return null;
    }

    private static Section? FindSectionRecursive(Section section, string title)
    {
        if (section.Title == title) return section;
        foreach (var sub in section.Subsections)
        {
            var match = FindSectionRecursive(sub, title);
            if (match is not null) return match;
        }
        return null;
    }

    /// <summary>Render the model as markdown. Mirrors Python's
    /// ``PromptObjectModel.render_markdown`` exactly.</summary>
    public string RenderMarkdown()
    {
        bool anySectionNumbered = _sections.Any(s => s.Numbered == true);
        var md = new List<string>();
        int sectionCounter = 0;

        foreach (var section in _sections)
        {
            List<int> sectionNumber;
            if (section.Title is not null)
            {
                sectionCounter++;
                if (anySectionNumbered && section.Numbered != false)
                    sectionNumber = new List<int> { sectionCounter };
                else
                    sectionNumber = new List<int>();
            }
            else
            {
                sectionNumber = new List<int>();
            }

            md.Add(section.RenderMarkdown(2, sectionNumber));
        }

        return string.Join("\n", md);
    }

    /// <summary>Render the model as XML. Mirrors Python's
    /// ``PromptObjectModel.render_xml`` exactly (XML preamble +
    /// <c>&lt;prompt&gt;</c> wrapper + indent of 2 spaces per level).</summary>
    public string RenderXml()
    {
        var xml = new List<string>
        {
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            "<prompt>",
        };

        bool anySectionNumbered = _sections.Any(s => s.Numbered == true);
        int sectionCounter = 0;

        foreach (var section in _sections)
        {
            List<int> sectionNumber;
            if (section.Title is not null)
            {
                sectionCounter++;
                if (anySectionNumbered && section.Numbered != false)
                    sectionNumber = new List<int> { sectionCounter };
                else
                    sectionNumber = new List<int>();
            }
            else
            {
                sectionNumber = new List<int>();
            }
            xml.Add(section.RenderXml(1, sectionNumber));
        }

        xml.Add("</prompt>");
        return string.Join("\n", xml);
    }

    /// <summary>Serialize to a list of dicts (matches Python's to_dict
    /// which returns a List rather than a Dict).</summary>
    public IReadOnlyList<Dictionary<string, object>> ToDict() =>
        _sections.Select(s => s.ToDict()).ToList();

    /// <summary>Serialize to JSON string with 2-space indent and Python
    /// dict-style formatting. Empty model emits ``"[]"``.</summary>
    public string ToJson()
    {
        if (_sections.Count == 0) return "[]";
        // System.Text.Json indents with 2 spaces by default in .NET 9+.
        var dicts = ToDict();
        return JsonSerializer.Serialize(dicts, JsonOptions);
    }

    /// <summary>Serialize to YAML string. Matches PyYAML's
    /// ``yaml.dump(data, default_flow_style=False, sort_keys=False)``
    /// exactly. Empty model emits ``"[]\n"``.</summary>
    public string ToYaml()
    {
        if (_sections.Count == 0) return "[]\n";
        var sectionDicts = ToDict();
        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.NullNamingConvention.Instance)
            .DisableAliases()
            .Build();
        return serializer.Serialize(sectionDicts);
    }

    /// <summary>Construct a PromptObjectModel from YAML string.</summary>
    public static PromptObjectModel FromYaml(string yaml)
    {
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.NullNamingConvention.Instance)
            .Build();
        var raw = deserializer.Deserialize<object?>(yaml);
        var json = JsonSerializer.Serialize(NormalizeYamlObject(raw));
        return FromJson(json);
    }

    private static object? NormalizeYamlObject(object? obj)
    {
        switch (obj)
        {
            case Dictionary<object, object?> map:
                {
                    var result = new Dictionary<string, object?>();
                    foreach (var kv in map)
                    {
                        var key = kv.Key?.ToString() ?? "";
                        result[key] = NormalizeYamlObject(kv.Value);
                    }
                    return result;
                }
            case List<object?> list:
                return list.ConvertAll(NormalizeYamlObject);
            default:
                return obj;
        }
    }

    /// <summary>Construct a PromptObjectModel from JSON.</summary>
    public static PromptObjectModel FromJson(string json)
    {
        var pom = new PromptObjectModel();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return pom;

        int idx = 0;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            // Python's _from_dict: if i > 0 and 'title' not in sec: sec['title'] = "Untitled Section"
            bool isFirst = (idx == 0);
            pom._sections.Add(SectionFromJson(el, isSubsection: false, isFirst: isFirst));
            idx++;
        }
        return pom;
    }

    private static Section SectionFromJson(JsonElement el, bool isSubsection, bool isFirst = false)
    {
        // Python validation rules from _from_dict.build_section
        if (isSubsection && (!el.TryGetProperty("title", out _) ||
                             el.TryGetProperty("title", out var checkT) && checkT.ValueKind == JsonValueKind.Null))
            throw new ArgumentException("All subsections must have a title");

        bool hasBody = el.TryGetProperty("body", out var bodyEl) &&
                       bodyEl.ValueKind == JsonValueKind.String &&
                       !string.IsNullOrEmpty(bodyEl.GetString());
        bool hasBullets = el.TryGetProperty("bullets", out var bulletsEl) &&
                          bulletsEl.ValueKind == JsonValueKind.Array &&
                          bulletsEl.GetArrayLength() > 0;
        bool hasSubsections = el.TryGetProperty("subsections", out var subsEl) &&
                              subsEl.ValueKind == JsonValueKind.Array &&
                              subsEl.GetArrayLength() > 0;

        if (!hasBody && !hasBullets && !hasSubsections)
            throw new ArgumentException(
                "All sections must have either a non-empty body, non-empty bullets, or subsections");

        string? title = null;
        if (el.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
            title = t.GetString();
        else if (!isSubsection && !isFirst)
            title = "Untitled Section"; // Python applies this fallback at the top level

        var s = new Section(title);
        if (hasBody)
            s.Body = bodyEl.GetString() ?? "";

        if (hasBullets)
        {
            foreach (var item in bulletsEl.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    s.BulletsMutable.Add(item.GetString() ?? "");
        }

        if (el.TryGetProperty("numbered", out var n))
        {
            if (n.ValueKind == JsonValueKind.True) s.Numbered = true;
            else if (n.ValueKind == JsonValueKind.False) s.Numbered = false;
        }
        if (el.TryGetProperty("numberedBullets", out var nb) && nb.ValueKind == JsonValueKind.True)
            s.NumberedBullets = true;

        if (hasSubsections)
        {
            foreach (var sub in subsEl.EnumerateArray())
                s.SubsectionsMutable.Add(SectionFromJson(sub, isSubsection: true));
        }

        return s;
    }

    /// <summary>Add a PromptObjectModel as a subsection of an existing
    /// section in this model, identified by title.
    /// </summary>
    public void AddPomAsSubsection(string targetTitle, PromptObjectModel pomToAdd)
    {
        ArgumentNullException.ThrowIfNull(pomToAdd);
        var target = FindSection(targetTitle);
        if (target is null)
            throw new ArgumentException($"No section with title '{targetTitle}' found.");
        AddPomAsSubsection(target, pomToAdd);
    }

    /// <summary>Add a PromptObjectModel as a subsection of an existing
    /// Section object directly. Overload mirrors Python's polymorphism.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1822:Mark members as static",
        Justification = "Instance overload mirrors the AddPomAsSubsection(string, ...) sibling and the cross-port instance surface.")]
    public void AddPomAsSubsection(Section target, PromptObjectModel pomToAdd)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pomToAdd);
        foreach (var s in pomToAdd.Sections)
            target.SubsectionsMutable.Add(s);
    }
}
