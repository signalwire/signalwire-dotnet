# MCP to SWAIG Gateway

## Overview

The MCP-SWAIG Gateway bridges Model Context Protocol (MCP) servers with SignalWire AI Gateway (SWAIG) functions, allowing SignalWire AI agents to interact with MCP-based tools. This gateway acts as a translation layer and session manager between the two protocols.

## Installation

The MCP Gateway skill is included in the SignalWire Agents SDK:

```bash
dotnet add package SignalWire.Agents
```

The gateway server itself is a standalone service. Install with:

```bash
pip install "signalwire-agents[mcp-gateway]"
```

Once installed, the `mcp-gateway` CLI command is available:

```bash
mcp-gateway -c config.json
```

## Architecture

### Components

1. **MCP Gateway Service** - HTTP/HTTPS server with Basic Authentication. Manages multiple MCP server instances, handles session lifecycle per SignalWire call, and translates between SWAIG and MCP protocols.

2. **MCP Gateway Skill** - SignalWire skill that connects agents to the gateway. Dynamically creates SWAIG functions from MCP tools and manages session lifecycle using `call_id`.

### Protocol Flow

```
SignalWire Agent                 Gateway Service              MCP Server
      |                                |                          |
      |---(1) Add Skill--------------->|                          |
      |<--(2) Query Tools--------------|                          |
      |                                |---(3) List Tools-------->|
      |                                |<--(4) Tool List----------|
      |---(5) Call SWAIG Function----->|                          |
      |                                |---(6) Spawn Session----->|
      |                                |---(7) Call MCP Tool----->|
      |                                |<--(8) MCP Response-------|
      |<--(9) SWAIG Response-----------|                          |
      |                                |                          |
      |---(10) Hangup Hook------------>|                          |
      |                                |---(11) Close Session---->|
```

## Message Envelope Format

The gateway uses a custom envelope format for routing and session management:

```json
{
    "session_id": "call_xyz123",
    "service": "todo",
    "tool": "add_todo",
    "arguments": {
        "text": "Buy milk"
    },
    "timeout": 300,
    "metadata": {
        "agent_id": "agent_123",
        "timestamp": "2024-01-20T10:30:00Z"
    }
}
```

## Configuration

### Gateway Configuration (`config.json`)

The configuration supports environment variable substitution using `${VAR_NAME|default}` syntax:

```json
{
    "server": {
        "host": "${MCP_HOST|0.0.0.0}",
        "port": "${MCP_PORT|8080}",
        "auth_user": "${MCP_AUTH_USER|admin}",
        "auth_password": "${MCP_AUTH_PASSWORD|changeme}",
        "auth_token": "${MCP_AUTH_TOKEN|optional-bearer-token}"
    },
    "services": {
        "todo": {
            "command": ["python3", "./test/todo_mcp.py"],
            "description": "Simple todo list for testing",
            "enabled": true,
            "sandbox": {
                "enabled": true,
                "resource_limits": true,
                "restricted_env": true
            }
        },
        "calculator": {
            "command": ["node", "/path/to/calculator.js"],
            "description": "Math calculations",
            "enabled": true,
            "sandbox": {
                "enabled": true,
                "resource_limits": true,
                "restricted_env": false
            }
        }
    },
    "session": {
        "default_timeout": 300,
        "max_sessions_per_service": 100,
        "cleanup_interval": 60,
        "sandbox_dir": "./sandbox"
    },
    "rate_limiting": {
        "default_limits": ["200 per day", "50 per hour"],
        "tools_limit": "30 per minute",
        "call_limit": "10 per minute",
        "session_delete_limit": "20 per minute",
        "storage_uri": "memory://"
    },
    "logging": {
        "level": "INFO",
        "file": "gateway.log"
    }
}
```

### Skill Configuration (C#)

```csharp
using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "MCP Agent",
    Route = "/mcp-agent",
});

agent.AddSkill("mcp_gateway", new Dictionary<string, object>
{
    ["gateway_url"]    = "https://localhost:8080",
    ["auth_user"]      = "admin",
    ["auth_password"]  = "changeme",
    ["services"]       = new List<Dictionary<string, object>>
    {
        new()
        {
            ["name"]  = "todo",
            ["tools"] = new List<string> { "add_todo", "list_todos" },
        },
        new()
        {
            ["name"]  = "calculator",
            ["tools"] = "*", // All tools
        },
    },
    ["session_timeout"]  = 300,
    ["tool_prefix"]      = "mcp_",
    ["retry_attempts"]   = 3,
    ["request_timeout"]  = 30,
    ["verify_ssl"]       = true,
});
```

### Environment Variable Substitution

