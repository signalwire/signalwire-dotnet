# Fabric Resources (.NET)

## Overview

The Fabric namespace manages AI agents, SWML scripts, subscribers, call flows, SIP endpoints, cXML resources, and more. Each sub-resource exposes the async CRUD methods (`ListAsync`/`GetAsync`/`CreateAsync`/`UpdateAsync`/`DeleteAsync`).

## AI Agents

```csharp
// Create
var agent = await client.Fabric.AiAgents.CreateAsync(new Dictionary<string, object?>
{
    ["name"]   = "Support Bot",
    ["prompt"] = new Dictionary<string, object> { ["text"] = "You are a helpful support agent." },
});
var agentId = agent["id"].ToString();

// List
var agents = await client.Fabric.AiAgents.ListAsync();

// Get
var details = await client.Fabric.AiAgents.GetAsync(agentId);

// Update
await client.Fabric.AiAgents.UpdateAsync(agentId, new Dictionary<string, object?>
{
    ["name"] = "Updated Bot",
});

// Delete
await client.Fabric.AiAgents.DeleteAsync(agentId);
```

## SWML Scripts

```csharp
// Create
var script = await client.Fabric.SwmlScripts.CreateAsync(new Dictionary<string, object?>
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
var scripts = await client.Fabric.SwmlScripts.ListAsync();
```

## Subscribers

```csharp
// Create a SIP subscriber
var subscriber = await client.Fabric.Subscribers.CreateAsync(new Dictionary<string, object?>
{
    ["display_name"] = "Alice Smith",
    ["type"]         = "sip",
    ["email"]        = "alice@example.com",
    ["password"]     = "secure-password",
});

// List
var subscribers = await client.Fabric.Subscribers.ListAsync();
```

## Call Flows

```csharp
// Create
var flow = await client.Fabric.CallFlows.CreateAsync(new Dictionary<string, object?>
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
var flows = await client.Fabric.CallFlows.ListAsync();
```

## SIP Endpoints

```csharp
// Create
var endpoint = await client.Fabric.SipEndpoints.CreateAsync(new Dictionary<string, object?>
{
    ["username"]     = "alice",
    ["password"]     = "secure-password",
    ["display_name"] = "Alice Smith",
    ["caller_id"]    = "+15551234567",
});

// List
var endpoints = await client.Fabric.SipEndpoints.ListAsync();
```

## cXML Resources

cXML is exposed as `CxmlScripts` (inline scripts), `CxmlApplications`, and
`CxmlWebhooks`:

```csharp
var cxml = await client.Fabric.CxmlScripts.CreateAsync(new Dictionary<string, object?>
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
var resources = await client.Fabric.ResourcesGeneric.ListAsync();
var resource  = await client.Fabric.ResourcesGeneric.GetAsync(resourceId);
var addrs     = await client.Fabric.ResourcesGeneric.ListAddressesAsync(resourceId);
```

## Addresses

Top-level Fabric addresses are read-only (list/get); a resource's addresses are
created by binding a phone number (the server auto-materializes them):

```csharp
var addresses = await client.Fabric.AddressesTopLevel.ListAsync();
var address   = await client.Fabric.AddressesTopLevel.GetAsync(addressId);
```

## Tokens

Generate authentication tokens for subscribers:

```csharp
var token = await client.Fabric.TokensApi.CreateSubscriberTokenAsync(new Dictionary<string, object?>
{
    ["subscriber_id"] = subscriberId,
    ["ttl"]           = 3600,
});
```
