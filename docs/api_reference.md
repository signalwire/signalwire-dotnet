# API Reference (.NET)

## AgentBase

The core class for building AI agents. Extends `Service` with prompt management, SWAIG tool dispatch, context switching, and skills.

### Constructor

```csharp
var agent = new AgentBase(new AgentOptions
{
    Name  = "my-agent",
    Route = "/agent",
    Host  = "0.0.0.0",
    Port  = 3000,
});
```

### Prompt Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `SetPromptText` | `AgentBase SetPromptText(string text)` | Set raw prompt text (non-POM) |
| `SetPostPrompt` | `AgentBase SetPostPrompt(string text)` | Set the post-prompt for summary generation |
| `PromptAddSection` | `AgentBase PromptAddSection(string title, string body, List<string>? bullets = null)` | Add a POM section |
| `PromptAddSubsection` | `AgentBase PromptAddSubsection(string parentTitle, string title, string body)` | Add a nested subsection |
| `PromptAddToSection` | `AgentBase PromptAddToSection(string title, string? body = null, List<string>? bullets = null)` | Append to an existing section |
| `PromptHasSection` | `bool PromptHasSection(string title)` | Check if a section exists |
| `GetPrompt` | `object GetPrompt()` | Get the prompt payload (POM array or text) |

### Tool Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `DefineTool` | `AgentBase DefineTool(string name, string description, Dictionary<string, object> parameters, Func<...> handler, bool secure = false)` | Define a tool with a handler |
| `RegisterSwaigFunction` | `AgentBase RegisterSwaigFunction(Dictionary<string, object> funcDef)` | Register a raw SWAIG function |
| `DefineTools` | `AgentBase DefineTools(List<Dictionary<string, object>> toolDefs)` | Register multiple tool definitions |
| `OnFunctionCall` | `FunctionResult? OnFunctionCall(string name, Dictionary<string, object> args, Dictionary<string, object?> rawData)` | Dispatch a function call |

### AI Config Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `AddHint` | `AgentBase AddHint(string hint)` | Add a speech recognition hint |
| `AddHints` | `AgentBase AddHints(List<string> hints)` | Add multiple hints |
| `AddPatternHint` | `AgentBase AddPatternHint(string pattern)` | Add a pattern-based hint |
| `AddLanguage` | `AgentBase AddLanguage(string name, string code, string voice)` | Add a language option |
| `SetLanguages` | `AgentBase SetLanguages(List<Dictionary<string, object>> languages)` | Set all languages |
| `AddPronunciation` | `AgentBase AddPronunciation(string replace, string with, string ignore = "")` | Add pronunciation rule |
| `SetPronunciations` | `AgentBase SetPronunciations(List<Dictionary<string, object>> pronunciations)` | Set all pronunciations |
| `SetParam` | `AgentBase SetParam(string key, object value)` | Set a single AI parameter |
| `SetParams` | `AgentBase SetParams(Dictionary<string, object> parameters)` | Set all AI parameters |
| `SetGlobalData` | `AgentBase SetGlobalData(Dictionary<string, object> data)` | Set global data |
| `UpdateGlobalData` | `AgentBase UpdateGlobalData(Dictionary<string, object> data)` | Merge into global data |
| `SetNativeFunctions` | `AgentBase SetNativeFunctions(List<string> functions)` | Set native function list |
| `SetInternalFillers` | `AgentBase SetInternalFillers(List<string> fillers)` | Set filler phrases |
| `AddInternalFiller` | `AgentBase AddInternalFiller(string filler)` | Add a filler phrase |
| `EnableDebugEvents` | `AgentBase EnableDebugEvents(string level = "all")` | Enable debug event reporting |
| `SetPromptLlmParams` | `AgentBase SetPromptLlmParams(Dictionary<string, object> parameters)` | Set prompt LLM params |
| `SetPostPromptLlmParams` | `AgentBase SetPostPromptLlmParams(Dictionary<string, object> parameters)` | Set post-prompt LLM params |

