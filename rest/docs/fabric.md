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

The generic ``ResourcesGeneric`` accessor lists/gets/deletes any resource type
and lists its addresses (there is no generic create — create a typed resource,
e.g. ``SwmlScripts``/``CallFlows``):

```csharp
var resources = client.Fabric.ResourcesGeneric.List();
var resource  = client.Fabric.ResourcesGeneric.Get(resourceId);
var addrs     = client.Fabric.ResourcesGeneric.ListAddresses(resourceId);
```

## Addresses

Top-level Fabric addresses are read-only (list/get); a resource's addresses are
created by binding a phone number (the server auto-materializes them):

```csharp
var addresses = client.Fabric.AddressesTopLevel.List();
var address   = client.Fabric.AddressesTopLevel.Get(addressId);
```

## Tokens

Generate authentication tokens for subscribers:

```csharp
var token = client.Fabric.TokensApi.CreateSubscriberToken(new Dictionary<string, object>
{
    ["subscriber_id"] = subscriberId,
    ["ttl"]           = 3600,
});
```
