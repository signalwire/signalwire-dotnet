using System.Diagnostics.CodeAnalysis;

namespace SignalWire.Contexts;

/// <summary>
/// Reserved tool names auto-injected by the runtime when contexts/steps are
/// present. User-defined SWAIG tools must not collide with these names.
///
/// <list type="bullet">
/// <item><c>next_step</c> / <c>change_context</c> are injected when
/// valid_steps or valid_contexts is set so the model can navigate the flow.</item>
/// <item><c>gather_submit</c> is injected while a step's gather_info is
/// collecting answers.</item>
/// </list>
///
/// <see cref="ContextBuilder.Validate"/> rejects any agent that registers a
/// user tool sharing one of these names — the runtime would never call the
/// user tool because the native one wins.
/// </summary>
public static class ReservedToolNames
{
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>
    {
        "next_step",
        "change_context",
        "gather_submit",
    };
}

// -- History visibility modes --

/// <summary>
/// Valid values for a step's or context's <c>history</c> visibility mode,
/// controlling what the model still sees when a step is entered.
///
/// <list type="bullet">
/// <item><c>keep</c> — nothing is cleared: every prior step's instructions
/// and dialogue stay in the model's context.</item>
/// <item><c>default</c> — prior step instructions are hidden; the dialogue
/// is kept. This is the behavior when unset.</item>
/// <item><c>hide</c> — prior instructions hidden AND the prior dialogue
/// pulled out of the model's context. The only way back in is an explicit
/// <c>${step_history.*}</c> reference in the new prompt.</item>
/// </list>
/// </summary>
internal static class HistoryModes
{
    public static readonly IReadOnlyList<string> Values = ["keep", "default", "hide"];

    public static string Validate(string mode)
    {
        if (!Values.Contains(mode))
            throw new ArgumentException(
                $"history must be one of [keep, default, hide], got '{mode}'", nameof(mode));
        return mode;
    }
}

// -- GatherQuestion --

/// <summary>
/// One question in a step's <c>gather_info</c> block — the unit the runtime
/// asks the caller, then records under <see cref="Key"/>.
///
/// <para>Constructed from an options dictionary rather than positional
/// parameters, matching the reference's keyword-argument shape.
/// <c>key</c> and <c>question</c> are required and throw if absent;
/// <c>type</c>, <c>confirm</c>, <c>prompt</c>, <c>functions</c>, and
/// <c>isolated</c> are optional.</para>
///
/// <para><b>Wire shape:</b> <see cref="ToDict"/> emits only the keys that
/// carry information — <c>type</c> is omitted when it is the default
/// <c>"string"</c>, <c>confirm</c> only when true, empty
/// <c>functions</c> is dropped. <c>isolated</c> is the exception: it is
/// emitted whenever set, <b>including when false</b>, because false is
/// the only way a question can opt out of an isolated gather's
/// default.</para>
///
/// <para>Instances are immutable once constructed.</para>
/// </summary>
public class GatherQuestion
{
    private readonly string _key;
    private readonly string _question;
    private readonly string _type;
    private readonly bool _confirm;
    private readonly string? _prompt;
    private readonly List<string>? _functions;
    // Tri-state: null means "inherit the gather_info default"
    private readonly bool? _isolated;

    public GatherQuestion(Dictionary<string, object> opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        _key = (string)opts["key"];
        _question = (string)opts["question"];
        _type = opts.TryGetValue("type", out var t) ? (string)t : "string";
        _confirm = opts.TryGetValue("confirm", out var c) && c is true;
        _prompt = opts.TryGetValue("prompt", out var p) ? (string)p : null;
        _functions = opts.TryGetValue("functions", out var f) ? (List<string>)f : null;
        _isolated = opts.TryGetValue("isolated", out var iso) ? (bool?)iso : null;
    }

    public string Key => _key;

    /// <summary>The question text put to the caller.
    /// (equivalent to Python's <c>question</c>.)</summary>
    public string Question => _question;

    /// <summary>The answer's expected type, defaulting to <c>"string"</c>.
    /// (equivalent to Python's <c>type</c>.)</summary>
    public string Type => _type;

    /// <summary>Whether the answer must be confirmed back to the caller.
    /// (equivalent to Python's <c>confirm</c>.)</summary>
    public bool Confirm => _confirm;

    /// <summary>An optional prompt override for this question.
    /// (equivalent to Python's <c>prompt</c>.)</summary>
    public string? Prompt => _prompt;

    /// <summary>The SWAIG functions reachable while answering this question.
    /// (equivalent to Python's <c>functions</c>.)</summary>
    public IReadOnlyList<string>? Functions => _functions;

    /// <summary>Tri-state isolation: null inherits the gather_info default.
    /// (equivalent to Python's <c>isolated</c>.)</summary>
    public bool? Isolated => _isolated;

    public Dictionary<string, object> ToDict()
    {
        var map = new Dictionary<string, object>
        {
            ["key"] = _key,
            ["question"] = _question,
        };
        if (_type != "string") map["type"] = _type;
        if (_confirm) map["confirm"] = true;
        if (_prompt is not null) map["prompt"] = _prompt;
        if (_functions is { Count: > 0 }) map["functions"] = _functions;
        // Emitted even when False, so it can override an isolated gather
        if (_isolated is not null) map["isolated"] = _isolated;
        return map;
    }
}

// -- GatherInfo --

