namespace SignalWire.Contexts;

// -- GatherQuestion --

public class GatherQuestion
{
    private readonly string _key;
    private readonly string _question;
    private readonly string _type;
    private readonly bool _confirm;
    private readonly string? _prompt;
    private readonly List<string>? _functions;

    public GatherQuestion(Dictionary<string, object> opts)
    {
        _key = (string)opts["key"];
        _question = (string)opts["question"];
        _type = opts.TryGetValue("type", out var t) ? (string)t : "string";
        _confirm = opts.TryGetValue("confirm", out var c) && c is true;
        _prompt = opts.TryGetValue("prompt", out var p) ? (string)p : null;
        _functions = opts.TryGetValue("functions", out var f) ? (List<string>)f : null;
    }

    public string Key => _key;

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
        return map;
    }
}

// -- GatherInfo --

public class GatherInfo
{
    private readonly List<GatherQuestion> _questions = [];
    private readonly string? _outputKey;
    private readonly string? _completionAction;
    private readonly string? _prompt;

    public GatherInfo(string? outputKey = null, string? completionAction = null, string? prompt = null)
    {
        _outputKey = outputKey;
        _completionAction = completionAction;
        _prompt = prompt;
    }

    public GatherInfo AddQuestion(Dictionary<string, object> opts)
    {
        _questions.Add(new GatherQuestion(opts));
        return this;
    }

    public List<GatherQuestion> Questions => _questions;
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
        return map;
    }
}

// -- Step --

public class Step
{
    private readonly string _name;
    private string? _text;
    private string? _stepCriteria;
    private object? _functions;
    private List<string>? _validSteps;
    private List<string>? _validContexts;
    private List<Dictionary<string, object>> _sections = [];
    private GatherInfo? _gatherInfo;
    private bool _end;
    private bool _skipUserTurn;
    private bool _skipToNextStep;
    private string? _resetSystemPrompt;
    private string? _resetUserPrompt;
    private bool _resetConsolidate;
    private bool _resetFullReset;

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

    public Step AddBullets(string title, List<string> bullets)
    {
        if (_text is not null)
            throw new InvalidOperationException("Cannot add POM sections when SetText() has been used.");
        _sections.Add(new Dictionary<string, object> { ["title"] = title, ["bullets"] = bullets });
        return this;
    }

    public Step ClearSections()
    {
        _sections = [];
        _text = null;
        return this;
    }

    public Step SetStepCriteria(string criteria) { _stepCriteria = criteria; return this; }
    public Step SetFunctions(object functions) { _functions = functions; return this; }
    public Step SetValidSteps(List<string> steps) { _validSteps = steps; return this; }
    public Step SetValidContexts(List<string> contexts) { _validContexts = contexts; return this; }
    public Step SetEnd(bool end) { _end = end; return this; }
    public Step SetSkipUserTurn(bool skip) { _skipUserTurn = skip; return this; }
    public Step SetSkipToNextStep(bool skip) { _skipToNextStep = skip; return this; }

    public Step SetGatherInfo(Dictionary<string, object> opts)
    {
        _gatherInfo = new GatherInfo(
            opts.TryGetValue("output_key", out var ok) ? (string)ok : null,
            opts.TryGetValue("completion_action", out var ca) ? (string)ca : null,
            opts.TryGetValue("prompt", out var p) ? (string)p : null);
        return this;
    }

    public Step AddGatherQuestion(Dictionary<string, object> opts)
    {
        _gatherInfo ??= new GatherInfo();
        _gatherInfo.AddQuestion(opts);
        return this;
    }

    public Step SetResetSystemPrompt(string sp) { _resetSystemPrompt = sp; return this; }
    public Step SetResetUserPrompt(string up) { _resetUserPrompt = up; return this; }
    public Step SetResetConsolidate(bool c) { _resetConsolidate = c; return this; }
    public Step SetResetFullReset(bool f) { _resetFullReset = f; return this; }

    public List<string>? ValidSteps => _validSteps;
    public List<string>? ValidContexts => _validContexts;
    public GatherInfo? GatherInfoData => _gatherInfo;

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

public class Context
{
    private const int MaxStepsPerContext = 100;
    private readonly string _name;
    private readonly Dictionary<string, Step> _steps = [];
    private readonly List<string> _stepOrder = [];
    private List<string>? _validContexts;
    private List<string>? _validSteps;
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
            if (opts.TryGetValue("functions", out var f)) step.SetFunctions(f);
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

    public Dictionary<string, Step> GetSteps() => _steps;
    public List<string> GetStepOrder() => [.. _stepOrder];

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

    public Context AddBullets(string title, List<string> bullets)
    {
        if (_promptText is not null)
            throw new InvalidOperationException("Cannot add POM sections when SetPrompt() has been used.");
        _promptSections.Add(new Dictionary<string, object> { ["title"] = title, ["bullets"] = bullets });
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

    public Context AddSystemBullets(string title, List<string> bullets)
    {
        if (_systemPrompt is not null)
            throw new InvalidOperationException("Cannot add POM sections when SetSystemPrompt() has been used.");
        _systemPromptSections.Add(new Dictionary<string, object> { ["title"] = title, ["bullets"] = bullets });
        return this;
    }

    // -- Config setters --

    public Context SetValidContexts(List<string> contexts) { _validContexts = contexts; return this; }
    public Context SetValidSteps(List<string> steps) { _validSteps = steps; return this; }
    public Context SetPostPrompt(string postPrompt) { _postPrompt = postPrompt; return this; }
    public Context SetConsolidate(bool consolidate) { _consolidate = consolidate; return this; }
    public Context SetFullReset(bool fullReset) { _fullReset = fullReset; return this; }
    public Context SetUserPrompt(string userPrompt) { _userPrompt = userPrompt; return this; }
    public Context SetIsolated(bool isolated) { _isolated = isolated; return this; }

    // -- Fillers --

    public Context SetEnterFillers(Dictionary<string, List<string>> fillers) { _enterFillers = fillers; return this; }
    public Context SetExitFillers(Dictionary<string, List<string>> fillers) { _exitFillers = fillers; return this; }

    public Context AddEnterFiller(string lang, string text)
    {
        _enterFillers ??= [];
        if (!_enterFillers.ContainsKey(lang)) _enterFillers[lang] = [];
        _enterFillers[lang].Add(text);
        return this;
    }

    public Context AddExitFiller(string lang, string text)
    {
        _exitFillers ??= [];
        if (!_exitFillers.ContainsKey(lang)) _exitFillers[lang] = [];
        _exitFillers[lang].Add(text);
        return this;
    }

    public List<string>? GetValidContexts() => _validContexts;

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

        return map;
    }
}

// -- ContextBuilder --

public class ContextBuilder
{
    private const int MaxContexts = 50;
    private readonly Dictionary<string, Context> _contexts = [];
    private readonly List<string> _contextOrder = [];

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

    public List<string> Validate()
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
            var stepOrder = context.GetStepOrder();
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
                            errors.Add($"Step '{stepName}' in context '{contextName}' has gather_info completion_action='next_step' but it is the last step");
                    }
                    else if (!context.GetSteps().ContainsKey(action))
                    {
                        errors.Add($"Step '{stepName}' in context '{contextName}' has gather_info completion_action='{action}' but step '{action}' does not exist");
                    }
                }
            }
        }

        return errors;
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

    public static ContextBuilder CreateSimpleContext(string name)
    {
        var builder = new ContextBuilder();
        builder.AddContext(name);
        return builder;
    }
}
