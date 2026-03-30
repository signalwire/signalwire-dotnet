# Fabric Resources (.NET)

## Overview

The Fabric namespace manages AI agents, SWML scripts, subscribers, call flows, SIP endpoints, cXML resources, and more. Each sub-resource supports standard CRUD operations.

## AI Agents

```csharp
// Create
var agent = client.Fabric.AiAgents.Create(
    name:   "Support Bot",
    prompt: new Dictionary<string, object> { ["text"] = "You are a helpful support agent." }
);
var agentId = agent["id"].ToString();

// List
var agents = client.Fabric.AiAgents.List();

// Get
var details = client.Fabric.AiAgents.Get(agentId);

// Update
client.Fabric.AiAgents.Update(agentId, new Dictionary<string, object>
{
    ["name"] = "Updated Bot",
});

// Delete
client.Fabric.AiAgents.Delete(agentId);
```

## SWML Scripts

```csharp
// Create
var script = client.Fabric.SwmlScripts.Create(new Dictionary<string, object>
{
    ["name"]    = "greeting",
    ["content"] = new Dictionary<string, object>
    {
        ["version"]  = "1.0.0",
        ["sections"] = new Dictionary<string, object>
        {
            ["main"] = new List<Dictionary<string, object>>
            {
                new() { ["answer"] = new Dictionary<string, object>() },
                new() { ["play"] = new Dictionary<string, object> { ["url"] = "say:Hello!" } },
                new() { ["hangup"] = new Dictionary<string, object>() },
            },
        },
    },
});

// List
var scripts = client.Fabric.SwmlScripts.List();
```

## Subscribers

```csharp
// Create a SIP subscriber
var subscriber = client.Fabric.Subscribers.Create(new Dictionary<string, object>
{
    ["display_name"] = "Alice Smith",
    ["type"]         = "sip",
    ["email"]        = "alice@example.com",
    ["password"]     = "secure-password",
});

// List
var subscribers = client.Fabric.Subscribers.List();
```

## Call Flows

```csharp
// Create
var flow = client.Fabric.CallFlows.Create(new Dictionary<string, object>
{
    ["name"]    = "main-ivr",
    ["content"] = new Dictionary<string, object>
    {
        ["version"]  = "1.0.0",
        ["sections"] = new Dictionary<string, object>
        {
            ["main"] = new List<Dictionary<string, object>>
            {
                new() { ["answer"] = new Dictionary<string, object>() },
                new() { ["ai"] = new Dictionary<string, object>
                    {
                        ["prompt"] = new Dictionary<string, object> { ["text"] = "You are helpful." },
                    }
                },
            },
        },
    },
});

// List
var flows = client.Fabric.CallFlows.List();
```

## SIP Endpoints

```csharp
// Create
var endpoint = client.Fabric.SipEndpoints.Create(new Dictionary<string, object>
{
    ["username"]     = "alice",
    ["password"]     = "secure-password",
    ["display_name"] = "Alice Smith",
    ["caller_id"]    = "+15551234567",
});

// List
var endpoints = client.Fabric.SipEndpoints.List();
```

## cXML Resources

```csharp
var cxml = client.Fabric.CxmlResources.Create(new Dictionary<string, object>
{
    ["name"] = "conference-handler",
    ["body"] = "<Response><Dial><Conference>room-1</Conference></Dial></Response>",
});
```

## Generic Resources

```csharp
var resource = client.Fabric.Resources.Create(new Dictionary<string, object>
{
    ["name"]    = "custom-handler",
    ["type"]    = "swml_script",
    ["content"] = swmlContent,
});
```

## Addresses

Fabric addresses map phone numbers to resources:

```csharp
var address = client.Fabric.Addresses.Create(new Dictionary<string, object>
{
    ["name"]        = "Main Number",
    ["type"]        = "phone_number",
    ["resource_id"] = resourceId,
    ["channels"]    = new Dictionary<string, object>
    {
        ["voice"] = new Dictionary<string, object> { ["resource_id"] = agentId },
    },
});
```

## Tokens

Generate authentication tokens for subscribers:

```csharp
var token = client.Fabric.Tokens.Create(new Dictionary<string, object>
{
    ["subscriber_id"] = subscriberId,
    ["ttl"]           = 3600,
});
```
