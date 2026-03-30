# Configuration Guide (.NET)

## Overview

All SignalWire services support optional JSON configuration files with environment variable substitution. Services continue to work without any configuration file.

## Quick Start

### Zero Configuration

```csharp
var agent = new AgentBase(new AgentOptions { Name = "my-agent", Route = "/agent" });
agent.Run();
```

### With Configuration File

Configuration files are auto-detected in this order:

1. `{service_name}_config.json`
2. `config.json`
3. `.swml/config.json`
4. `~/.swml/config.json`
5. `/etc/swml/config.json`

### Configuration Structure

```json
{
  "service": {
    "name": "my-service",
    "host": "${HOST|0.0.0.0}",
    "port": "${PORT|3000}"
  },
  "security": {
    "ssl_enabled": "${SSL_ENABLED|false}",
    "ssl_cert_path": "${SSL_CERT|/etc/ssl/cert.pem}",
    "ssl_key_path": "${SSL_KEY|/etc/ssl/key.pem}",
    "basic_auth_user": "${AUTH_USER|signalwire}",
    "basic_auth_password": "${AUTH_PASS|}"
  },
  "logging": {
    "level": "${LOG_LEVEL|info}",
    "format": "${LOG_FORMAT|json}"
  }
}
```

### Environment Variable Substitution

Config values support `${VAR_NAME|default}` syntax:

- `${PORT}` -- use the `PORT` env var, fail if unset
- `${PORT|3000}` -- use `PORT` if set, otherwise `3000`
- `${HOST|0.0.0.0}` -- use `HOST` if set, otherwise `0.0.0.0`

## Environment Variables

### Agent Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `SIGNALWIRE_PROJECT_ID` | - | Project ID for REST/RELAY clients |
| `SIGNALWIRE_API_TOKEN` | - | API token for REST/RELAY clients |
| `SIGNALWIRE_SPACE` | - | Space hostname (e.g. `example.signalwire.com`) |
| `SIGNALWIRE_LOG_LEVEL` | `info` | Log level (`debug`, `info`, `warn`, `error`) |

### Server Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `HOST` | `0.0.0.0` | Server bind address |
| `PORT` | `3000` | Server port |
| `SWML_DOMAIN` | - | Public domain for URL generation |

### Security Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `SWML_SSL_ENABLED` | `false` | Enable HTTPS |
| `SWML_SSL_CERT_PATH` | - | SSL certificate path |
| `SWML_SSL_KEY_PATH` | - | SSL private key path |
| `SWML_BASIC_AUTH_USER` | `signalwire` | Basic auth username |
| `SWML_BASIC_AUTH_PASSWORD` | *auto-generated* | Basic auth password |

## Programmatic Configuration

### Constructor Parameters

```csharp
var agent = new AgentBase(new AgentOptions
{
    Name              = "my-agent",
    Route             = "/agent",
    Host              = "0.0.0.0",
    Port              = 3000,
    BasicAuthUser     = "myuser",
    BasicAuthPassword = "mypass",
    AutoAnswer        = true,
    RecordCall        = false,
    RecordFormat      = "wav",
    RecordStereo      = false,
    UsePom            = true,
});
```

### AI Parameters

```csharp
agent.SetParams(new Dictionary<string, object>
{
    ["ai_model"]              = "gpt-4.1-nano",
    ["wait_for_user"]         = false,
    ["end_of_speech_timeout"] = 1000,
    ["ai_volume"]             = 5,
    ["languages_enabled"]     = true,
    ["local_tz"]              = "America/Los_Angeles",
});
```

### REST Client Configuration

```csharp
// From constructor
var client = new RestClient(
    projectId: "your-project-id",
    token:     "your-api-token",
    space:     "example.signalwire.com"
);

// From environment variables
var client = new RestClient(); // Uses SIGNALWIRE_PROJECT_ID, SIGNALWIRE_API_TOKEN, SIGNALWIRE_SPACE
```

### RELAY Client Configuration

```csharp
var client = new Client(new Dictionary<string, string>
{
    ["project"]  = "your-project-id",
    ["token"]    = "your-api-token",
    ["host"]     = "relay.signalwire.com",
    ["contexts"] = "default,support",
});
```

## Logging

Configure log levels via environment variable:

```bash
export SIGNALWIRE_LOG_LEVEL=debug    # Show WebSocket/HTTP traffic
export SIGNALWIRE_LOG_LEVEL=info     # Normal operation (default)
export SIGNALWIRE_LOG_LEVEL=warn     # Warnings only
export SIGNALWIRE_LOG_LEVEL=error    # Errors only
```
