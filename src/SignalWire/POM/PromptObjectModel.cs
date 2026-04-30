// PromptObjectModel.cs
//
// Cross-language port of Python's signalwire.pom.pom — the structured
// prompt-object data model. Users build a hierarchical prompt as a tree
// of sections with title/body/bullets/subsections, then render to
// markdown, XML, or a serializable dict for transport.
//
// Mirrors the Python public surface:
//   - PromptObjectModel: AddSection, FindSection, RenderMarkdown,
//     RenderXml, ToDict, ToJson, FromJson, Sections (read-only).
//   - Section: Title, Body, Bullets, Subsections, AddBody, AddBullets,
//     AddSubsection, RenderMarkdown, RenderXml, ToDict.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SignalWire.POM;

public class Section
{
    public string Title { get; set; }
    public string Body { get; set; }
    public List<string> Bullets { get; }
    public bool Numbered { get; set; }
    public bool NumberedBullets { get; set; }
    public List<Section> Subsections { get; }

    public Section(
        string title = "",
        string body = "",
        List<string>? bullets = null,
        bool numbered = false,
        bool numberedBullets = false)
    {
        Title = title;
        Body = body;
        Bullets = bullets is null ? new List<string>() : new List<string>(bullets);
        Numbered = numbered;
        NumberedBullets = numberedBullets;
        Subsections = new List<Section>();
    }

    /// <summary>Set or replace this section's body text.
    /// (Python parity: ``Section.add_body``.)</summary>
    public Section AddBody(string body)
    {
        Body = body;
        return this;
    }

    /// <summary>Append bullets to this section.
    /// (Python parity: ``Section.add_bullets``.)</summary>
    public Section AddBullets(List<string> bullets)
    {
        Bullets.AddRange(bullets);
        return this;
    }

    /// <summary>Add a subsection under this section, returning the new
    /// Section. (Python parity: ``Section.add_subsection``.)</summary>
    public Section AddSubsection(
        string title = "",
        string body = "",
        List<string>? bullets = null,
        bool numbered = false,
        bool numberedBullets = false)
    {
        var sub = new Section(title, body, bullets, numbered, numberedBullets);
        Subsections.Add(sub);
        return sub;
    }

    /// <summary>Render this section as a markdown fragment, indented at
    /// the given header level (default 2).</summary>
    public string RenderMarkdown(int level = 2, string sectionNumber = "")
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(Title))
        {
            sb.Append(new string('#', level));
            sb.Append(' ');
            if (!string.IsNullOrEmpty(sectionNumber)) sb.Append(sectionNumber).Append(' ');
            sb.AppendLine(Title);
            sb.AppendLine();
        }
        if (!string.IsNullOrEmpty(Body))
        {
            sb.AppendLine(Body);
            sb.AppendLine();
        }
        if (Bullets.Count > 0)
        {
            for (int i = 0; i < Bullets.Count; i++)
            {
                var prefix = NumberedBullets ? $"{i + 1}." : "-";
                sb.AppendLine($"{prefix} {Bullets[i]}");
            }
            sb.AppendLine();
        }
        for (int i = 0; i < Subsections.Count; i++)
        {
            var subNum = Numbered ? $"{(string.IsNullOrEmpty(sectionNumber) ? "" : sectionNumber + ".")}{i + 1}" : "";
            sb.Append(Subsections[i].RenderMarkdown(level + 1, subNum));
        }
        return sb.ToString();
    }

    /// <summary>Render this section as an XML fragment.</summary>
    public string RenderXml(int indent = 0, string sectionNumber = "")
    {
        var pad = new string(' ', indent * 2);
        var sb = new StringBuilder();
        sb.Append(pad).AppendLine("<section>");
        if (!string.IsNullOrEmpty(Title))
            sb.Append(pad).Append("  <title>").Append(System.Net.WebUtility.HtmlEncode(Title)).AppendLine("</title>");
        if (!string.IsNullOrEmpty(Body))
            sb.Append(pad).Append("  <body>").Append(System.Net.WebUtility.HtmlEncode(Body)).AppendLine("</body>");
        foreach (var bullet in Bullets)
            sb.Append(pad).Append("  <bullet>").Append(System.Net.WebUtility.HtmlEncode(bullet)).AppendLine("</bullet>");
        foreach (var sub in Subsections)
            sb.Append(sub.RenderXml(indent + 1));
        sb.Append(pad).AppendLine("</section>");
        return sb.ToString();
    }

    /// <summary>Serialize to a Dictionary suitable for JSON.</summary>
    public Dictionary<string, object> ToDict()
    {
        var d = new Dictionary<string, object>
        {
            ["title"] = Title,
            ["body"] = Body,
            ["bullets"] = new List<string>(Bullets),
            ["numbered"] = Numbered,
            ["numberedBullets"] = NumberedBullets,
            ["subsections"] = Subsections.Select(s => s.ToDict()).ToList(),
        };
        return d;
    }
}

