// PomBuilder.cs
//
// Fluent builder wrapping PromptObjectModel. Mirrors Python's
// signalwire.core.pom_builder.PomBuilder — section auto-vivification on
// AddToSection / AddSubsection, fluent chaining, render+serialize
// passthroughs to the underlying PromptObjectModel.

using System.Collections.Generic;
using System.Text.Json;

namespace SignalWire.POM;

public class PomBuilder
{
    public PromptObjectModel Pom { get; private set; }
    private readonly Dictionary<string, Section> _sections;

    public PomBuilder()
    {
        Pom = new PromptObjectModel();
        _sections = new Dictionary<string, Section>();
    }

    /// <summary>Add a new section. (Python parity:
    /// ``PomBuilder.add_section``.)</summary>
    public PomBuilder AddSection(
        string title,
        string body = "",
        List<string>? bullets = null,
        bool numbered = false,
        bool numberedBullets = false,
        List<Dictionary<string, object>>? subsections = null)
    {
        var section = Pom.AddSection(title, body, bullets, numbered, numberedBullets);
        _sections[title] = section;
        if (subsections is not null)
        {
            foreach (var sub in subsections)
            {
                var subTitle = sub.TryGetValue("title", out var t) ? t as string ?? "" : "";
                var subBody = sub.TryGetValue("body", out var b) ? b as string ?? "" : "";
                List<string>? subBullets = null;
                if (sub.TryGetValue("bullets", out var bs) && bs is List<string> bl)
                    subBullets = bl;
                section.AddSubsection(subTitle, subBody, subBullets);
            }
        }
        return this;
    }

    /// <summary>Add content to an existing section (auto-vivifies if
    /// missing). (Python parity: ``PomBuilder.add_to_section``.)</summary>
    public PomBuilder AddToSection(
        string title,
        string? body = null,
        string? bullet = null,
        List<string>? bullets = null)
    {
        if (!_sections.ContainsKey(title))
        {
            AddSection(title);
        }
        var section = _sections[title];
        if (!string.IsNullOrEmpty(body))
        {
            section.Body = string.IsNullOrEmpty(section.Body)
                ? body
                : $"{section.Body}\n\n{body}";
        }
        if (!string.IsNullOrEmpty(bullet))
        {
            section.Bullets.Add(bullet);
        }
        if (bullets is not null)
        {
            section.Bullets.AddRange(bullets);
        }
        return this;
    }

    /// <summary>Add a subsection under an existing section
    /// (auto-vivifies parent if missing). (Python parity:
    /// ``PomBuilder.add_subsection``.)</summary>
    public PomBuilder AddSubsection(
        string parentTitle,
        string title,
        string body = "",
        List<string>? bullets = null)
    {
        if (!_sections.ContainsKey(parentTitle))
        {
            AddSection(parentTitle);
        }
        _sections[parentTitle].AddSubsection(title, body, bullets);
        return this;
    }

    /// <summary>Check if a section with the given title exists.
    /// (Python parity: ``PomBuilder.has_section``.)</summary>
    public bool HasSection(string title) => _sections.ContainsKey(title);

    /// <summary>Get a section by title, or null if absent.
    /// (Python parity: ``PomBuilder.get_section``.)</summary>
    public Section? GetSection(string title) =>
        _sections.TryGetValue(title, out var s) ? s : null;

    /// <summary>Render the POM as markdown.</summary>
    public string RenderMarkdown() => Pom.RenderMarkdown();

    /// <summary>Render the POM as XML.</summary>
    public string RenderXml() => Pom.RenderXml();

    /// <summary>Serialize the POM to a list of section dicts.</summary>
    public List<Dictionary<string, object>> ToDict() => Pom.ToDict();

    /// <summary>Serialize the POM to a JSON string.</summary>
    public string ToJson() => Pom.ToJson();

    /// <summary>Build a PomBuilder from a list of section dicts.
    /// (Python parity: ``PomBuilder.from_sections`` classmethod.)</summary>
    public static PomBuilder FromSections(List<Dictionary<string, object>> sections)
    {
        var builder = new PomBuilder();
        var json = JsonSerializer.Serialize(sections);
        builder.Pom = PromptObjectModel.FromJson(json);
        // Rebuild the sections lookup so HasSection / GetSection work.
        // Only titled sections are indexable (Python parity:
        // pom_builder.from_sections guards with ``if section.title``).
        // Section.Title is nullable and Dictionary rejects null keys.
        foreach (var s in builder.Pom.Sections)
        {
            if (!string.IsNullOrEmpty(s.Title))
            {
                builder._sections[s.Title] = s;
            }
        }
        return builder;
    }
}