/// <summary>
/// A step's <c>gather_info</c> block — an ordered list of
/// <see cref="GatherQuestion"/>s the runtime works through one at a time,
/// plus the settings that govern the gather as a whole.
///
/// <para><c>outputKey</c> names where the collected answers are stored,
/// <c>completionAction</c> is what the runtime does once every question is
/// answered, and <c>prompt</c> overrides the instructions injected while
/// gathering. <c>isolated</c> is the <b>default</b> for the questions in
/// this gather — when true a question is asked with its sibling Q&amp;A
/// hidden from the model, forcing it to ask rather than infer; a
/// question's own <c>isolated</c> overrides it.</para>
///
/// <para>While a gather is active the runtime deactivates the step's other
/// functions — see <see cref="Step.AddGatherQuestion"/> for the full
/// tool-access consequences.</para>
///
/// <para><b>Wire shape:</b> <see cref="ToDict"/> always emits
/// <c>questions</c> (possibly empty) and omits every optional setting that
/// is unset; <c>isolated</c> is emitted only when true.</para>
/// </summary>
public class GatherInfo
{
    private readonly List<GatherQuestion> _questions = [];
    private readonly string? _outputKey;
    private readonly string? _completionAction;
    private readonly string? _prompt;
    private readonly bool _isolated;

    public GatherInfo(string? outputKey = null, string? completionAction = null, string? prompt = null, bool isolated = false)
    {
        _outputKey = outputKey;
        _completionAction = completionAction;
        _prompt = prompt;
        _isolated = isolated;
    }

    public GatherInfo AddQuestion(Dictionary<string, object> opts)
    {
        _questions.Add(new GatherQuestion(opts));
        return this;
    }

    public IReadOnlyList<GatherQuestion> Questions => _questions;
    public string? CompletionAction => _completionAction;

    public Dictionary<string, object> ToDict()
    {
        var map = new Dictionary<string, object>
        {
            ["questions"] = _questions.Select(q => q.ToDict()).ToList(),
        };
        if (_prompt is not null) map["prompt"] = _prompt;
        if (_outputKey is not null) map["output_key"] = _outputKey;
        if (_completionAction is not null) map["completion_action"] = _completionAction;
        if (_isolated) map["isolated"] = true;
        return map;
    }
}

// -- Step --

/// <summary>
/// One step of a <see cref="Context"/>'s flow: the instructions injected
/// while the step is active, the criteria for moving on, which tools and
/// destinations are reachable from it, and any information to gather.
///
/// <para>Every setter returns <c>this</c> for fluent chaining. Steps are
/// created through <see cref="Context.AddStep"/>, which owns their ordering.</para>
///
/// <para><b>Text is exclusive-or POM sections.</b> A step's instructions
/// come either from <see cref="SetText"/> or from
/// <see cref="AddSection"/>/<see cref="AddBullets"/>, never both — mixing
/// them throws <see cref="InvalidOperationException"/>. Use
/// <see cref="ClearSections"/> to drop both and start over. A step with
/// neither throws when it is serialized, not when it is built.</para>
///
/// <para><b>Two behaviours that surprise people</b> are documented on the
/// members themselves: <see cref="SetFunctions(IReadOnlyList{string})"/> —
/// an unset function list is <i>inherited</i> from the previous step rather
/// than reset — and <see cref="SetEnd"/>, which exits step mode without
/// ending the call.</para>
///
/// <para><b>Wire shape:</b> <see cref="ToDict"/> always emits <c>name</c>
/// and the rendered <c>text</c>; boolean flags appear only when true; the
/// four <c>reset*</c> settings are folded into a single nested
/// <c>reset</c> object that is omitted entirely when empty.</para>
/// </summary>
public class Step
{
    private readonly string _name;
    private string? _text;
    private string? _stepCriteria;
    private object? _functions;
    private IReadOnlyList<string>? _validSteps;
    private IReadOnlyList<string>? _validContexts;
    private List<Dictionary<string, object>> _sections = [];
    private GatherInfo? _gatherInfo;
    private bool _end;
    private bool _skipUserTurn;
    private bool _skipToNextStep;
    private string? _resetSystemPrompt;
    private string? _resetUserPrompt;
    private bool _resetConsolidate;
    private bool _resetFullReset;
    // Visibility of everything that came before this step
    private string? _history;

    public Step(string name) { _name = name; }

    public string Name => _name;

    public Step SetText(string text)
    {
        if (_sections.Count > 0)
            throw new InvalidOperationException("Cannot use SetText() when POM sections have been added.");
        _text = text;
        return this;
    }

    public Step AddSection(string title, string body)
    {
        if (_text is not null)
            throw new InvalidOperationException("Cannot add POM sections when SetText() has been used.");
        _sections.Add(new Dictionary<string, object> { ["title"] = title, ["body"] = body });
        return this;
    }

    public Step AddBullets(string title, IReadOnlyList<string> bullets)
    {
        ArgumentNullException.ThrowIfNull(bullets);
        if (_text is not null)
            throw new InvalidOperationException("Cannot add POM sections when SetText() has been used.");
        _sections.Add(new Dictionary<string, object> { ["title"] = title, ["bullets"] = new List<string>(bullets) });
        return this;
    }

    public Step ClearSections()
    {
        _sections = [];
        _text = null;
        return this;
    }

    public Step SetStepCriteria(string criteria) { _stepCriteria = criteria; return this; }

