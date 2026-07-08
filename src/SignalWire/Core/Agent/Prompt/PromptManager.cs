// Copyright (c) 2025 SignalWire
//
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Prompt management functionality for AgentBase.

using System.Diagnostics.CodeAnalysis;
using SignalWire.Contexts;
using SignalWire.POM;

namespace SignalWire.Core.Agent.Prompt;

/// <summary>
/// Manages prompt building and configuration for an agent.
/// </summary>
/// <remarks>
/// Mirrors the Python reference <c>signalwire.core.agent.prompt.manager.PromptManager</c>
/// and the Ruby <c>SignalWire::Core::Agent::Prompt::PromptManager</c>. It manages a
/// POM-backed prompt (via <see cref="PromptObjectModel"/>), an optional raw prompt
/// text, a post-prompt, and a contexts configuration (via <see cref="ContextBuilder"/>).
/// <para>
/// The prompt has two mutually exclusive modes: raw text (<see cref="SetPromptText"/>)
/// OR POM sections (the <c>PromptAdd*</c> methods). Mixing the two throws. Contexts,
/// when defined, take precedence over both in <see cref="GetPrompt"/>.
/// </para>
/// </remarks>
public class PromptManager
{
    private string? _promptText;
    private string? _postPromptText;
    private Dictionary<string, object>? _contexts;

    /// <summary>The backing Prompt Object Model. (equivalent to Python's <c>manager.pom</c>.)</summary>
    internal PromptObjectModel Pom { get; private set; }

    /// <summary>
    /// Create a prompt manager. (equivalent to Python's <c>__init__(agent=None)</c>.)
    /// </summary>
    /// <param name="agent">Optional parent AgentBase instance, kept as a
    /// back-reference for consistency with the Python/Ruby managers; may be null for
    /// standalone use.</param>
    public PromptManager(object? agent = null)
    {
        // agent is kept as a constructor arg for parity with the Python/Ruby
        // managers (they store it as a back-reference); C# has no use for it in
        // this standalone manager, so it is intentionally discarded rather than
        // exposed as public surface.
        _ = agent;
        Pom = new PromptObjectModel();
        _promptText = null;
        _postPromptText = null;
        _contexts = null;
    }

    /// <summary>
    /// Set the agent's prompt as raw text. (equivalent to Python's <c>set_prompt_text</c>.)
    /// </summary>
    /// <exception cref="InvalidOperationException">If POM sections are already in use.</exception>
    public PromptManager SetPromptText(string text)
    {
        ValidatePromptModeExclusivity();
        _promptText = text;
        return this;
    }

    /// <summary>Set the post-prompt text. (equivalent to Python's <c>set_post_prompt</c>.)</summary>
    public PromptManager SetPostPrompt(string text)
    {
        _postPromptText = text;
        return this;
    }

    /// <summary>
    /// Set the prompt from a POM array (list of section dictionaries).
    /// (equivalent to Python's <c>set_prompt_pom</c>.)
    /// </summary>
    public PromptManager SetPromptPom(IReadOnlyList<Dictionary<string, object>> pom)
    {
        ArgumentNullException.ThrowIfNull(pom);
        _promptText = null;
        Pom = PomBuilder.FromSections(pom).Pom;
        return this;
    }

    /// <summary>
    /// Add a section to the prompt. (equivalent to Python's <c>prompt_add_section</c>.)
    /// </summary>
    /// <exception cref="InvalidOperationException">If raw prompt text is already in use.</exception>
    public PromptManager PromptAddSection(
        string title,
        string body = "",
        IReadOnlyList<string>? bullets = null,
        bool numbered = false,
        bool numberedBullets = false,
        IReadOnlyList<Dictionary<string, object>>? subsections = null)
    {
        ValidatePromptModeExclusivity();
        var section = Pom.AddSection(
            title, body: body, bullets: bullets ?? [],
            numbered: numbered ? true : null, numberedBullets: numberedBullets);
        AddSubsections(section, subsections);
        return this;
    }

    /// <summary>
    /// Add content to an existing section (creating it if needed).
    /// (equivalent to Python's <c>prompt_add_to_section</c>.)
    /// </summary>
    public PromptManager PromptAddToSection(
        string title,
        string? body = null,
        string? bullet = null,
        IReadOnlyList<string>? bullets = null)
    {
        var section = Pom.FindSection(title) ?? Pom.AddSection(title, body: "");
        AppendBody(section, body);
        AppendBullets(section, bullet, bullets);
        return this;
    }

