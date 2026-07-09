# SWAIG Reference (.NET)

SWAIG (SignalWire AI Gateway) is the platform's AI tool-calling system. It connects the AI's decisions to actions like call transfers, SMS, recordings, and API calls, with native access to the media stack. This document covers the `FunctionResult` class and the SWAIG post_data format.

<!-- snippet-setup -->
```csharp
using SignalWire.SWAIG;
using System.Collections.Generic;
// Shared context for the action fragments below: a `result` FunctionResult.
FunctionResult result = new FunctionResult("ok");
```

## FunctionResult Methods

### Basic Construction

```csharp
using System;
using SignalWire.SWAIG;

var basic          = new FunctionResult("Hello, I'll help you with that");
var withPostProcess = new FunctionResult("Processing...", postProcess: true);
```

### Call Control Actions

#### Connect()

Transfer/connect call to another destination using SWML.

```csharp
result.Connect("+15551234567", final: true);
result.Connect("support@company.com", final: false, fromAddr: "+15559876543");
```

#### SendSms()

Send SMS message to a PSTN phone number.

```csharp
result.SendSms(
    toNumber:   "+15551234567",
    fromNumber: "+15559876543",
    body:       "Your order has been confirmed!"
);

// With media
result.SendSms(
    toNumber:   "+15551234567",
    fromNumber: "+15559876543",
    body:       "See attached receipt.",
    media:      new List<string> { "https://example.com/receipt.jpg" },
    tags:       new List<string> { "order", "confirmation" }
);
```

#### RecordCall()

Start background call recording.

```csharp
// The canonical overload takes the closed-set enums RecordFormat / RecordDirection.
// Pass one of the enum args (here `format:`) so the call resolves to it unambiguously.
result.RecordCall(format: RecordFormat.Wav);
result.RecordCall(controlId: "support_001", stereo: true, format: RecordFormat.Mp3);
```

#### StopRecordCall()

```csharp
result.StopRecordCall();
result.StopRecordCall("support_001");
```

#### Hangup()

```csharp
result.Hangup();
```

#### Hold()

```csharp
result.Hold(timeout: 30);          // 30 seconds
result.Hold(timeout: 300);         // 5 minutes (default)
```

#### WaitForUser()

```csharp
result.WaitForUser();                                    // Simple enable
result.WaitForUser(enabled: true, timeout: 30);          // With timeout
result.WaitForUser(answerFirst: true);                   // Answer before waiting
```

#### Stop()

```csharp
result.Stop();  // Stop the AI session
```

### State & Data Actions

#### UpdateGlobalData()

```csharp
result.UpdateGlobalData(new Dictionary<string, object>
{
    ["customer_name"] = "John",
    ["account_tier"]  = "premium",
});
```

#### RemoveGlobalData()

```csharp
result.RemoveGlobalData(new List<string> { "temp_data", "session_key" });
```

#### SetMetadata()

```csharp
result.SetMetadata(new Dictionary<string, object>
{
    ["call_type"]  = "support",
    ["priority"]   = "high",
});
```

#### RemoveMetadata()

```csharp
result.RemoveMetadata(new List<string> { "old_key" });
```

### Context & History Actions

#### SwitchContext()

```csharp
result.SwitchContext(
    systemPrompt: "You are now a billing specialist.",
    userPrompt:   "The customer needs help with billing.",
    consolidate:  true
);
```

#### SwmlChangeContext()

```csharp
result.SwmlChangeContext("billing");
```

#### SwmlChangeStep()

```csharp
result.SwmlChangeStep("needs_assessment");
```

#### ReplaceInHistory()

```csharp
result.ReplaceInHistory("The customer asked about pricing.");
result.ReplaceInHistory();   // No text → replace with the AI summary (defaults to true)
```

### Media Actions

#### Say()

```csharp
result.Say("Let me look that up for you.");
```

#### PlayBackgroundFile()

```csharp
result.PlayBackgroundFile("https://example.com/hold-music.mp3");
result.PlayBackgroundFile("https://example.com/notification.mp3", wait: true);
```

#### StopBackgroundFile()

```csharp
result.StopBackgroundFile();
```

### Speech & AI Actions

#### AddDynamicHints()

```csharp
result.AddDynamicHints(new List<object> { "new term", "product name" });
```

#### ClearDynamicHints()

```csharp
result.ClearDynamicHints();
```

#### SetEndOfSpeechTimeout()

```csharp
result.SetEndOfSpeechTimeout(1500);  // 1.5 seconds
```

#### ToggleFunctions()

```csharp
result.ToggleFunctions(new List<Dictionary<string, object>>
{
    new() { ["function"] = "transfer_call", ["active"] = false },
    new() { ["function"] = "lookup_order",  ["active"] = true },
});
```

#### UpdateSettings()

```csharp
result.UpdateSettings(new Dictionary<string, object>
{
    ["ai_model"] = "gpt-4.1-nano",
    ["temperature"] = 0.5,
});
```

### Advanced Actions

#### ExecuteSwml()

```csharp
result.ExecuteSwml(new Dictionary<string, object>
{
    ["sections"] = new Dictionary<string, object>
    {
        ["main"] = new List<Dictionary<string, object>>
        {
            new() { ["play"] = new Dictionary<string, object> { ["url"] = "say:Hello" } },
        },
    },
});
```

#### SwmlTransfer()

```csharp
result.SwmlTransfer("https://example.com/handler", aiResponse: "Transferring you now.");
```

#### JoinConference()

```csharp
result.JoinConference("support-queue", muted: false, beep: "true");
```

#### JoinRoom()

```csharp
result.JoinRoom("video-room-1");
```

#### SipRefer()

```csharp
result.SipRefer("sip:agent@pbx.example.com");
```

#### Tap()

```csharp
result.Tap("wss://listener.example.com/stream", direction: "both", codec: "PCMU");
```

#### Pay()

```csharp
result.Pay(
    paymentConnectorUrl: "https://payments.example.com/process",
    inputMethod:         "dtmf",
    timeout:             600,
    maxAttempts:         3
);
```

### RPC Actions

#### ExecuteRpc()

```csharp
result.ExecuteRpc("calling.dial", new Dictionary<string, object>
{
    ["to_number"]   = "+15551234567",
    ["from_number"] = "+15559876543",
});
```

#### RpcDial()

```csharp
result.RpcDial(
    toNumber:   "+15551234567",
    fromNumber: "+15559876543",
    destSwml:   "https://example.com/handler.swml");
```

#### SimulateUserInput()

```csharp
result.SimulateUserInput("I need help with my billing");
```

## Fluent Chaining

All methods return `this` for fluent chaining:

```csharp
using System.Collections.Generic;
using SignalWire.SWAIG;

var chained = new FunctionResult("Transferring to support.")
    .UpdateGlobalData(new Dictionary<string, object> { ["dept"] = "support" })
    .RecordCall(stereo: true, format: RecordFormat.Wav)
    .Connect("+15551234567");
```

## SWAIG Request Format

When SignalWire invokes a SWAIG function, it sends a POST request:

```json
{
    "function": "get_weather",
    "argument": {
        "parsed": [{"location": "Austin"}]
    },
    "call_id": "abc-123",
    "meta_data": {},
    "global_data": {}
}
```

## SWAIG Response Format

The handler returns a `FunctionResult` serialized via `ToDict()`:

```json
{
    "response": "It's sunny and 72F in Austin.",
    "action": [
        {"set_global_data": {"weather_location": "Austin"}}
    ]
}
```