    /// <summary>
    /// Set which non-internal functions are callable while this step is
    /// active.
    ///
    /// <para><b>IMPORTANT — inheritance behavior:</b> If you do NOT call
    /// this method, the step inherits whichever function set was active on
    /// the previous step (or the previous context's last step). The
    /// server-side runtime only resets the active set when a step
    /// explicitly declares its <c>functions</c> field. This is the most
    /// common source of bugs in multi-step agents: forgetting
    /// <see cref="SetFunctions(IReadOnlyList{string})"/> on a later step lets the previous step's
    /// tools leak through. Best practice is to call
    /// <see cref="SetFunctions(IReadOnlyList{string})"/> explicitly on every step that should
    /// differ from the previous one.</para>
    ///
    /// <para>Keep the per-step active set small: LLM tool selection
    /// accuracy degrades noticeably past ~7-8 simultaneously-active tools
    /// per call. Use per-step whitelisting to partition large tool
    /// collections.</para>
    ///
    /// <para>Internal functions (e.g. <c>gather_submit</c>, hangup hook)
    /// are ALWAYS protected and cannot be deactivated by this whitelist.
    /// The native navigation tools <c>next_step</c> and
    /// <c>change_context</c> are injected automatically when
    /// <see cref="SetValidSteps"/> / <see cref="SetValidContexts"/> is
    /// used; they are not affected by this list.</para>
    ///
    /// </summary>
    /// <param name="functions">One of:
    /// <list type="bullet">
    /// <item><c>List&lt;string&gt;</c> — whitelist of function names
    /// allowed in this step.</item>
    /// <item>An empty list — explicit disable-all (no user functions
    /// callable).</item>
    /// <item>The string <c>"none"</c> — synonym for the empty list.</item>
    /// </list>
    /// </param>
    public Step SetFunctions(IReadOnlyList<string> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        _functions = functions;
        return this;
    }

    /// <inheritdoc cref="SetFunctions(IReadOnlyList{string})"/>
    /// <remarks>The string arm of the reference's <c>str | list[str]</c>; the only
    /// meaningful value is <c>"none"</c>, a synonym for the empty list.</remarks>
    public Step SetFunctions(string functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        _functions = functions;
        return this;
    }

    public Step SetValidSteps(IReadOnlyList<string> steps) { _validSteps = steps; return this; }
    public Step SetValidContexts(IReadOnlyList<string> contexts) { _validContexts = contexts; return this; }

    /// <summary>
    /// Mark this step as terminal for the step flow.
    ///
    /// <para><b>IMPORTANT:</b> <paramref name="end"/> = true does NOT end
    /// the conversation or hang up the call. It exits step mode entirely
    /// after this step executes — clearing the steps list, current step
    /// index, valid_steps, and valid_contexts. The agent keeps running,
    /// but operates only under the base system prompt and the
    /// context-level prompt; no more step instructions are injected and
    /// no more <c>next_step</c> tool is offered.</para>
    ///
    /// <para>To actually end the call, call a hangup tool or define a
    /// hangup hook.</para>
    /// </summary>
    public Step SetEnd(bool end) { _end = end; return this; }

    public Step SetSkipUserTurn(bool skip) { _skipUserTurn = skip; return this; }
    public Step SetSkipToNextStep(bool skip) { _skipToNextStep = skip; return this; }

    /// <summary>
    /// Control what the model still sees when this step is entered.
    ///
    /// <para>The mode applies at the moment this step is entered and governs
    /// everything that came before it — including the turn that triggered the
    /// transition. It does not affect this step's own turns, which accumulate
    /// fresh. Nothing is deleted: the call log keeps every message.</para>
    ///
    /// <list type="bullet">
    /// <item><c>keep</c> — clear nothing. Every prior step's instructions and
    /// dialogue stay visible to the model.</item>
    /// <item><c>default</c> — hide the prior step instructions, keep the
    /// user/assistant dialogue. This is the default when unset.</item>
    /// <item><c>hide</c> — hide the prior instructions AND pull the prior
    /// dialogue out of the model's context. Pair it with a
    /// <c>${step_history.*}</c> reference in this step's text to choose
    /// exactly what comes back.</item>
    /// </list>
    /// </summary>
    /// <param name="history">One of "keep", "default", or "hide".</param>
    /// <exception cref="ArgumentException">if history is not one of the three modes.</exception>
    public Step SetHistory(string history) { _history = HistoryModes.Validate(history); return this; }

    /// <summary>
    /// Enable info gathering for this step. Questions are presented one at a
    /// time via dynamic step instruction re-injection.
    ///
    /// <para>Recognized <paramref name="opts"/> keys: <c>output_key</c>,
    /// <c>completion_action</c>, <c>prompt</c>, and <c>isolated</c>.
    /// <c>isolated</c> (bool, default false) becomes the default for every
    /// question in this gather: when true a question is asked with the
    /// sibling Q&amp;A hidden from the model, so it must ask rather than
    /// derive the answer. A question's own <c>isolated</c> overrides it.</para>
    /// </summary>
    public Step SetGatherInfo(Dictionary<string, object> opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        _gatherInfo = new GatherInfo(
            opts.TryGetValue("output_key", out var ok) ? (string)ok : null,
            opts.TryGetValue("completion_action", out var ca) ? (string)ca : null,
            opts.TryGetValue("prompt", out var p) ? (string)p : null,
            opts.TryGetValue("isolated", out var iso) && iso is true);
        return this;
    }