Supported variables:
- `MCP_HOST`: Server bind address (default: 0.0.0.0)
- `MCP_PORT`: Server port (default: 8080)
- `MCP_AUTH_USER`: Basic auth username (default: admin)
- `MCP_AUTH_PASSWORD`: Basic auth password (default: changeme)
- `MCP_AUTH_TOKEN`: Bearer token for API access
- `MCP_SESSION_TIMEOUT`: Session timeout in seconds (default: 300)
- `MCP_MAX_SESSIONS`: Max sessions per service (default: 100)
- `MCP_CLEANUP_INTERVAL`: Session cleanup interval in seconds (default: 60)
- `MCP_LOG_LEVEL`: Logging level (default: INFO)
- `MCP_LOG_FILE`: Log file path (default: gateway.log)

### Sandbox Configuration Options

Each service can have its own sandbox configuration:

| Option | Default | Description |
|--------|---------|-------------|
| `enabled` | `true` | Enable/disable sandboxing completely |
| `resource_limits` | `true` | Apply CPU, memory, process limits |
| `restricted_env` | `true` | Use minimal environment variables |
| `working_dir` | Current dir | Working directory for the process |

#### Sandbox Profiles

1. **High Security** (Default) - Process isolation with resource limits and restricted environment variables.
2. **Medium Security** - Resource limits enabled but full environment variables (for services needing PATH, NODE_PATH, etc.).
3. **No Sandbox** - Disabled sandboxing for trusted services with full filesystem and resource access.

## API Endpoints

### Gateway Service Endpoints

#### GET /health
Health check endpoint
```bash
curl http://localhost:8080/health
```

#### GET /services
List available MCP services
```bash
curl -u admin:changeme http://localhost:8080/services
```

#### GET /services/{service_name}/tools
Get tools for a specific service
```bash
curl -u admin:changeme http://localhost:8080/services/todo/tools
```

#### POST /services/{service_name}/call
Call a tool on a service

```bash
curl -u admin:changeme -X POST http://localhost:8080/services/todo/call \
  -H "Content-Type: application/json" \
  -d '{
    "tool": "add_todo",
    "arguments": {"text": "Test item"},
    "session_id": "test-123",
    "timeout": 300
  }'
```

#### GET /sessions
List active sessions

#### DELETE /sessions/{session_id}
Close a specific session

## Security Features

### Authentication
- **Basic Auth**: Username/password authentication
- **Bearer Token**: Alternative token-based authentication
- **Dual Support**: Can use either Basic Auth or Bearer tokens

### Input Validation
- Service name validation (alphanumeric + dash/underscore, max 64 chars)
- Session ID validation (alphanumeric + dot/dash/underscore, max 128 chars)
- Tool name validation (alphanumeric + dash/underscore, max 64 chars)
- Request size limits (10 MB max)

### Rate Limiting
Configurable through the `rate_limiting` section in `config.json`.

### Security Headers
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Content-Security-Policy: default-src 'none'`
- `Strict-Transport-Security` (HTTPS only)

### Process Sandboxing
Configurable per MCP service with three security levels (High, Medium, No Sandbox).

## Testing

### End-to-End Testing with C#

```csharp
using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "MCP Test Agent",
    Route = "/mcp-test",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.PromptAddSection(
    "Role",
    "You are a helpful assistant with access to external MCP tools."
);

agent.AddSkill("mcp_gateway", new Dictionary<string, object>
{
    ["gateway_url"]   = "http://localhost:8080",
    ["auth_user"]     = "admin",
    ["auth_password"] = "changeme",
    ["services"]      = new List<Dictionary<string, object>>
    {
        new() { ["name"] = "todo" },
    },
});

agent.Run();
```

### Testing with curl

```bash
# Start the gateway
cd mcp_gateway
python3 gateway_service.py

# Test with curl
./test/test_gateway.sh
```

## Deployment

### Local Development
```bash
cd mcp_gateway
python3 gateway_service.py
```

### Docker Deployment
```bash
cd mcp_gateway
docker build -t mcp-gateway .
docker run -p 8080:8080 -v $(pwd)/config.json:/app/config.json mcp-gateway
```

### Docker Compose
```bash
cd mcp_gateway
docker-compose up -d
```

## Implementation Details

### Session Management
1. **Session Creation**: First tool call creates session with `call_id`
2. **Session Persistence**: Sessions maintained across multiple tool calls
3. **Session Cleanup**: Automatic cleanup on timeout or hangup hook
4. **State Isolation**: Each session gets separate MCP server instance

### Error Handling
1. **MCP Server Failures**: Automatic restart with backoff
2. **Network Errors**: Retry logic with configurable attempts
3. **Invalid Requests**: Clear error messages returned to SWAIG
4. **Resource Exhaustion**: Reject new sessions when at limit

## Troubleshooting

### Common Issues

1. **MCP Server Won't Start** - Check command path in `config.json`, verify server is executable, check logs for import errors.

2. **Authentication Failures** - Verify credentials match in config and skill, check Basic Auth header format.

3. **Session Timeouts** - Increase timeout in skill configuration, check gateway logs for premature cleanup.

4. **SSL Certificate Errors** - For self-signed certs, set `verify_ssl` to `false`.

### Debug Mode

Enable debug logging:
```json
{
    "logging": {
        "level": "DEBUG",
        "file": "gateway.log"
    }
}
```
