# Calling Commands (.NET)

## Overview

The Calling namespace provides REST-based call control with 37 commands. These methods control live calls without requiring a WebSocket connection.

## Dial

```csharp
client.Calling.Dial(
    from: "+15559876543",
    to:   "+15551234567",
    url:  "https://example.com/call-handler"
);
```

## Play

```csharp
// TTS
client.Calling.Play(callId, new Dictionary<string, object>
{
    ["type"]   = "tts",
    ["params"] = new Dictionary<string, object> { ["text"] = "Hello!" },
});

// Audio file
client.Calling.Play(callId, new Dictionary<string, object>
{
    ["type"]   = "audio",
    ["params"] = new Dictionary<string, object> { ["url"] = "https://example.com/audio.mp3" },
});
```

## Record

```csharp
client.Calling.Record(callId, new Dictionary<string, object>
{
    ["beep"]      = true,
    ["format"]    = "wav",
    ["direction"] = "both",
});
```

## Collect

```csharp
client.Calling.Collect(callId, new Dictionary<string, object>
{
    ["digits"] = new Dictionary<string, object>
    {
        ["max"]           = 4,
        ["digit_timeout"] = 5,
    },
});
```

## Connect

```csharp
client.Calling.Connect(callId, new Dictionary<string, object>
{
    ["devices"] = new List<List<Dictionary<string, object>>>
    {
        new()
        {
            new Dictionary<string, object>
            {
                ["type"]   = "phone",
                ["params"] = new Dictionary<string, object>
                {
                    ["to_number"]   = "+15551234567",
                    ["from_number"] = "+15559876543",
                },
            },
        },
    },
});
```

## Detect

```csharp
client.Calling.Detect(callId, new Dictionary<string, object>
{
    ["type"]   = "machine",
    ["params"] = new Dictionary<string, object>
    {
        ["initial_timeout"]     = 4.0,
        ["end_silence_timeout"] = 2.0,
    },
});
```

## Tap

```csharp
client.Calling.Tap(callId, new Dictionary<string, object>
{
    ["type"]   = "audio",
    ["params"] = new Dictionary<string, object>
    {
        ["direction"] = "both",
        ["codec"]     = "PCMU",
    },
});
```

## Stream

```csharp
client.Calling.Stream(callId, new Dictionary<string, object>
{
    ["url"]       = "wss://listener.example.com/stream",
    ["direction"] = "both",
    ["codec"]     = "PCMU",
});
```

## AI

```csharp
client.Calling.Ai(callId, new Dictionary<string, object>
{
    ["prompt"] = new Dictionary<string, object> { ["text"] = "You are helpful." },
    ["SWAIG"]  = new Dictionary<string, object>
    {
        ["functions"] = new List<Dictionary<string, object>>
        {
            new()
            {
                ["function"]     = "get_info",
                ["purpose"]      = "Get information",
                ["web_hook_url"] = "https://example.com/swaig",
            },
        },
    },
});
```

## Transcribe

```csharp
client.Calling.Transcribe(callId, new Dictionary<string, object>
{
    ["language"]  = "en-US",
    ["direction"] = "both",
});
```

## Denoise

```csharp
client.Calling.Denoise(callId, new Dictionary<string, object>());
```

## Answer / Hangup

```csharp
client.Calling.Answer(callId);
client.Calling.Hangup(callId);
```

## Send Digits

```csharp
client.Calling.SendDigits(callId, "1234#");
```

## Queue

```csharp
client.Calling.Enqueue(callId, new Dictionary<string, object>
{
    ["queue_name"] = "support",
    ["max_wait"]   = 300,
});
```

## Conference

```csharp
client.Calling.Conference(callId, new Dictionary<string, object>
{
    ["name"]  = "team-meeting",
    ["beep"]  = true,
    ["muted"] = false,
});
```