    /// <summary>
    /// Add a question to this step's gather_info. Initializes gather_info
    /// if not yet set.
    ///
    /// <para><b>IMPORTANT — gather mode locks function access:</b> While
    /// the model is asking gather questions, the runtime forcibly
    /// deactivates ALL of the step's other functions. The only callable
    /// tools during a gather question are:</para>
    ///
    /// <list type="bullet">
    /// <item><c>gather_submit</c> (the native answer-submission tool)</item>
    /// <item>Whatever names you pass in this question's <c>"functions"</c>
    /// option</item>
    /// </list>
    ///
    /// <para><c>next_step</c> and <c>change_context</c> are also filtered
    /// out — the model cannot navigate away until the gather completes.
    /// This is by design: it forces a tight ask → submit → next-question
    /// loop.</para>
    ///
    /// <para>If a question needs to call out to a tool (e.g. validate an
    /// email, geocode a ZIP), list that tool name in this question's
    /// <c>"functions"</c> option. Functions listed here are active ONLY
    /// for this question.</para>
    /// </summary>
    public Step AddGatherQuestion(Dictionary<string, object> opts)
    {
        _gatherInfo ??= new GatherInfo();
        _gatherInfo.AddQuestion(opts);
        return this;
    }

    public Step SetResetSystemPrompt(string systemPrompt) { _resetSystemPrompt = systemPrompt; return this; }
    public Step SetResetUserPrompt(string userPrompt) { _resetUserPrompt = userPrompt; return this; }
    public Step SetResetConsolidate(bool consolidate) { _resetConsolidate = consolidate; return this; }
    public Step SetResetFullReset(bool fullReset) { _resetFullReset = fullReset; return this; }

    public IReadOnlyList<string>? ValidSteps => _validSteps;
    public IReadOnlyList<string>? ValidContexts => _validContexts;
    public GatherInfo? GatherInfoData => _gatherInfo;

    /// <summary>The raw SetFunctions value (a whitelist list, an empty list, or
    /// the string "none"), or null if unset. Internal — read by
    /// <see cref="ContextBuilder.Validate"/> for the dangling-reference check;
    /// adds no public-surface drift.</summary>
    internal object? Functions => _functions;

    private string RenderText()
    {
        if (_text is not null) return _text;
        if (_sections.Count == 0)
            throw new InvalidOperationException($"Step '{_name}' has no text or POM sections defined");

        var parts = new List<string>();
        foreach (var section in _sections)
        {
            var title = (string)section["title"];
            var lines = $"## {title}\n";
            if (section.TryGetValue("bullets", out var b) && b is List<string> bullets)
            {
                foreach (var bullet in bullets) lines += $"- {bullet}\n";
            }
            else
            {
                lines += (string)section["body"] + "\n";
            }
            parts.Add(lines);
        }
        return string.Join("\n", parts).TrimEnd();
    }

    public Dictionary<string, object> ToDict()
    {
        var map = new Dictionary<string, object>
        {
            ["name"] = _name,
            ["text"] = RenderText(),
        };
        if (_stepCriteria is not null) map["step_criteria"] = _stepCriteria;
        if (_functions is not null) map["functions"] = _functions;
        if (_validSteps is not null) map["valid_steps"] = _validSteps;
        if (_validContexts is not null) map["valid_contexts"] = _validContexts;
        if (_end) map["end"] = true;
        if (_skipUserTurn) map["skip_user_turn"] = true;
        if (_skipToNextStep) map["skip_to_next_step"] = true;
        if (_history is not null) map["history"] = _history;

        var resetObj = new Dictionary<string, object>();
        if (_resetSystemPrompt is not null) resetObj["system_prompt"] = _resetSystemPrompt;
        if (_resetUserPrompt is not null) resetObj["user_prompt"] = _resetUserPrompt;
        if (_resetConsolidate) resetObj["consolidate"] = true;
        if (_resetFullReset) resetObj["full_reset"] = true;
        if (resetObj.Count > 0) map["reset"] = resetObj;

        if (_gatherInfo is not null) map["gather_info"] = _gatherInfo.ToDict();
        return map;
    }
}

// -- Context --

/// <summary>
/// A named phase of an agent workflow: an ordered set of
/// <see cref="Step"/>s plus the prompts, reset behaviour, and navigation
/// rules that apply while the context is active. Contexts are created
/// through <see cref="ContextBuilder"/>; only one is active at a time.
///
/// <para>Every setter returns <c>this</c> for fluent chaining.</para>
///
/// <para><b>Step management.</b> <see cref="AddStep"/> appends a step and
/// returns it for further configuration; it throws on a duplicate name and
/// enforces a cap of 100 steps per context. <see cref="RemoveStep"/> is a
/// no-op for an unknown name, whereas <see cref="MoveStep"/> throws.
/// Ordering is tracked separately from the step map, so
/// <see cref="GetStepOrder"/> — not <see cref="GetSteps"/> — is what
/// determines execution order and serialization order.</para>
///
/// <para><b>Text is exclusive-or POM sections</b>, independently for each
/// of the two prompts: <see cref="SetPrompt"/> conflicts with
/// <see cref="AddSection"/>/<see cref="AddBullets"/>, and
/// <see cref="SetSystemPrompt"/> conflicts with
/// <see cref="AddSystemSection"/>/<see cref="AddSystemBullets"/>. Mixing
/// either pair throws <see cref="InvalidOperationException"/>. When
/// sections are present they are rendered to a markdown string, so both
/// forms reach the wire as a plain string under the same key.</para>
///
/// <para><b>Context-level defaults.</b> <see cref="SetHistory"/> sets the
/// default visibility mode for this context's steps, which a step's own
/// <see cref="Step.SetHistory"/> overrides. <see cref="SetIsolated"/>
/// wipes conversation history on entry — but is superseded by a reset
/// configuration; see that member for the exception.</para>
///
/// <para><b>Wire shape:</b> <see cref="ToDict"/> always emits
/// <c>steps</c> (in <see cref="GetStepOrder"/> order); every other key is
/// omitted when unset, and the booleans appear only when true.</para>
/// </summary>
public class Context
{
    private const int MaxStepsPerContext = 100;
    private readonly string _name;
    private readonly Dictionary<string, Step> _steps = [];
    private readonly List<string> _stepOrder = [];
    private IReadOnlyList<string>? _validContexts;
    private IReadOnlyList<string>? _validSteps;
    private string? _initialStep;
    private string? _postPrompt;
    private string? _systemPrompt;
    private bool _consolidate;
    private bool _fullReset;
    private string? _userPrompt;
    private bool _isolated;
    private string? _promptText;
    private List<Dictionary<string, object>> _promptSections = [];
    private List<Dictionary<string, object>> _systemPromptSections = [];
    private Dictionary<string, List<string>>? _enterFillers;
    private Dictionary<string, List<string>>? _exitFillers;
    // Default visibility mode for the steps in this context
    private string? _history;