    /// <summary>
    /// Add a subsection to an existing section (creating the parent if needed).
    /// (equivalent to Python's <c>prompt_add_subsection</c>.)
    /// </summary>
    public PromptManager PromptAddSubsection(
        string parentTitle,
        string title,
        string body = "",
        IReadOnlyList<string>? bullets = null)
    {
        var parent = Pom.FindSection(parentTitle) ?? Pom.AddSection(parentTitle, body: "");
        parent.AddSubsection(title, body: body, bullets: bullets ?? []);
        return this;
    }

    /// <summary>
    /// Check whether a section exists in the prompt.
    /// (equivalent to Python's <c>prompt_has_section</c>.)
    /// </summary>
    public bool PromptHasSection(string title)
    {
        return Pom.FindSection(title) is not null;
    }

    /// <summary>
    /// Define contexts for the agent. Accepts a <see cref="ContextBuilder"/>
    /// (materialised via <c>ToDict</c>) or a raw dictionary.
    /// (equivalent to Python's <c>define_contexts</c>.)
    /// </summary>
    /// <exception cref="ArgumentException">If not a ContextBuilder or dictionary.</exception>
    public PromptManager DefineContexts(object contexts)
    {
        _contexts = contexts switch
        {
            ContextBuilder cb => cb.ToDict(),
            Dictionary<string, object> dict => dict,
            _ => throw new ArgumentException(
                "contexts must be a Dictionary or a ContextBuilder object", nameof(contexts)),
        };
        return this;
    }

    /// <summary>
    /// Get the prompt configuration. Contexts take precedence (return null — they
    /// render their own sections); otherwise raw text if set, else the POM section
    /// array, else null. (equivalent to Python's <c>get_prompt</c>.)
    /// </summary>
    /// <returns>A string, a list of section dictionaries, or null.</returns>
    public object? GetPrompt()
    {
        if (_contexts is not null)
        {
            return null;
        }
        if (_promptText is not null)
        {
            return _promptText;
        }
        var sections = Pom.ToDict();
        return sections.Count == 0 ? null : sections;
    }

    /// <summary>Get the raw prompt text if set. (equivalent to Python's <c>get_raw_prompt</c>.)</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface.")]
    public string? GetRawPrompt() => _promptText;

    /// <summary>Get the post-prompt text. (equivalent to Python's <c>get_post_prompt</c>.)</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface.")]
    public string? GetPostPrompt() => _postPromptText;

    /// <summary>Get the contexts configuration. (equivalent to Python's <c>get_contexts</c>.)</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface.")]
    public Dictionary<string, object>? GetContexts() => _contexts;

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    /// <summary>Throw if both prompt modes (raw text + POM sections) are active.</summary>
    private void ValidatePromptModeExclusivity()
    {
        if (_promptText is not null && Pom.ToDict().Count > 0)
        {
            throw new InvalidOperationException(
                "Cannot use both prompt_text and POM sections. "
                + "Please use either SetPromptText() OR the PromptAdd* methods, not both.");
        }
    }

    private static void AddSubsections(
        Section section, IReadOnlyList<Dictionary<string, object>>? subsections)
    {
        if (subsections is null)
        {
            return;
        }
        foreach (var sub in subsections)
        {
            if (!sub.TryGetValue("title", out var titleObj) || titleObj is not string title)
            {
                continue;
            }
            var body = sub.TryGetValue("body", out var b) && b is string bs ? bs : "";
            var bullets = sub.TryGetValue("bullets", out var bl) && bl is IReadOnlyList<string> bls
                ? bls
                : (IReadOnlyList<string>)[];
            section.AddSubsection(title, body: body, bullets: bullets);
        }
    }

    private static void AppendBody(Section section, string? body)
    {
        if (body is null)
        {
            return;
        }
        section.Body = string.IsNullOrEmpty(section.Body) ? body : $"{section.Body}\n\n{body}";
    }

    private static void AppendBullets(Section section, string? bullet, IReadOnlyList<string>? bullets)
    {
        var toAdd = new List<string>();
        if (bullet is not null)
        {
            toAdd.Add(bullet);
        }
        if (bullets is not null)
        {
            toAdd.AddRange(bullets);
        }
        if (toAdd.Count > 0)
        {
            section.AddBullets(toAdd);
        }
    }
}