### Verb Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `AddPreAnswerVerb` | `AgentBase AddPreAnswerVerb(string verb, object config)` | Add a verb before answering |
| `AddPostAnswerVerb` | `AgentBase AddPostAnswerVerb(string verb, object config)` | Add a verb after answering |
| `AddAnswerVerb` | `AgentBase AddAnswerVerb(string verb, object config)` | Alias for `AddPostAnswerVerb` |
| `AddPostAiVerb` | `AgentBase AddPostAiVerb(string verb, object config)` | Add a verb after AI disconnects |
| `ClearPreAnswerVerbs` | `AgentBase ClearPreAnswerVerbs()` | Remove all pre-answer verbs |
| `ClearPostAnswerVerbs` | `AgentBase ClearPostAnswerVerbs()` | Remove all post-answer verbs |
| `ClearPostAiVerbs` | `AgentBase ClearPostAiVerbs()` | Remove all post-AI verbs |

### Context Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `DefineContexts` | `ContextBuilder DefineContexts()` | Get the context builder |
| `Contexts` | `ContextBuilder Contexts()` | Alias for `DefineContexts` |

### Skill Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `GetSkillManager` | `SkillManager GetSkillManager()` | Get the skill manager |
| `AddSkill` | `AgentBase AddSkill(string name, Dictionary<string, object>? parameters = null)` | Load a skill |
| `RemoveSkill` | `AgentBase RemoveSkill(string name)` | Remove a skill |
| `ListSkills` | `List<string> ListSkills()` | List loaded skills |
| `HasSkill` | `bool HasSkill(string name)` | Check if skill is loaded |

### Callback Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `SetDynamicConfigCallback` | `AgentBase SetDynamicConfigCallback(Action<...> callback)` | Set per-request config callback |
| `OnSummary` | `AgentBase OnSummary(Action<string, Dictionary<string, object?>?, Dictionary<string, string>> callback)` | Set summary callback |
| `OnDebugEvent` | `AgentBase OnDebugEvent(Action<Dictionary<string, object?>?, Dictionary<string, string>> callback)` | Set debug event handler |

### Web/URL Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `SetWebHookUrl` | `AgentBase SetWebHookUrl(string url)` | Set SWAIG webhook URL |
| `SetPostPromptUrl` | `AgentBase SetPostPromptUrl(string url)` | Set post-prompt callback URL |
| `ManualSetProxyUrl` | `AgentBase ManualSetProxyUrl(string url)` | Override proxy URL |
| `AddSwaigQueryParams` | `AgentBase AddSwaigQueryParams(Dictionary<string, string> parameters)` | Add query params to SWAIG URL |
| `ClearSwaigQueryParams` | `AgentBase ClearSwaigQueryParams()` | Clear SWAIG query params |
| `AddFunctionInclude` | `AgentBase AddFunctionInclude(Dictionary<string, object> include)` | Add a function include |
| `SetFunctionIncludes` | `AgentBase SetFunctionIncludes(List<Dictionary<string, object>> includes)` | Set function includes |

### SWML Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `RenderSwml` | `Dictionary<string, object> RenderSwml()` | Render the SWML document |
| `RenderSwmlWithContext` | `Dictionary<string, object> RenderSwmlWithContext(Dictionary<string, object?>? requestBody, Dictionary<string, string> headers)` | Render with request context |
| `BuildAiVerb` | `Dictionary<string, object> BuildAiVerb(Dictionary<string, string>? headers = null)` | Build the AI verb block |
| `CloneForRequest` | `AgentBase CloneForRequest()` | Deep-copy for per-request customization |

---

## FunctionResult

See [SWAIG Reference](swaig_reference.md) for the complete `FunctionResult` API.

## Service

Base class for SWML HTTP services. `AgentBase` inherits from `Service`.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Service name |
| `Route` | `string` | HTTP route path |
| `Host` | `string` | Bind address |
| `Port` | `int` | HTTP port |
| `Document` | `Document` | SWML document |

### Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `Run` | `void Run()` | Start the HTTP server |
| `BasicAuthUser` | `string BasicAuthUser()` | Get the basic auth username |
| `BasicAuthPassword` | `string BasicAuthPassword()` | Get the basic auth password |
| `GetBasicAuthCredentials` | `(string, string) GetBasicAuthCredentials()` | Get both credentials |

## AgentServer

Multi-agent HTTP server. Registers agents at routes and dispatches requests.

```csharp
var server = new AgentServer(host: "0.0.0.0", port: 3000);
server.Register(agent);
server.Run();
```