    public Context(string name) { _name = name; }
    public string Name => _name;

    // -- Steps --

    public Step AddStep(string name, Dictionary<string, object>? opts = null)
    {
        if (_steps.ContainsKey(name))
            throw new InvalidOperationException($"Step '{name}' already exists in context '{_name}'");
        if (_steps.Count >= MaxStepsPerContext)
            throw new InvalidOperationException($"Maximum steps per context ({MaxStepsPerContext}) exceeded");

        var step = new Step(name);
        _steps[name] = step;
        _stepOrder.Add(name);

        if (opts is not null)
        {
            if (opts.TryGetValue("text", out var t)) step.SetText((string)t);
            if (opts.TryGetValue("step_criteria", out var sc)) step.SetStepCriteria((string)sc);
            if (opts.TryGetValue("functions", out var f))
            {
                // The reference's `str | list[str]` union: dispatch to the matching
                // overload rather than erasing both arms back to `object`.
                if (f is string fs) step.SetFunctions(fs);
                else step.SetFunctions((IReadOnlyList<string>)f);
            }
            if (opts.TryGetValue("valid_steps", out var vs)) step.SetValidSteps((List<string>)vs);
            if (opts.TryGetValue("valid_contexts", out var vc)) step.SetValidContexts((List<string>)vc);
        }
        return step;
    }

    public Step? GetStep(string name) => _steps.TryGetValue(name, out var s) ? s : null;

    public Context RemoveStep(string name)
    {
        if (_steps.Remove(name))
        {
            _stepOrder.Remove(name);
        }
        return this;
    }

    public Context MoveStep(string name, int position)
    {
        if (!_steps.ContainsKey(name))
            throw new InvalidOperationException($"Step '{name}' not found in context '{_name}'");
        _stepOrder.Remove(name);
        _stepOrder.Insert(position, name);
        return this;
    }

