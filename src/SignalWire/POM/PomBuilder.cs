// PomBuilder.cs
//
// Fluent builder wrapping PromptObjectModel. Mirrors Python's
// signalwire.core.pom_builder.PomBuilder — section auto-vivification on
// AddToSection / AddSubsection, fluent chaining, render+serialize
// passthroughs to the underlying PromptObjectModel.

using System.Collections.Generic;
using System.Text.Json;

namespace SignalWire.POM;

/// <summary>
/// Fluent, title-addressed front end over <see cref="PromptObjectModel"/>.
/// Where the model requires you to hold a <see cref="Section"/> reference to
/// extend it, this builder keeps a title-to-section index so content can be
/// added by name in any order.
///
/// <para><b>Auto-vivification is the point:</b>
/// <see cref="AddToSection"/> and <see cref="AddSubsection"/> create the
/// named (parent) section when it does not yet exist instead of throwing,
/// so a prompt can be assembled by several independent pieces of code
/// without any of them owning the ordering. Sections appear in the rendered
/// output in creation order.</para>
///
/// <para><b>Body appends, it does not replace.</b> Successive
/// <see cref="AddToSection"/> calls that pass a body join the fragments
/// with a blank line — unlike <see cref="Section.AddBody"/>, which
/// overwrites. Bullets always append.</para>
///
/// <para><b>Duplicate titles collapse.</b> The index is keyed by title, so
/// adding a second section with an existing title leaves both in the
/// underlying model's section list but points <see cref="HasSection"/> /
/// <see cref="GetSection"/> — and therefore all subsequent
/// <see cref="AddToSection"/> calls — at the most recently added one.
/// Untitled sections are not indexed at all and can only be reached
/// through <see cref="Pom"/>.</para>
///
/// <para>Rendering and serialization pass straight through to
/// <see cref="Pom"/>, so the exact output is defined by the underlying model
/// (see <see cref="PromptObjectModel"/>). <see cref="FromSections"/>
/// rebuilds a builder — index included — from previously serialized
/// section dicts.</para>
///
/// <para></para>
/// </summary>
public class PomBuilder
{
    public PromptObjectModel Pom { get; private set; }
    private readonly Dictionary<string, Section> _sections;

    public PomBuilder()
    {
        Pom = new PromptObjectModel();
        _sections = new Dictionary<string, Section>();
    }

    /// <summary>Add a new section.</summary>
    public PomBuilder AddSection(
        string title,
        string body = "",
        IReadOnlyList<string>? bullets = null,
        bool numbered = false,
        bool numberedBullets = false,
        IReadOnlyList<Dictionary<string, object>>? subsections = null)
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
    /// missing).</summary>
    public PomBuilder AddToSection(
        string title,
        string? body = null,
        string? bullet = null,
        IReadOnlyList<string>? bullets = null)
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
            section.BulletsMutable.Add(bullet);
        }
        if (bullets is not null)
        {
            section.BulletsMutable.AddRange(bullets);
        }
        return this;
    }

    /// <summary>Add a subsection under an existing section
    /// (auto-vivifies parent if missing).
    /// </summary>
    public PomBuilder AddSubsection(
        string parentTitle,
        string title,
        string body = "",
        IReadOnlyList<string>? bullets = null)
    {
        if (!_sections.ContainsKey(parentTitle))
        {
            AddSection(parentTitle);
        }
        _sections[parentTitle].AddSubsection(title, body, bullets);
        return this;
    }

    /// <summary>Check if a section with the given title exists.</summary>
    public bool HasSection(string title) => _sections.ContainsKey(title);

    /// <summary>Get a section by title, or null if absent.</summary>
    public Section? GetSection(string title) =>
        _sections.TryGetValue(title, out var s) ? s : null;

    /// <summary>Render the POM as markdown.</summary>
    public string RenderMarkdown() => Pom.RenderMarkdown();

    /// <summary>Render the POM as XML.</summary>
    public string RenderXml() => Pom.RenderXml();

    /// <summary>Serialize the POM to a list of section dicts.</summary>
    public IReadOnlyList<Dictionary<string, object>> ToDict() => Pom.ToDict();

    /// <summary>Serialize the POM to a JSON string.</summary>
    public string ToJson() => Pom.ToJson();

    /// <summary>Build a PomBuilder from a list of section dicts.</summary>
    public static PomBuilder FromSections(IReadOnlyList<Dictionary<string, object>> sections)
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
