# Calling Commands (.NET)

## Overview

The Calling namespace provides REST-based call control. These methods control live
calls without requiring a WebSocket connection. Every command is asynchronous and
returns `Task<Dictionary<string, object?>>`. Per-call commands take the `callId`
first, followed by the command's typed parameters.

<!-- snippet-setup -->
```csharp
using SignalWire.REST;
using System.Collections.Generic;
using System.Threading.Tasks;
// Shared context the fragments below assume: a constructed `client` and a live callId.
RestClient client = new RestClient("project", "token", "example.signalwire.com");
string callId = "call-uuid";
```

## Dial

`DialAsync` places an outbound call. It takes typed `from`/`to` params (plus optional
routing fields):

```csharp
await client.Calling.DialAsync(
    from: "+15559876543",
    to:   "+15551234567",
    url:  "https://example.com/call-handler");
```

## Play

`PlayAsync` takes the `callId` and a `play` list of media objects:

```csharp
// TTS + audio file in one playlist
await client.Calling.PlayAsync(callId, new List<object?>
{
    new Dictionary<string, object?>
    {
        ["type"]   = "tts",
        ["params"] = new Dictionary<string, object> { ["text"] = "Hello!" },
    },
    new Dictionary<string, object?>
    {
        ["type"]   = "audio",
        ["params"] = new Dictionary<string, object> { ["url"] = "https://example.com/audio.mp3" },
    },
});
```

Playback control takes `callId` + `controlId`: `PlayPauseAsync`, `PlayResumeAsync`,
`PlayStopAsync`, `PlayVolumeAsync` (the last also takes a `volume` double).

## Record

`RecordAsync` takes the `callId` and an optional `audio` settings dictionary:

```csharp
await client.Calling.RecordAsync(callId, audio: new Dictionary<string, object?>
{
    ["beep"]      = true,
    ["format"]    = "wav",
    ["direction"] = "both",
});
```

Record control (`callId` + `controlId`): `RecordPauseAsync`, `RecordResumeAsync`,
`RecordStopAsync`.

## Collect

`CollectAsync` takes the `callId` and optional typed params (`digits`, `speech`, …):

```csharp
await client.Calling.CollectAsync(callId, digits: new Dictionary<string, object?>
{
    ["max"]           = 4,
    ["digit_timeout"] = 5,
});
```

## Detect

`DetectAsync` takes the `callId` and a required `detect` dictionary:

```csharp
await client.Calling.DetectAsync(callId, detect: new Dictionary<string, object?>
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

`TapAsync` takes the `callId`, a `tap` dictionary, and a `device` dictionary:

```csharp
await client.Calling.TapAsync(callId,
    tap: new Dictionary<string, object?>
    {
        ["type"]      = "audio",
        ["direction"] = "both",
    },
    device: new Dictionary<string, object?>
    {
        ["type"]  = "rtp",
        ["codec"] = "PCMU",
    });
```

## Stream

`StreamAsync` takes the `callId` and a required `url`:

```csharp
await client.Calling.StreamAsync(callId,
    url:   "wss://listener.example.com/stream",
    codec: "PCMU");
```

## AI

The AI commands are `AiMessageAsync`, `AiHoldAsync`, `AiUnholdAsync`, and
`AiStopAsync`:

```csharp
await client.Calling.AiMessageAsync(callId,
    role:        "user",
    messageText: "Please summarize the call.");
```

## Transcribe

`TranscribeAsync` takes the `callId` (plus optional `controlId`/`statusUrl`):

```csharp
await client.Calling.TranscribeAsync(callId,
    statusUrl: "https://example.com/transcribe-status");
```

Live variants: `LiveTranscribeAsync`, `LiveTranslateAsync` (each takes a required
`action` dictionary).

## Denoise

```csharp
await client.Calling.DenoiseAsync(callId);
await client.Calling.DenoiseStopAsync(callId);
```

## End and Transfer

```csharp
await client.Calling.EndAsync(callId);
await client.Calling.TransferAsync(callId, dest: new Dictionary<string, object?>
{
    ["dest"] = "+15551234567",
});
await client.Calling.DisconnectAsync(callId);
```

## Refer

`ReferAsync` takes the `callId` and a `device` dictionary:

```csharp
await client.Calling.ReferAsync(callId, device: new Dictionary<string, object?>
{
    ["to_uri"] = "sip:agent@example.com",
});
```
