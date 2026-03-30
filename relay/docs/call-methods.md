# Call Methods Reference (.NET)

## Overview

The `Call` object represents a live phone call. It provides async methods for every calling operation.

## Answer and Hangup

```csharp
await call.AnswerAsync();
await call.HangupAsync();
await call.HangupAsync(reason: "busy");
```

## Play

Play audio or TTS to the call.

```csharp
// TTS
var action = await call.PlayAsync(media: new[]
{
    new Dictionary<string, object>
    {
        ["type"]   = "tts",
        ["params"] = new Dictionary<string, object> { ["text"] = "Hello!" },
    },
});
await action.WaitAsync();

// Audio file
var action = await call.PlayAsync(media: new[]
{
    new Dictionary<string, object>
    {
        ["type"]   = "audio",
        ["params"] = new Dictionary<string, object> { ["url"] = "https://example.com/audio.mp3" },
    },
});
await action.WaitAsync();

// Silence
var action = await call.PlayAsync(media: new[]
{
    new Dictionary<string, object>
    {
        ["type"]   = "silence",
        ["params"] = new Dictionary<string, object> { ["duration"] = 2 },
    },
});
```

## Record

```csharp
var action = await call.RecordAsync(new Dictionary<string, object>
{
    ["beep"]        = true,
    ["format"]      = "wav",
    ["stereo"]      = false,
    ["direction"]   = "both",
    ["end_silence"] = 5,
});

var result = await action.WaitAsync();
// result contains recording URL
```

## Play and Collect (DTMF/Speech)

```csharp
var action = await call.PlayAndCollectAsync(
    media: new[]
    {
        new Dictionary<string, object>
        {
            ["type"]   = "tts",
            ["params"] = new Dictionary<string, object> { ["text"] = "Press 1 for sales, 2 for support." },
        },
    },
    collect: new Dictionary<string, object>
    {
        ["digits"] = new Dictionary<string, object>
        {
            ["max"]           = 1,
            ["digit_timeout"] = 5.0,
        },
        ["initial_timeout"] = 10.0,
    }
);

var result = await action.WaitAsync();
```

## Connect

Bridge the call to another destination.

```csharp
await call.ConnectAsync(
    devices: new List<List<Dictionary<string, object>>>
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
    ringback: new[]
    {
        new Dictionary<string, object>
        {
            ["type"]   = "tts",
            ["params"] = new Dictionary<string, object> { ["text"] = "Please wait." },
        },
    }
);
```

## Detect

Run detection on the call (machine, fax, digit).

```csharp
var action = await call.DetectAsync(new Dictionary<string, object>
{
    ["type"]   = "machine",
    ["params"] = new Dictionary<string, object>
    {
        ["initial_timeout"]    = 4.0,
        ["end_silence_timeout"] = 2.0,
    },
});

var result = await action.WaitAsync();
```

## Tap

Start a media tap (real-time audio stream).

```csharp
var action = await call.TapAsync(new Dictionary<string, object>
{
    ["type"]   = "audio",
    ["params"] = new Dictionary<string, object>
    {
        ["direction"] = "both",
        ["codec"]     = "PCMU",
        ["rtp"]       = new Dictionary<string, object>
        {
            ["addr"] = "192.168.1.100",
            ["port"] = 9000,
        },
    },
});
```

## Send Digits

```csharp
await call.SendDigitsAsync("1234#");
```

## Action Control

Actions returned by Play, Record, Detect, etc. support:

```csharp
var action = await call.PlayAsync(media: ttsMedia);

await action.WaitAsync();        // Wait for completion
await action.WaitAsync(15);      // Wait with timeout (seconds)
await action.StopAsync();        // Stop the action
await action.PauseAsync();       // Pause (play only)
await action.ResumeAsync();      // Resume (play only)
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
