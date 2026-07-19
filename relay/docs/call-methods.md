# Call Methods Reference (.NET)

## Overview

The `Call` object represents a live phone call. Simple operations
(answer, hangup, transfer, send digits, ...) are `async` methods returning a
`Task`. Media operations (play, record, collect, detect, tap, ...) are
**synchronous** methods that return an `*Action` handle; you `await` the
handle's `WaitAsync()` to block until the operation completes.

<!-- snippet-setup -->
```csharp
using SignalWire.Relay;
using System.Collections.Generic;
using System.Threading.Tasks;
// Shared context: `call` is a live Call delivered to your OnCall handler
// (see the Getting Started page). Declared here so each fragment resolves.
Call call = null!;
```

## Answer and Hangup

```csharp
await call.AnswerAsync();
await call.HangupAsync();
await call.HangupAsync(reason: "busy");
```

## Play

Play audio or TTS to the call. The typed convenience methods
(`PlayTts`, `PlayAudio`, `PlaySilence`, `PlayRingtone`) build the media
shape for you; the generic `Play(extra)` takes a raw RELAY payload. All
return a `PlayAction` synchronously.

```csharp
// TTS
var action = call.PlayTts("Hello!");
await action.WaitAsync();

// Audio file
action = call.PlayAudio("https://example.com/audio.mp3");
await action.WaitAsync();

// Silence (seconds)
action = call.PlaySilence(2);
await action.WaitAsync();

// Raw payload via the generic Play(extra)
action = call.Play(new Dictionary<string, object?>
{
    ["play"] = new[]
    {
        new Dictionary<string, object>
        {
            ["type"]   = "tts",
            ["params"] = new Dictionary<string, object> { ["text"] = "Hello!" },
        },
    },
});
await action.WaitAsync();
```

## Record

The wire payload nests the recording options in a `record.audio` envelope:

```csharp
var action = call.Record(new Dictionary<string, object?>
{
    ["record"] = new Dictionary<string, object?>
    {
        ["audio"] = new Dictionary<string, object?>
        {
            ["beep"]               = true,
            ["format"]             = "wav",
            ["stereo"]             = false,
            ["direction"]          = "both",
            ["end_silence_timeout"] = 5,
        },
    },
});

await action.WaitAsync();
// action.Url contains the recording URL once finished
```

## Play and Collect (DTMF/Speech)

```csharp
var action = call.PlayAndCollect(new Dictionary<string, object?>
{
    ["play"] = new[]
    {
        new Dictionary<string, object>
        {
            ["type"]   = "tts",
            ["params"] = new Dictionary<string, object> { ["text"] = "Press 1 for sales, 2 for support." },
        },
    },
    ["collect"] = new Dictionary<string, object>
    {
        ["digits"] = new Dictionary<string, object>
        {
            ["max"]           = 1,
            ["digit_timeout"] = 5.0,
        },
        ["initial_timeout"] = 10.0,
    },
});

var result = await action.WaitAsync();
```

## Connect

Bridge the call to another destination.

```csharp
await call.ConnectAsync(new Dictionary<string, object?>
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
                    ["timeout"]     = 30,
                },
            },
        },
    },
    ["ringback"] = new[]
    {
        new Dictionary<string, object>
        {
            ["type"]   = "tts",
            ["params"] = new Dictionary<string, object> { ["text"] = "Please wait." },
        },
    },
});
```

## Detect

Run detection on the call (machine, fax, digit). The wire payload nests the
`{type, params}` object in a `detect` envelope (the typed conveniences
`DetectDigit` / `DetectAnsweringMachine` / `DetectFax` build it for you):

```csharp
var action = call.Detect(new Dictionary<string, object?>
{
    ["detect"] = new Dictionary<string, object?>
    {
        ["type"]   = "machine",
        ["params"] = new Dictionary<string, object>
        {
            ["initial_timeout"]     = 4.0,
            ["end_silence_timeout"] = 2.0,
        },
    },
});

var result = await action.WaitAsync();
```

## Tap

Start a media tap (real-time audio stream). The wire payload takes TWO
envelopes: `tap` (what to tap) and `device` (where to send it):

```csharp
var action = call.Tap(new Dictionary<string, object?>
{
    ["tap"] = new Dictionary<string, object?>
    {
        ["type"]   = "audio",
        ["params"] = new Dictionary<string, object>
        {
            ["direction"] = "both",
        },
    },
    ["device"] = new Dictionary<string, object?>
    {
        ["type"]   = "rtp",
        ["params"] = new Dictionary<string, object>
        {
            ["addr"]  = "192.168.1.100",
            ["port"]  = 9000,
            ["codec"] = "PCMU",
        },
    },
});
```

## Send Digits

```csharp
await call.SendDigitsAsync(new Dictionary<string, object?> { ["digits"] = "1234#" });
```

## Action Control

Actions returned by Play, Record, Detect, etc. support a single async method,
`WaitAsync()`; stop/pause/resume are synchronous (they fire-and-return a
sub-command RPC). Pause/Resume exist only on `PlayAction` and `RecordAction`.

```csharp
var action = call.PlayTts("Hello!");

await action.WaitAsync();        // Wait for completion (default 30s)
await action.WaitAsync(15);      // Wait with timeout (seconds)
action.Stop();                   // Stop the action
action.Pause();                  // Pause (PlayAction / RecordAction)
action.Resume();                 // Resume (PlayAction / RecordAction)
```

## Call Properties

| Property | Type | Description |
|----------|------|-------------|
| `CallId` | `string` | Unique call identifier |
| `NodeId` | `string` | Node handling the call |
| `Tag` | `string` | Client-generated correlation tag |
| `State` | `string` | Current call state |
| `DialWinner` | `bool` | True if this leg won the dial |
| `Device` | `Dictionary` | Device info (type, params) |
