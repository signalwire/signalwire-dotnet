# REST Client Reference (.NET)

<!-- snippet-setup -->
```csharp
using SignalWire.REST;
using System.Collections.Generic;
using System.Threading.Tasks;
// Context for the illustrative FRAGMENTS below (Error Handling / HTTP Client).
// The Constructor snippet opens with `using System;` so it is a self-contained
// unit and this preamble is NOT prepended to it.
RestClient client = new RestClient("project", "token", "example.signalwire.com");
```

## Constructor

```csharp
using System;
using SignalWire.REST;

var client = new RestClient(
    projectId: "your-project-id",
    token:     "your-api-token",
    space:     "example.signalwire.com"
);
```

All parameters fall back to environment variables if not provided:

| Parameter | Environment Variable |
|-----------|---------------------|
| `projectId` | `SIGNALWIRE_PROJECT_ID` |
| `token` | `SIGNALWIRE_API_TOKEN` |
| `space` | `SIGNALWIRE_SPACE` |

Find your Project ID, API token, and Space URL under **API** in your
[SignalWire dashboard](https://signalwire.com/signin).

## Request options (timeout & retries)

Every REST verb accepts an optional `requestOptions:` argument (a
`RequestOptions` record) that overrides transport behavior for that one call —
`Timeout` (seconds, per attempt), `Retries`, `RetryOnStatus`, and `RetryBackoff`.
A `RequestOptions` passed to the `RestClient` constructor becomes the
client-default envelope; a per-call `requestOptions:` shallow-overrides it.

```csharp
// Client-default: 5s per-attempt timeout, 2 retries on transient failures.
var tunedClient = new RestClient(
    projectId: "your-project-id",
    token:     "your-api-token",
    space:     "example.signalwire.com",
    requestOptions: new RequestOptions { Timeout = 5.0, Retries = 2 });

// Per-call override: give this one request a longer 30s budget.
await tunedClient.Http.GetAsync(
    "/api/relay/rest/phone_numbers",
    requestOptions: new RequestOptions { Timeout = 30.0 });
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `ProjectId` | `string` | Project ID |
| `Token` | `string` | API token |
| `Space` | `string` | Space hostname |
| `BaseUrl` | `string` | Computed base URL (`https://{space}`) |
| `Http` | `HttpClient` | Underlying HTTP client |

## Namespace Accessors

All namespace accessors are lazily initialized on first access.

| Accessor | Type | Description |
|----------|------|-------------|
| `Fabric` | `Fabric` | Fabric API (AI agents, SWML, subscribers, etc.) |
| `Calling` | `Calling` | REST-based call control |
| `PhoneNumbers` | `CrudResource` | Phone number management |
| `Datasphere` | `CrudResource` | Document management and search |
| `Video` | `CrudResource` | Video rooms |
| `Addresses` | `CrudResource` | Address management |
| `Queues` | `CrudResource` | Call queues |
| `Recordings` | `CrudResource` | Call recordings |
| `NumberGroups` | `CrudResource` | Number groups |
| `VerifiedCallers` | `CrudResource` | Verified callers |
| `SipProfile` | `CrudResource` | SIP profiles |
| `Lookup` | `CrudResource` | Phone number lookup |
| `ShortCodes` | `CrudResource` | Short codes |
| `ImportedNumbers` | `CrudResource` | Imported phone numbers |
| `Mfa` | `CrudResource` | Multi-factor authentication |
| `Registry` | `CrudResource` | 10DLC registry |
| `Logs` | `CrudResource` | Message/voice/fax/conference logs |
| `Project` | `CrudResource` | Project management |
| `Pubsub` | `CrudResource` | PubSub tokens |
| `Chat` | `CrudResource` | Chat tokens |

## CrudResource Methods

All `CrudResource` instances expose asynchronous methods returning
`Task<Dictionary<string, object?>>`:

| Method | Description |
|--------|-------------|
| `ListAsync(queryParams?)` | List resources (`Dictionary<string, string>` query params) |
| `GetAsync(id)` | Get a single resource |
| `CreateAsync(data)` | Create a new resource (`Dictionary<string, object?>` body) |
| `UpdateAsync(id, data)` | Update a resource |
| `DeleteAsync(id)` | Delete a resource |
| `SearchAsync(...)` | Search (where supported) |

## Error Handling

```csharp
try
{
    var result = await client.PhoneNumbers.ListAsync();
}
catch (ArgumentException ex)
{
    // Missing or invalid credentials (thrown by the RestClient constructor)
    Console.WriteLine($"Config error: {ex.Message}");
}
catch (SignalWireRestError ex)
{
    // HTTP or API error
    Console.WriteLine($"REST error: {ex.Message}");
}
```

## HTTP Client

The underlying `HttpClient` handles authentication, JSON serialization, and error handling:

```csharp
// Direct HTTP access for custom endpoints
var response = await client.Http.GetAsync("/api/custom/endpoint");
var result = await client.Http.PostAsync("/api/custom/endpoint", new Dictionary<string, object?>
{
    ["key"] = "value",
});
```

## Endpoint override

`RestClient` composes the base URL as `https://{space}`. To point the REST
transport at a different endpoint — a staging cluster, a proxy, or a local mock
server — construct the low-level `HttpClient` directly with an explicit
`baseUrl` (the `string baseUrl` constructor parameter is the override seam):

```csharp
// Full base URL, scheme included. Use http:// for a local mock/dev server and
// https:// for a real endpoint — the value is used verbatim.
var overrideHttp = new SignalWire.REST.HttpClient(
    "your-project-id", "your-api-token", "http://127.0.0.1:8080");
```

The generated namespace resources build on any `HttpClient`, so this override
carries through the whole REST surface.

## Custom CA bundle (TLS)

To trust a custom CA bundle for the REST transport's TLS verification (a private
platform cert or a mock server's throwaway CA), set the fleet-canonical env var
before constructing the client:

| Env var | Applies to |
|---------|------------|
| `SIGNALWIRE_REST_CA_FILE` | REST transport (this client) |
| `SIGNALWIRE_RELAY_CA_FILE` | RELAY WebSocket transport |

When set, the SDK-owned transport trusts that bundle as its TLS root. Unset, the
default OS trust store applies. A caller-injected `HttpClient` keeps its own TLS
configuration untouched.