    [SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "get_* accessor matches the cross-port surface")]
    public IReadOnlyDictionary<string, Step> GetSteps() => _steps;

    [SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "get_* accessor matches the cross-port surface")]
    public IReadOnlyList<string> GetStepOrder() => [.. _stepOrder];

    // -- Prompt (plain text vs POM) --

    public Context SetPrompt(string prompt)
    {
        if (_promptSections.Count > 0)
            throw new InvalidOperationException("Cannot use SetPrompt() when POM sections have been added.");
        _promptText = prompt;
        return this;
    }

    public Context AddSection(string title, string body)
    {
        if (_promptText is not null)
            throw new InvalidOperationException("Cannot add POM sections when SetPrompt() has been used.");
        _promptSections.Add(new Dictionary<string, object> { ["title"] = title, ["body"] = body });
        return this;
    }

    public Context AddBullets(string title, IReadOnlyList<string> bullets)
    {
        ArgumentNullException.ThrowIfNull(bullets);
        if (_promptText is not null)
            throw new InvalidOperationException("Cannot add POM sections when SetPrompt() has been used.");
        _promptSections.Add(new Dictionary<string, object> { ["title"] = title, ["bullets"] = new List<string>(bullets) });
        return this;
    }

    // -- System prompt (plain text vs POM) --

    public Context SetSystemPrompt(string systemPrompt)
    {
        if (_systemPromptSections.Count > 0)
            throw new InvalidOperationException("Cannot use SetSystemPrompt() when POM sections have been added.");
        _systemPrompt = systemPrompt;
        return this;
    }

    public Context AddSystemSection(string title, string body)
    {
        if (_systemPrompt is not null)
            throw new InvalidOperationException("Cannot add POM sections when SetSystemPrompt() has been used.");
        _systemPromptSections.Add(new Dictionary<string, object> { ["title"] = title, ["body"] = body });
        return this;
    }

    public Context AddSystemBullets(string title, IReadOnlyList<string> bullets)
    {
        ArgumentNullException.ThrowIfNull(bullets);
        if (_systemPrompt is not null)
            throw new InvalidOperationException("Cannot add POM sections when SetSystemPrompt() has been used.");
        _systemPromptSections.Add(new Dictionary<string, object> { ["title"] = title, ["bullets"] = new List<string>(bullets) });
        return this;
    }

    // -- Config setters --

    /// <summary>
    /// Set which step the context starts on when entered.
    ///
    /// <para>By default, a context starts on its first step (index 0). Use
    /// this to skip a preamble step on re-entry via
    /// <c>change_context</c>.</para>
    /// </summary>
    /// <param name="stepName">Name of the step to start on (must exist in this context).</param>
    public Context SetInitialStep(string stepName) { _initialStep = stepName; return this; }

    [SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "get_* accessor matches the cross-port surface")]
    public string? GetInitialStep() => _initialStep;

    public Context SetValidContexts(IReadOnlyList<string> contexts) { _validContexts = contexts; return this; }
    public Context SetValidSteps(IReadOnlyList<string> steps) { _validSteps = steps; return this; }
    public Context SetPostPrompt(string postPrompt) { _postPrompt = postPrompt; return this; }
    public Context SetConsolidate(bool consolidate) { _consolidate = consolidate; return this; }
    public Context SetFullReset(bool fullReset) { _fullReset = fullReset; return this; }
    public Context SetUserPrompt(string userPrompt) { _userPrompt = userPrompt; return this; }

    /// <summary>
    /// Set the default visibility mode for every step in this context.
    ///
    /// <para>A step's own <see cref="Step.SetHistory"/> overrides this. See
    /// <see cref="Step.SetHistory"/> for what each mode does.</para>
    /// </summary>
    /// <param name="history">One of "keep", "default", or "hide".</param>
    /// <exception cref="ArgumentException">if history is not one of the three modes.</exception>
    public Context SetHistory(string history) { _history = HistoryModes.Validate(history); return this; }

    /// <summary>
    /// Mark this context as isolated — entering it wipes conversation
    /// history.
    ///
    /// <para>When <paramref name="isolated"/> = true and the context is
    /// entered via change_context, the runtime wipes the conversation
    /// array. The model starts fresh with only the new context's
    /// system_prompt + step instructions, with no memory of prior
    /// turns.</para>
    ///
    /// <para><b>EXCEPTION — reset overrides the wipe:</b> If the context
    /// also has a reset configuration (via <see cref="SetConsolidate"/>
    /// or <see cref="SetFullReset"/>), the wipe is skipped in favor of
    /// the reset behavior. Use reset with consolidate=true to summarize
    /// prior history into a single message instead of dropping it
    /// entirely.</para>
    ///
    /// <para>Use cases: switching to a sensitive billing flow that
    /// should not see prior small-talk; handing off to a different agent
    /// persona; resetting after a long off-topic detour.</para>
    /// </summary>
    public Context SetIsolated(bool isolated) { _isolated = isolated; return this; }

    // -- Fillers --

    public Context SetEnterFillers(Dictionary<string, List<string>> fillers) { _enterFillers = fillers; return this; }
    public Context SetExitFillers(Dictionary<string, List<string>> fillers) { _exitFillers = fillers; return this; }

    public Context AddEnterFiller(string lang, params string[] fillers)
    {
        _enterFillers ??= [];
        if (!_enterFillers.ContainsKey(lang)) _enterFillers[lang] = [];
        _enterFillers[lang].AddRange(fillers);
        return this;
    }

    public Context AddExitFiller(string lang, params string[] fillers)
    {
        _exitFillers ??= [];
        if (!_exitFillers.ContainsKey(lang)) _exitFillers[lang] = [];
        _exitFillers[lang].AddRange(fillers);
        return this;
    }

    [SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "get_* accessor matches the cross-port surface")]
    public IReadOnlyList<string>? GetValidContexts() => _validContexts;

    // -- Rendering helpers --

    private static string RenderSections(List<Dictionary<string, object>> sections)
    {
        var parts = new List<string>();
        foreach (var section in sections)
        {
            var title = (string)section["title"];
            var lines = $"## {title}\n";
            if (section.TryGetValue("bullets", out var b) && b is List<string> bullets)
            {
                foreach (var bullet in bullets) lines += $"- {bullet}\n";
            }
            else
            {
                lines += (string)section["body"] + "\n";
            }
            parts.Add(lines);
        }
        return string.Join("\n", parts).TrimEnd();
    }

    public Dictionary<string, object> ToDict()
    {
        var map = new Dictionary<string, object>
        {
            ["steps"] = _stepOrder.Select(n => _steps[n].ToDict()).ToList(),
        };

        if (_validContexts is not null) map["valid_contexts"] = _validContexts;
        if (_validSteps is not null) map["valid_steps"] = _validSteps;
        if (_initialStep is not null) map["initial_step"] = _initialStep;
        if (_postPrompt is not null) map["post_prompt"] = _postPrompt;

        if (_systemPromptSections.Count > 0) map["system_prompt"] = RenderSections(_systemPromptSections);
        else if (_systemPrompt is not null) map["system_prompt"] = _systemPrompt;

        if (_consolidate) map["consolidate"] = true;
        if (_fullReset) map["full_reset"] = true;
        if (_userPrompt is not null) map["user_prompt"] = _userPrompt;
        if (_isolated) map["isolated"] = true;

        if (_promptSections.Count > 0) map["prompt"] = RenderSections(_promptSections);
        else if (_promptText is not null) map["prompt"] = _promptText;

        if (_enterFillers is not null) map["enter_fillers"] = _enterFillers;
        if (_exitFillers is not null) map["exit_fillers"] = _exitFillers;

        if (_history is not null) map["history"] = _history;

        return map;
    }
}

// -- ContextBuilder --