public class PromptObjectModel
{
    private readonly List<Section> _sections;
    public bool Debug { get; }

    public IReadOnlyList<Section> Sections => _sections;

    public PromptObjectModel(bool debug = false)
    {
        Debug = debug;
        _sections = new List<Section>();
    }

    /// <summary>Add a top-level section to the model, returning the new
    /// Section. (Python parity: ``PromptObjectModel.add_section``.)</summary>
    public Section AddSection(
        string title = "",
        string body = "",
        List<string>? bullets = null,
        bool numbered = false,
        bool numberedBullets = false)
    {
        var s = new Section(title, body, bullets, numbered, numberedBullets);
        _sections.Add(s);
        return s;
    }

    /// <summary>Recursively find a section by title. Returns null if not
    /// found. (Python parity: ``PromptObjectModel.find_section``.)</summary>
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

    /// <summary>Render the model as markdown.</summary>
    public string RenderMarkdown()
    {
        var sb = new StringBuilder();
        foreach (var s in _sections)
        {
            sb.Append(s.RenderMarkdown(level: 2));
        }
        return sb.ToString();
    }

    /// <summary>Render the model as XML.</summary>
    public string RenderXml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<prompt>");
        foreach (var s in _sections)
        {
            sb.Append(s.RenderXml(indent: 1));
        }
        sb.AppendLine("</prompt>");
        return sb.ToString();
    }

    /// <summary>Serialize to a list of dicts (matches Python's to_dict
    /// which returns a List rather than a Dict).</summary>
    public List<Dictionary<string, object>> ToDict() =>
        _sections.Select(s => s.ToDict()).ToList();

    /// <summary>Serialize to JSON string.</summary>
    public string ToJson() => JsonSerializer.Serialize(ToDict());

    /// <summary>Serialize to YAML — not supported in .NET (no built-in
    /// YAML serializer; users should use a YAML library for this).</summary>
    public string ToYaml() =>
        throw new NotSupportedException(
            "ToYaml is not supported in .NET — use a YAML library against ToDict()");

    /// <summary>Construct a PromptObjectModel from JSON.</summary>
    public static PromptObjectModel FromJson(string json)
    {
        var pom = new PromptObjectModel();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return pom;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            pom._sections.Add(SectionFromJson(el));
        }
        return pom;
    }

    private static Section SectionFromJson(JsonElement el)
    {
        var s = new Section();
        if (el.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
            s.Title = t.GetString() ?? "";
        if (el.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String)
            s.Body = b.GetString() ?? "";
        if (el.TryGetProperty("bullets", out var bs) && bs.ValueKind == JsonValueKind.Array)
            foreach (var item in bs.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    s.Bullets.Add(item.GetString() ?? "");
        if (el.TryGetProperty("numbered", out var n) && n.ValueKind == JsonValueKind.True)
            s.Numbered = true;
        if (el.TryGetProperty("numberedBullets", out var nb) && nb.ValueKind == JsonValueKind.True)
            s.NumberedBullets = true;
        if (el.TryGetProperty("subsections", out var subs) && subs.ValueKind == JsonValueKind.Array)
            foreach (var sub in subs.EnumerateArray())
                s.Subsections.Add(SectionFromJson(sub));
        return s;
    }

    /// <summary>Add a PromptObjectModel as a subsection of an existing
    /// section in this model. (Python parity:
    /// ``PromptObjectModel.add_pom_as_subsection``.)</summary>
    public void AddPomAsSubsection(string targetTitle, PromptObjectModel pomToAdd)
    {
        var target = FindSection(targetTitle);
        if (target is null)
            throw new ArgumentException($"Target section '{targetTitle}' not found");
        foreach (var s in pomToAdd.Sections)
        {
            target.Subsections.Add(s);
        }
    }
}
