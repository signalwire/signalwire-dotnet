# SWML Service Guide (.NET)

## Overview

The `Service` class is the base for all SignalWire HTTP services. It manages a SWML `Document`, provides schema-driven verb methods, handles HTTP requests with Basic authentication, and supports routing callbacks. `AgentBase` extends `Service` with AI-specific features.

## Service Class

### Constructor

```csharp
using SignalWire.SWML;

var service = new Service(new ServiceOptions
{
    Name              = "my-service",
    Route             = "/service",
    Host              = "0.0.0.0",
    Port              = 3000,
    BasicAuthUser     = "user",
    BasicAuthPassword = "pass",
});
```

### ServiceOptions

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Name` | `string` | required | Service name |
| `Route` | `string` | `"/"` | HTTP route path |
| `Host` | `string` | `"0.0.0.0"` | Bind address |
| `Port` | `int?` | `null` (auto) | HTTP port |
| `BasicAuthUser` | `string?` | `"signalwire"` | Basic auth username |
| `BasicAuthPassword` | `string?` | auto-generated | Basic auth password |

## Document Class

The `Document` class builds SWML documents with schema validation.

### Creating Documents

```csharp
var doc = new Document();
doc.AddSection("main", new List<Dictionary<string, object>>
{
    new() { ["answer"] = new Dictionary<string, object>() },
    new() { ["play"] = new Dictionary<string, object> { ["url"] = "say:Hello" } },
    new() { ["hangup"] = new Dictionary<string, object>() },
});
```

### Schema Validation

The SDK includes the SWML schema (`schema.json`) and validates verbs at construction time.

## SWML Rendering Pipeline

`AgentBase` uses a 5-phase pipeline to build the SWML document:

```
Phase 1: Pre-answer verbs    (hold music, initial play)
Phase 2: Answer verb          (auto-answer with max_duration)
Phase 3: Record call verb     (if recording enabled)
Phase 4: Post-answer verbs    (custom verbs after answer)
Phase 5: AI verb              (prompt, tools, params, contexts)
Phase 6: Post-AI verbs        (goodbye message, cleanup)
```

### Example SWML Output

```json
{
  "version": "1.0.0",
  "sections": {
    "main": [
      {"answer": {"max_duration": 14400}},
      {"record_call": {"format": "wav", "stereo": false}},
      {"ai": {
        "prompt": {
          "pom": [
            {"title": "Role", "body": "You are a helpful assistant."}
          ]
        },
        "post_prompt": {"text": "Summarize the conversation."},
        "post_prompt_url": "http://user:pass@host:3000/agent/post_prompt",
        "params": {"ai_model": "gpt-4.1-nano"},
        "hints": ["SignalWire", "SWML"],
        "languages": [{"name": "English", "code": "en-US", "voice": "inworld.Mark"}],
        "SWAIG": {
          "functions": [
            {
              "function": "get_weather",
              "purpose": "Get weather for a location",
              "argument": {"type": "object", "properties": {"location": {"type": "string"}}},
              "web_hook_url": "http://user:pass@host:3000/agent/swaig"
            }
          ]
        }
      }}
    ]
  }
}
```

## HTTP Endpoints

When an agent runs, it exposes these endpoints:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/{route}` | GET/POST | Serve the SWML document |
| `/{route}/swaig` | POST | SWAIG function dispatch |
| `/{route}/post_prompt` | POST | Post-prompt callback |
| `/health` | GET | Health check |
| `/ready` | GET | Readiness check |

## Custom Routing

You can add custom routes to a service:

```csharp
// AgentBase inherits routing from Service
agent.AddRoute("/custom-endpoint", (requestData, headers) =>
{
    return new { status = "ok", timestamp = DateTime.UtcNow };
});
```

## Proxy Detection

The service auto-detects proxy URLs from request headers:

1. `X-Forwarded-Host` + `X-Forwarded-Proto`
2. `X-Original-Host`
3. `Host` header
4. Fallback to `Host:Port`

Override with `ManualSetProxyUrl()`:

```csharp
agent.ManualSetProxyUrl("https://myagent.example.com");
```
