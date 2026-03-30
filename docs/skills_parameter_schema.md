# Skills Parameter Schema (.NET)

## Overview

Each built-in skill accepts configuration parameters that control its behavior. This document lists the parameter schema for every built-in skill.

## datetime

Provides date, time, and timezone conversion tools.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `timezone` | `string` | `"UTC"` | Default timezone for responses |
| `format` | `string` | `"12h"` | Time format (`12h` or `24h`) |

### Registered Tools

| Tool | Description |
|------|-------------|
| `get_current_time` | Get the current date and time |
| `convert_timezone` | Convert a time between timezones |

### Example

```csharp
agent.AddSkill("datetime", new Dictionary<string, object>
{
    ["timezone"] = "America/New_York",
    ["format"]   = "24h",
});
```

## math

Provides mathematical calculation tools.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `precision` | `int` | `4` | Decimal precision for results |

### Registered Tools

| Tool | Description |
|------|-------------|
| `calculate` | Evaluate a mathematical expression |
| `convert_units` | Convert between units of measurement |

### Example

```csharp
agent.AddSkill("math", new Dictionary<string, object>
{
    ["precision"] = 6,
});
```

## web_search

Google Custom Search API integration.

### Parameters

| Parameter | Type | Default | Required | Description |
|-----------|------|---------|----------|-------------|
| `api_key` | `string` | - | Yes | Google API key |
| `search_engine_id` | `string` | - | Yes | Google Custom Search Engine ID |
| `num_results` | `int` | `3` | No | Number of results to return |
| `delay` | `int` | `0` | No | Delay in ms before searching |

### Registered Tools

| Tool | Description |
|------|-------------|
| `web_search` | Search the web using Google |

### Example

```csharp
agent.AddSkill("web_search", new Dictionary<string, object>
{
    ["api_key"]          = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY")!,
    ["search_engine_id"] = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID")!,
    ["num_results"]      = 5,
});
```

### Required Environment Variables

- `GOOGLE_SEARCH_API_KEY` -- Google API key
- `GOOGLE_SEARCH_ENGINE_ID` -- Custom Search Engine ID

## datasphere

SignalWire Datasphere semantic search integration.

### Parameters

| Parameter | Type | Default | Required | Description |
|-----------|------|---------|----------|-------------|
| `project_id` | `string` | env var | No | SignalWire project ID |
| `token` | `string` | env var | No | SignalWire API token |
| `space` | `string` | env var | No | SignalWire space hostname |
| `document_id` | `string` | - | No | Specific document to search |
| `count` | `int` | `5` | No | Number of results |

### Registered Tools

| Tool | Description |
|------|-------------|
| `search_knowledge` | Search the knowledge base |

### Example

```csharp
agent.AddSkill("datasphere", new Dictionary<string, object>
{
    ["document_id"] = "doc-abc-123",
    ["count"]       = 10,
});
```

### Required Environment Variables

- `SIGNALWIRE_PROJECT_ID` -- SignalWire project ID
- `SIGNALWIRE_API_TOKEN` -- SignalWire API token
- `SIGNALWIRE_SPACE` -- SignalWire space hostname
