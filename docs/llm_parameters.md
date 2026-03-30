# LLM Parameters Reference (.NET)

## Overview

LLM parameters control the AI model's behavior during conversations. These can be set at the prompt level (via `SetPromptLlmParams`) and the post-prompt level (via `SetPostPromptLlmParams`).

## Setting LLM Parameters

### Prompt LLM Parameters

```csharp
agent.SetPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"]       = 0.3,
    ["top_p"]             = 0.9,
    ["barge_confidence"]  = 0.7,
    ["presence_penalty"]  = 0.1,
    ["frequency_penalty"] = 0.2,
});
```

### Post-Prompt LLM Parameters

```csharp
agent.SetPostPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"] = 0.1,
    ["top_p"]       = 0.5,
});
```

## Parameter Reference

### Core Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `temperature` | `float` | 0.0-2.0 | 1.0 | Randomness of responses. Lower = more deterministic |
| `top_p` | `float` | 0.0-1.0 | 1.0 | Nucleus sampling threshold |
| `presence_penalty` | `float` | -2.0-2.0 | 0.0 | Penalize repeated topics |
| `frequency_penalty` | `float` | -2.0-2.0 | 0.0 | Penalize repeated tokens |
| `max_tokens` | `int` | 1-4096 | varies | Maximum response length |

### Voice AI Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `barge_confidence` | `float` | 0.5 | Confidence threshold for barge-in (interruption) |
| `barge_match_string` | `string` | - | Specific phrase that triggers barge-in |

## AI Model Parameters

Set via `SetParams()`:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ai_model` | `string` | `"gpt-4.1-nano"` | AI model to use |
| `wait_for_user` | `bool` | `true` | Wait for user to speak first |
| `end_of_speech_timeout` | `int` | `500` | Silence timeout in ms |
| `attention_timeout` | `int` | `30000` | Inactivity timeout in ms |
| `ai_volume` | `int` | `0` | AI voice volume adjustment (-10 to 10) |
| `languages_enabled` | `bool` | `false` | Enable multi-language support |
| `local_tz` | `string` | `"UTC"` | Local timezone for AI context |
| `internal_fillers` | `List<string>` | `[]` | Filler phrases during processing |
| `debug_events` | `string` | - | Debug event level (`"all"`) |
| `sip_routing` | `bool` | `false` | Enable SIP routing |

## Usage Examples

### Customer Service Agent

Low temperature for consistent, accurate responses:

```csharp
agent.SetPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"]       = 0.2,
    ["top_p"]             = 0.8,
    ["frequency_penalty"] = 0.3,
});

agent.SetParams(new Dictionary<string, object>
{
    ["ai_model"]              = "gpt-4.1-nano",
    ["end_of_speech_timeout"] = 500,
    ["attention_timeout"]     = 15000,
});
```

### Creative Assistant

Higher temperature for varied, creative responses:

```csharp
agent.SetPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"]      = 0.9,
    ["top_p"]            = 0.95,
    ["presence_penalty"] = 0.5,
});
```

### Summary Generation

Very low temperature for structured, deterministic summaries:

```csharp
agent.SetPostPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"] = 0.1,
    ["top_p"]       = 0.5,
    ["max_tokens"]  = 500,
});
```