/// <summary>
/// Builder for multi-step, multi-context AI agent workflows.
///
/// <para>A ContextBuilder owns one or more <see cref="Context"/>s; each
/// context owns an ordered list of <see cref="Step"/>s. Only one context
/// and one step is active at a time. Per chat turn, the runtime injects
/// the current step's instructions as a system message, then asks the LLM
/// for a response.</para>
///
/// <para><b>Native tools auto-injected by the runtime:</b> When a step
/// (or its enclosing context) declares valid_steps or valid_contexts, the
/// runtime auto-injects two native tools so the model can navigate the
/// flow: <c>next_step</c> and <c>change_context</c>. A third native tool,
/// <c>gather_submit</c>, is injected during gather_info questioning.
/// These three names are reserved: <see cref="Validate"/> rejects any
/// agent that defines a SWAIG tool with one of them. See
/// <see cref="ReservedToolNames.Reserved"/>.</para>
///
/// <para><b>Function whitelisting (<see cref="Step.SetFunctions(IReadOnlyList{string})"/>):</b>
/// Each step may declare a functions whitelist. The whitelist is applied
/// in-memory at the start of each LLM turn. CRITICALLY: if a step does
/// NOT declare a functions field, it INHERITS the previous step's active
/// set. See <see cref="Step.SetFunctions(IReadOnlyList{string})"/> for details and examples.</para>
/// </summary>
public class ContextBuilder
{
    private const int MaxContexts = 50;
    private readonly Dictionary<string, Context> _contexts = [];
    private readonly List<string> _contextOrder = [];
    private Func<IEnumerable<string>>? _toolNameSupplier;

    /// <summary>
    /// Attach a supplier that returns registered SWAIG tool names so
    /// <see cref="Validate"/> can check them against
    /// <see cref="ReservedToolNames.Reserved"/>. Called internally by
    /// <c>AgentBase.DefineContexts()</c>.
    /// </summary>
    public ContextBuilder AttachToolNameSupplier(Func<IEnumerable<string>> supplier)
    {
        _toolNameSupplier = supplier;
        return this;
    }

    /// <summary>
    /// Remove all contexts, returning the builder to its initial state.
    /// Use this in a dynamic config callback when you need to rebuild
    /// contexts from scratch for a specific request.
    /// </summary>
    public ContextBuilder Reset()
    {
        _contexts.Clear();
        _contextOrder.Clear();
        return this;
    }

    public Context AddContext(string name)
    {
        if (_contexts.ContainsKey(name))
            throw new InvalidOperationException($"Context '{name}' already exists");
        if (_contexts.Count >= MaxContexts)
            throw new InvalidOperationException($"Maximum number of contexts ({MaxContexts}) exceeded");

        var context = new Context(name);
        _contexts[name] = context;
        _contextOrder.Add(name);
        return context;
    }

    public Context? GetContext(string name) => _contexts.TryGetValue(name, out var c) ? c : null;
    public bool HasContexts() => _contexts.Count > 0;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (_contexts.Count == 0) { errors.Add("At least one context must be defined"); return errors; }

        if (_contexts.Count == 1)
        {
            var contextName = _contexts.Keys.First();
            if (contextName != "default")
                errors.Add("When using a single context, it must be named 'default'");
        }

        foreach (var (contextName, context) in _contexts)
        {
            if (context.GetSteps().Count == 0)
                errors.Add($"Context '{contextName}' must have at least one step");
        }

        // Validate initial_step references a real step in the context
        foreach (var (contextName, context) in _contexts)
        {
            var initialStep = context.GetInitialStep();
            if (initialStep is not null && !context.GetSteps().ContainsKey(initialStep))
            {
                var available = context.GetSteps().Keys.OrderBy(k => k).ToList();
                var availableStr = "[" + string.Join(", ", available.Select(k => $"'{k}'")) + "]";
                errors.Add(
                    $"Context '{contextName}' has initial_step='{initialStep}' but that step does " +
                    $"not exist. Available steps: {availableStr}");
            }
        }

        foreach (var (contextName, context) in _contexts)
        {
            foreach (var (stepName, step) in context.GetSteps())
            {
                if (step.ValidSteps is not null)
                {
                    foreach (var vs in step.ValidSteps)
                    {
                        if (vs != "next" && !context.GetSteps().ContainsKey(vs))
                            errors.Add($"Step '{stepName}' in context '{contextName}' references unknown step '{vs}'");
                    }
                }
            }
        }

        foreach (var (contextName, context) in _contexts)
        {
            if (context.GetValidContexts() is { } validCtxs)
            {
                foreach (var vc in validCtxs)
                {
                    if (!_contexts.ContainsKey(vc))
                        errors.Add($"Context '{contextName}' references unknown context '{vc}'");
                }
            }
        }

        foreach (var (contextName, context) in _contexts)
        {
            foreach (var (stepName, step) in context.GetSteps())
            {
                if (step.ValidContexts is not null)
                {
                    foreach (var vc in step.ValidContexts)
                    {
                        if (!_contexts.ContainsKey(vc))
                            errors.Add($"Step '{stepName}' in context '{contextName}' references unknown context '{vc}'");
                    }
                }
            }
        }

