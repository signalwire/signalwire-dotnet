# Third-Party Skills (.NET)

## Overview

The skills system supports custom third-party skills. You can create skills for any external service and distribute them as NuGet packages or shared libraries.

## Creating a Third-Party Skill

### 1. Implement SkillBase

```csharp
using SignalWire.Skills;
using SignalWire.Agent;
using SignalWire.SWAIG;

public class CrmLookupSkill : SkillBase
{
    public override string Name => "crm_lookup";
    public override string Description => "Look up customers in your CRM";
    public override List<string> RequiredEnvVars => new() { "CRM_API_KEY", "CRM_BASE_URL" };

    private string _apiKey = "";
    private string _baseUrl = "";

    public override void Setup(AgentBase agent, Dictionary<string, object>? parameters)
    {
        _apiKey  = parameters?.GetValueOrDefault("api_key")?.ToString()
                   ?? Environment.GetEnvironmentVariable("CRM_API_KEY") ?? "";
        _baseUrl = parameters?.GetValueOrDefault("base_url")?.ToString()
                   ?? Environment.GetEnvironmentVariable("CRM_BASE_URL") ?? "";

        agent.AddHints(new List<string> { "customer", "CRM", "account" });
        agent.PromptAddSection("CRM Access",
            "You can look up customer information using the crm_lookup tool.");
    }

    public override void RegisterTools(AgentBase agent, Dictionary<string, object>? parameters)
    {
        var apiKey  = _apiKey;
        var baseUrl = _baseUrl;

        agent.DefineTool(
            name:        "crm_lookup",
            description: "Look up a customer by name or email",
            parameters:  new Dictionary<string, object>
            {
                ["query"] = new Dictionary<string, object>
                {
                    ["type"]        = "string",
                    ["description"] = "Customer name or email to search for",
                },
            },
            handler: (args, rawData) =>
            {
                var query = args.GetValueOrDefault("query")?.ToString() ?? "";
                // In production, call the CRM API here
                return new FunctionResult($"Found customer: {query} (Premium tier, active)");
            }
        );
    }
}
```

### 2. Register the Skill

```csharp
using SignalWire.Skills;

// Register at startup
SkillRegistry.Register("crm_lookup", () => new CrmLookupSkill());
```

### 3. Use in an Agent

```csharp
var agent = new AgentBase(new AgentOptions { Name = "support" });

agent.AddSkill("crm_lookup", new Dictionary<string, object>
{
    ["api_key"]  = "my-crm-key",
    ["base_url"] = "https://crm.example.com/api",
});
```

## Skill Lifecycle

1. **Registration** -- `SkillRegistry.Register()` maps a name to a factory
2. **Loading** -- `agent.AddSkill()` creates an instance and calls `Setup()` and `RegisterTools()`
3. **Validation** -- Required env vars are checked at load time
4. **Unloading** -- `agent.RemoveSkill()` removes all registered tools and data

## Best Practices

- **Validate parameters early** -- Check required config in `Setup()` and throw clear errors
- **Use environment variables** -- Support both parameter-based and env-var-based configuration
- **Register hints** -- Add speech recognition hints for domain-specific terminology
- **Add prompt sections** -- Tell the AI when and how to use your skill's tools
- **Keep tools focused** -- Each tool should do one thing well

## Distribution

Package your skill as a NuGet package:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PackageId>MyCompany.SignalWire.Skills.CrmLookup</PackageId>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SignalWire" Version="*" />
  </ItemGroup>
</Project>
```

Users install and register:

```bash
dotnet add package MyCompany.SignalWire.Skills.CrmLookup
```

```csharp
SkillRegistry.Register("crm_lookup", () => new CrmLookupSkill());
agent.AddSkill("crm_lookup");
```
