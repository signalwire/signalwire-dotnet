# REST Client Reference (.NET)

## Constructor

```csharp
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
| `Compat` | `CrudResource` | Twilio-compatible LAML |
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

All `CrudResource` instances support:

| Method | Description |
|--------|-------------|
| `List()` | List all resources |
| `Get(id)` | Get a single resource |
| `Create(data)` | Create a new resource |
| `Update(id, data)` | Update a resource |
| `Delete(id)` | Delete a resource |
| `Search(...)` | Search (where supported) |

## Error Handling

```csharp
try
{
    var result = client.PhoneNumbers.List();
}
catch (ArgumentException ex)
{
    // Missing or invalid credentials
    Console.WriteLine($"Config error: {ex.Message}");
}
catch (Exception ex)
{
    // HTTP or API error
    Console.WriteLine($"REST error: {ex.Message}");
}
```

## HTTP Client

The underlying `HttpClient` handles authentication, JSON serialization, and error handling:

```csharp
// Direct HTTP access for custom endpoints
var response = client.Http.Get("/api/custom/endpoint");
var result = client.Http.Post("/api/custom/endpoint", new Dictionary<string, object>
{
    ["key"] = "value",
});
```
