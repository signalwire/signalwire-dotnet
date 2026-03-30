# DataMap Guide (.NET)

## Overview

DataMap provides declarative SWAIG tools that execute on SignalWire's servers, without requiring your own webhook infrastructure. DataMap tools can call external APIs, match expressions, and return structured responses.

## Quick Start

```csharp
using SignalWire.DataMap;
using SignalWire.SWAIG;

var weather = new DataMap("get_weather")
    .Description("Get weather for a location")
    .Parameter("location", "string", "City name", required: true)
    .Webhook("GET", "https://api.weather.com/v1/current?q=${args.location}")
    .Output(new FunctionResult("Weather: ${response.current.condition.text}"));

agent.RegisterSwaigFunction(weather.ToSwaigFunction());
```

## Builder Methods

### DataMap(name)

Create a new DataMap with the given function name.

```csharp
var dm = new DataMap("my_function");
```

### Description() / Purpose()

Set the function description (shown to the AI).

```csharp
dm.Description("Look up customer information");
dm.Purpose("Look up customer information");  // alias
```

### Parameter()

Add a parameter to the function.

```csharp
dm.Parameter("name", "string", "Customer name", required: true);
dm.Parameter("format", "string", "Output format",
    enumValues: new List<string> { "json", "text", "csv" });
```

### Webhook()

Add a webhook call. The DataMap will make this HTTP request when invoked.

```csharp
dm.Webhook("GET", "https://api.example.com/lookup?name=${args.name}");

// With headers
dm.Webhook("POST", "https://api.example.com/data",
    headers: new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer ${meta.api_key}",
        ["Content-Type"]  = "application/json",
    });
```

### Expression()

Add an expression-based pattern match. Useful for local logic without API calls.

```csharp
dm.Expression(
    "${args.command}",
    "play|resume",
    new FunctionResult("Playback started"),
    nomatchOutput: new FunctionResult("Playback stopped")
);
```

### Output()

Set the global output template for webhook responses.

```csharp
dm.Output(new FunctionResult("Result: ${response.data.value}"));
```

### ErrorKeys()

Specify response keys that indicate an error.

```csharp
dm.ErrorKeys(new List<string> { "error", "error_message" });
```

### ToSwaigFunction()

Convert the DataMap to a SWAIG function definition dictionary.

```csharp
var funcDef = dm.ToSwaigFunction();
agent.RegisterSwaigFunction(funcDef);
```

## Examples

### API Lookup

```csharp
var lookup = new DataMap("lookup_customer")
    .Description("Look up a customer by email")
    .Parameter("email", "string", "Customer email address", required: true)
    .Webhook("GET", "https://crm.example.com/api/customers?email=${args.email}")
    .Output(new FunctionResult(
        "Customer: ${response.name}, Plan: ${response.plan}, Status: ${response.status}"))
    .ErrorKeys(new List<string> { "error" });

agent.RegisterSwaigFunction(lookup.ToSwaigFunction());
```

### Expression Matching

```csharp
var control = new DataMap("media_control")
    .Description("Control audio playback")
    .Parameter("command", "string", "Playback command", required: true,
        enumValues: new List<string> { "play", "pause", "stop", "next", "previous" })
    .Expression(
        "${args.command}",
        "play|resume",
        new FunctionResult("Playback started"),
        nomatchOutput: new FunctionResult("Playback stopped")
    );

agent.RegisterSwaigFunction(control.ToSwaigFunction());
```

### Combining with Regular Tools

You can mix DataMap tools (server-side) with regular SWAIG tools (webhook-based) on the same agent:

```csharp
// DataMap tool (runs on SignalWire servers)
var weather = new DataMap("get_weather")
    .Description("Get weather for a location")
    .Parameter("location", "string", "City name", required: true)
    .Webhook("GET", "https://api.weather.com/v1/current?q=${args.location}")
    .Output(new FunctionResult("Weather: ${response.current.condition.text}"));

agent.RegisterSwaigFunction(weather.ToSwaigFunction());

// Regular SWAIG tool (runs on your server)
agent.DefineTool(
    name:        "echo_test",
    description: "Echo a message back",
    parameters:  new Dictionary<string, object>
    {
        ["message"] = new Dictionary<string, object>
        {
            ["type"] = "string", ["description"] = "Message to echo",
        },
    },
    handler: (args, raw) =>
    {
        var msg = args.GetValueOrDefault("message")?.ToString() ?? "nothing";
        return new FunctionResult($"Echo: {msg}");
    }
);
```

## Template Variables

DataMap templates support variable interpolation:

| Variable | Description |
|----------|-------------|
| `${args.param_name}` | Function argument values |
| `${response.field}` | Webhook response fields |
| `${response.nested.field}` | Nested response fields |
| `${meta.key}` | Metadata values |
