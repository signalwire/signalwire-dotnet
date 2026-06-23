# Calling Commands (.NET)

## Overview

The Calling namespace provides REST-based call control with 37 commands. These
methods control live calls without requiring a WebSocket connection. Every
command is asynchronous and returns `Task<Dictionary<string, object?>>`; the
per-call commands take the `callId` first and the RELAY-style payload as a
`Dictionary<string, object?>`.

## Dial

`DialAsync` takes a single payload `Dictionary`:

```csharp
await client.Calling.DialAsync(new Dictionary<string, object?>
{
    ["command"] = "dial",
    ["params"]  = new Dictionary<string, object>
    {
        ["from"] = "+15559876543",
        ["to"]   = "+15551234567",
        ["url"]  = "https://example.com/call-handler",
    },
});
```

## Play

```csharp
// TTS
await client.Calling.PlayAsync(callId, new Dictionary<string, object?>
{
    ["type"]   = "tts",
    ["params"] = new Dictionary<string, object> { ["text"] = "Hello!" },
});

// Audio file
await client.Calling.PlayAsync(callId, new Dictionary<string, object?>
{
    ["type"]   = "audio",
    ["params"] = new Dictionary<string, object> { ["url"] = "https://example.com/audio.mp3" },
});
```

Playback control: `PlayPauseAsync`, `PlayResumeAsync`, `PlayStopAsync`,
`PlayVolumeAsync` (each takes `callId` + optional payload).

## Record

```csharp
await client.Calling.RecordAsync(callId, new Dictionary<string, object?>
{
    ["beep"]      = true,
    ["format"]    = "wav",
    ["direction"] = "both",
});
```

Record control: `RecordPauseAsync`, `RecordResumeAsync`, `RecordStopAsync`.

## Collect

```csharp
await client.Calling.CollectAsync(callId, new Dictionary<string, object?>
{
    ["digits"] = new Dictionary<string, object>
    {
        ["max"]           = 4,
        ["digit_timeout"] = 5,
    },
});
```

## Detect

```csharp
await client.Calling.DetectAsync(callId, new Dictionary<string, object?>
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
await client.Calling.TapAsync(callId, new Dictionary<string, object?>
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
await client.Calling.StreamAsync(callId, new Dictionary<string, object?>
{
    ["url"]       = "wss://listener.example.com/stream",
    ["direction"] = "both",
    ["codec"]     = "PCMU",
});
```

## AI

The AI commands are `AiMessageAsync`, `AiHoldAsync`, `AiUnholdAsync`, and
`AiStopAsync`:

```csharp
await client.Calling.AiMessageAsync(callId, new Dictionary<string, object?>
{
    ["role"]    = "user",
    ["message"] = "Please summarize the call.",
});
```

## Transcribe

```csharp
await client.Calling.TranscribeAsync(callId, new Dictionary<string, object?>
{
    ["language"]  = "en-US",
    ["direction"] = "both",
});
```

Live variants: `LiveTranscribeAsync`, `LiveTranslateAsync`.

## Denoise

```csharp
await client.Calling.DenoiseAsync(callId);
await client.Calling.DenoiseStopAsync(callId);
```

## End and Transfer

```csharp
await client.Calling.EndAsync(callId);
await client.Calling.TransferAsync(callId, new Dictionary<string, object?>
{
    ["dest"] = "+15551234567",
});
await client.Calling.DisconnectAsync(callId);
```

## Refer

```csharp
await client.Calling.ReferAsync(callId, new Dictionary<string, object?>
{
    ["to_uri"] = "sip:agent@example.com",
});
```
