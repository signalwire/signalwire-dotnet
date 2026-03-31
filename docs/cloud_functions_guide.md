# SignalWire AI Agents - Cloud Functions Deployment Guide

This guide covers deploying SignalWire AI Agents to serverless platforms from .NET.

## Overview

SignalWire AI Agents support deployment to major cloud function platforms:

- **Azure Functions** - Serverless compute service on Microsoft Azure
- **AWS Lambda** - Serverless compute on Amazon Web Services
- **Google Cloud Functions** - Serverless compute on Google Cloud

## Azure Functions

### Environment Detection

The agent automatically detects Azure Functions environment using these variables:
- `AZURE_FUNCTIONS_ENVIRONMENT` - Azure Functions runtime environment
- `FUNCTIONS_WORKER_RUNTIME` - Runtime language
- `AzureWebJobsStorage` - Azure storage connection string

### Deployment Steps

1. **Create your function project**:
```bash
func init MyAgentFunction --dotnet
cd MyAgentFunction
func new --name AgentHandler --template "HttpTrigger"
```

2. **Add the SignalWire SDK reference**:
```xml
<PackageReference Include="SignalWire.Agents" Version="*" />
```

3. **Create the function** (`AgentHandler.cs`):
```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SignalWire.Agent;

public class AgentHandler
{
    private readonly AgentBase _agent;

    public AgentHandler()
    {
        _agent = new AgentBase(new AgentOptions
        {
            Name  = "azure-agent",
            Route = "/",
        });

        _agent.PromptAddSection("Role", "You are a helpful assistant running on Azure Functions.");
        _agent.AddLanguage("English", "en-US", "inworld.Mark");
        _agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });
    }

    [Function("AgentHandler")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    {
        return await _agent.HandleServerlessRequestAsync(req);
    }
}
```

4. **Deploy using Azure CLI**:
```bash
az functionapp create \
    --resource-group myResourceGroup \
    --consumption-plan-location westus \
    --runtime dotnet-isolated \
    --runtime-version 8.0 \
    --functions-version 4 \
    --name my-agent-function \
    --storage-account mystorageaccount

func azure functionapp publish my-agent-function
```

### Environment Variables

Set these in your Azure Function App settings:

```bash
SIGNALWIRE_PROJECT_ID="your-project-id"
SIGNALWIRE_TOKEN="your-token"
AGENT_USERNAME="your-username"
AGENT_PASSWORD="your-password"
```

### URL Format

```
https://{function-app-name}.azurewebsites.net/api/{function-name}
```

With authentication:
```
https://username:password@{function-app-name}.azurewebsites.net/api/{function-name}
```

## AWS Lambda

### Deployment Steps

1. **Create a Lambda project**:
```bash
dotnet new lambda.EmptyFunction --name MyAgentLambda
cd MyAgentLambda/src/MyAgentLambda
dotnet add package SignalWire.Agents
dotnet add package Amazon.Lambda.AspNetCoreServer
```

2. **Create your handler** (`Function.cs`):
```csharp
using Amazon.Lambda.AspNetCoreServer;
using SignalWire.Agent;

public class LambdaFunction : APIGatewayProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name  = "lambda-agent",
            Route = "/",
        });

        agent.PromptAddSection("Role", "You are a helpful assistant running on AWS Lambda.");
        agent.AddLanguage("English", "en-US", "inworld.Mark");
        agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

        builder.Configure(app => agent.ConfigureLambda(app));
    }
}
```

3. **Deploy**:
```bash
dotnet lambda deploy-function MyAgentLambda \
    --function-role arn:aws:iam::123456789:role/lambda-role
```

### Environment Variables

```bash
export SIGNALWIRE_PROJECT_ID="your-project-id"
export SIGNALWIRE_TOKEN="your-token"
export SWML_BASIC_AUTH_USER="dev"
export SWML_BASIC_AUTH_PASSWORD="w00t"
```

## Google Cloud Functions

### Deployment Steps

1. **Create your function project**:
```bash
dotnet new web -n MyAgentFunction
cd MyAgentFunction
dotnet add package SignalWire.Agents
dotnet add package Google.Cloud.Functions.Hosting
```

2. **Create your function** (`Function.cs`):
```csharp
using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using SignalWire.Agent;

public class AgentFunction : IHttpFunction
{
    private readonly AgentBase _agent;

    public AgentFunction()
    {
        _agent = new AgentBase(new AgentOptions
        {
            Name  = "gcp-agent",
            Route = "/",
        });

        _agent.PromptAddSection("Role", "You are a helpful assistant on Google Cloud.");
        _agent.AddLanguage("English", "en-US", "inworld.Mark");
        _agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });
    }

    public async Task HandleAsync(HttpContext context)
    {
        await _agent.HandleServerlessRequestAsync(context);
    }
}
```

3. **Deploy**:
```bash
gcloud functions deploy my-agent \
    --runtime dotnet8 \
    --trigger-http \
    --entry-point AgentFunction \
    --allow-unauthenticated
```

### URL Format

```
https://{region}-{project-id}.cloudfunctions.net/{function-name}
```

## Authentication

All platforms support HTTP Basic Authentication:

```csharp
var agent = new AgentBase(new AgentOptions
{
    Name     = "my-agent",
    Username = "your-username",
    Password = "your-password",
});
```

### Authentication Flow
1. Client sends request with `Authorization: Basic <credentials>` header
2. Agent validates credentials against configured username/password
3. If invalid, returns 401 with `WWW-Authenticate` header
4. If valid, processes the request normally

## Testing

### Local Testing

```bash
# Azure Functions
func start

# AWS Lambda (local invoke)
dotnet lambda invoke-function MyAgentLambda --payload '{"httpMethod":"GET","path":"/"}'
```

### Testing Authentication

```bash
# Test without auth (should return 401)
curl https://your-function-url/

# Test with valid auth
curl -u username:password https://your-function-url/

# Test SWAIG function call
curl -u username:password \
  -H "Content-Type: application/json" \
  -d '{"call_id": "test", "argument": {"parsed": [{"param": "value"}]}}' \
  https://your-function-url/your_function_name
```

## Best Practices

### Performance
- Use connection pooling for database connections
- Implement proper caching strategies
- Minimize cold start times with smaller deployment packages

### Security
- Always use HTTPS endpoints
- Implement proper authentication
- Use environment variables for sensitive data
- Consider using cloud-native secret management (Azure Key Vault, AWS Secrets Manager)

### Monitoring
- Enable cloud platform logging
- Monitor function execution times
- Set up alerts for errors and timeouts
- Use distributed tracing for complex workflows

### Cost Optimization
- Right-size memory allocation
- Implement proper timeout settings
- Use reserved capacity for predictable workloads
- Monitor and optimize function execution patterns

## Troubleshooting

### Common Issues

**Environment Detection:**
```csharp
// Check detected mode
var mode = ExecutionMode.Detect();
Console.WriteLine($"Detected mode: {mode}");
```

**URL Generation:**
```csharp
var agent = new AgentBase(new AgentOptions { Name = "test" });
Console.WriteLine($"Base URL: {agent.GetFullUrl()}");
Console.WriteLine($"Auth URL: {agent.GetFullUrl(includeAuth: true)}");
```

**Authentication Issues:**
- Verify username/password are set correctly
- Check that Authorization header is being sent
- Ensure credentials match exactly (case-sensitive)

### Debugging

Enable debug logging:
```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
    builder.SetMinimumLevel(LogLevel.Debug));
```

## Migration from Other Platforms

### From Traditional Servers
- Add cloud function entry point
- Configure environment variables
- Update URL generation logic
- Test authentication flow