        foreach (var (contextName, context) in _contexts)
        {
            var stepOrder = context.GetStepOrder().ToList();
            foreach (var (stepName, step) in context.GetSteps())
            {
                var gi = step.GatherInfoData;
                if (gi is null) continue;

                if (gi.Questions.Count == 0)
                    errors.Add($"Step '{stepName}' in context '{contextName}' has gather_info with no questions");

                var seenKeys = new HashSet<string>();
                foreach (var q in gi.Questions)
                {
                    if (!seenKeys.Add(q.Key))
                        errors.Add($"Step '{stepName}' in context '{contextName}' has duplicate gather_info question key '{q.Key}'");
                }

                var action = gi.CompletionAction;
                if (action is not null)
                {
                    if (action == "next_step")
                    {
                        var idx = stepOrder.IndexOf(stepName);
                        if (idx >= stepOrder.Count - 1)
                            errors.Add(
                                $"Step '{stepName}' in context '{contextName}' has " +
                                "gather_info completion_action='next_step' but it is " +
                                $"the last step in the context. Either (1) add another " +
                                $"step after '{stepName}', (2) set completion_action to " +
                                "the name of an existing step in this context to jump " +
                                "to it, or (3) set completion_action=null (default) to " +
                                $"stay in '{stepName}' after gathering completes.");
                    }
                    else if (!context.GetSteps().ContainsKey(action))
                    {
                        var available = context.GetSteps().Keys.OrderBy(k => k).ToList();
                        var availableStr = "[" + string.Join(", ", available.Select(k => $"'{k}'")) + "]";
                        errors.Add(
                            $"Step '{stepName}' in context '{contextName}' has " +
                            $"gather_info completion_action='{action}' but '{action}' " +
                            "is not a step in this context. Valid options: 'next_step' " +
                            "(advance to the next sequential step), null (stay in the " +
                            $"current step), or one of {availableStr}.");
                    }
                }
            }
        }

        // Validate that user-defined tools do not collide with reserved
        // native tool names. The runtime auto-injects next_step /
        // change_context / gather_submit when contexts/steps are present,
        // so user tools sharing those names would never be called.
        //
        // The dangling-function check below shares the same guard: BOTH only run
        // when a real tool-name supplier is attached (i.e. this builder belongs
        // to an agent whose tool universe is knowable). A bare ContextBuilder
        // with no agent cannot know the registered tools, so it must NOT red a
        // valid document (parity with Python's `isinstance(registered, dict)`
        // guard).
        if (_toolNameSupplier is not null)
        {
            var registered = _toolNameSupplier();
            if (registered is not null)
            {
                var registeredList = registered.ToList();
                var colliding = registeredList
                    .Where(name => ReservedToolNames.Reserved.Contains(name))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
                if (colliding.Count > 0)
                {
                    var collidingStr = "[" + string.Join(", ", colliding.Select(c => $"'{c}'")) + "]";
                    var reservedStr = "[" + string.Join(", ",
                        ReservedToolNames.Reserved.OrderBy(r => r).Select(r => $"'{r}'")) + "]";
                    errors.Add(
                        $"Tool name(s) {collidingStr} collide with reserved native " +
                        $"tools auto-injected by contexts/steps. The names {reservedStr} " +
                        "are reserved and cannot be used for user-defined SWAIG tools " +
                        "when contexts/steps are in use. Rename your tool(s) to avoid " +
                        "the collision.");
                }

                // Validate step SetFunctions([...]) whitelists against the known
                // tool universe. A step that whitelists a function which is
                // neither a registered SWAIG tool nor a reserved native tool is a
                // DANGLING reference: the emitted step's active-function set
                // silently points at nothing. "none"/[] (explicit disable-all)
                // and any non-list functions value are not reference lists and
                // are skipped. Mirrors Python's r5 F3 dangling-reference check.
                var knownFunctions = new HashSet<string>(registeredList);
                foreach (var native in ReservedToolNames.Reserved)
                {
                    knownFunctions.Add(native);
                }
                foreach (var (contextName, context) in _contexts)
                {
                    foreach (var (stepName, step) in context.GetSteps())
                    {
                        var funcs = StepFunctionList(step);
                        if (funcs is null)
                        {
                            continue;
                        }
                        foreach (var fn in funcs)
                        {
                            if (!knownFunctions.Contains(fn))
                            {
                                var available = "[" + string.Join(", ",
                                    knownFunctions.OrderBy(k => k, StringComparer.Ordinal)
                                        .Select(k => $"'{k}'")) + "]";
                                errors.Add(
                                    $"Step '{stepName}' in context '{contextName}' whitelists " +
                                    $"function '{fn}' via SetFunctions(), but no such SWAIG tool " +
                                    "is registered on the agent and it is not a reserved native " +
                                    "tool. This would emit a dangling function reference. " +
                                    "Register the tool (DefineTool / a skill) or remove it from " +
                                    $"the step. Available: {available}");
                            }
                        }
                    }
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Extract a step's SetFunctions whitelist as a list of names, or null when
    /// it is not a resolvable reference list. A step's functions value may be a
    /// <c>List&lt;string&gt;</c> / <c>IReadOnlyList&lt;string&gt;</c> whitelist,
    /// an empty list, or the string <c>"none"</c> — the empty list and "none"
    /// are explicit disable-all (not references to resolve), and any other
    /// non-list value is skipped, matching Python's <c>isinstance(funcs, list)</c>
    /// guard.
    /// </summary>
    private static IReadOnlyList<string>? StepFunctionList(Step step)
    {
        var funcs = step.Functions;
        return funcs switch
        {
            IReadOnlyList<string> list => list,
            IEnumerable<string> seq => seq.ToList(),
            _ => null,
        };
    }

    public Dictionary<string, object> ToDict()
    {
        var errors = Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException("Validation failed: " + string.Join("; ", errors));

        var result = new Dictionary<string, object>();
        foreach (var name in _contextOrder)
        {
            result[name] = _contexts[name].ToDict();
        }
        return result;
    }

    public static ContextBuilder CreateSimpleContext(string name = "default")
    {
        var builder = new ContextBuilder();
        builder.AddContext(name);
        return builder;
    }
}
