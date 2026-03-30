# Security Configuration Guide (.NET)

## Overview

The SignalWire .NET SDK provides a unified security configuration system with secure defaults for HTTPS, basic authentication, and security headers.

## Quick Start

### Basic HTTPS Setup

```bash
export SWML_SSL_ENABLED=true
export SWML_SSL_CERT_PATH=/path/to/cert.pem
export SWML_SSL_KEY_PATH=/path/to/key.pem
export SWML_DOMAIN=yourdomain.com
```

### Basic Authentication

Basic auth is enabled by default with auto-generated credentials:

```bash
export SWML_BASIC_AUTH_USER=myusername
export SWML_BASIC_AUTH_PASSWORD=mysecurepassword
```

## Environment Variables

### SSL/TLS Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `SWML_SSL_ENABLED` | `false` | Enable HTTPS |
| `SWML_SSL_CERT_PATH` | - | Path to SSL certificate |
| `SWML_SSL_KEY_PATH` | - | Path to SSL private key |
| `SWML_DOMAIN` | - | Domain name for URL generation |

### Authentication

| Variable | Default | Description |
|----------|---------|-------------|
| `SWML_BASIC_AUTH_USER` | `signalwire` | Basic auth username |
| `SWML_BASIC_AUTH_PASSWORD` | *auto-generated* | Basic auth password (32-char token) |

### Security Headers

| Variable | Default | Description |
|----------|---------|-------------|
| `SWML_ALLOWED_ORIGINS` | `*` | CORS allowed origins |
| `SWML_RATE_LIMIT` | `100` | Requests per minute per IP |

## Authentication Details

### Auto-Generated Credentials

When no credentials are provided, the SDK generates secure credentials automatically:

```csharp
var agent = new AgentBase(new AgentOptions { Name = "my-agent" });

// Credentials are auto-generated
Console.WriteLine($"User: {agent.BasicAuthUser()}");
Console.WriteLine($"Pass: {agent.BasicAuthPassword()}");
```

### Explicit Credentials

```csharp
var agent = new AgentBase(new AgentOptions
{
    Name              = "my-agent",
    BasicAuthUser     = "custom-user",
    BasicAuthPassword = "custom-password-123",
});
```

### SWAIG Webhook Authentication

SWAIG webhook URLs automatically include basic auth credentials:

```
http://signalwire:abc123@localhost:3000/agent/swaig
```

This ensures that only SignalWire can call your SWAIG endpoints.

## Security Headers

All responses include security headers:

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Content-Type-Options` | `nosniff` | Prevent MIME sniffing |
| `X-Frame-Options` | `DENY` | Prevent clickjacking |
| `Cache-Control` | `no-store` | Prevent caching of sensitive data |
| `Content-Type` | `application/json` | Explicit content type |

## Proxy URL Configuration

When behind a reverse proxy, set the proxy URL manually:

```csharp
agent.ManualSetProxyUrl("https://myagent.example.com");
```

This ensures SWAIG webhook URLs and post-prompt URLs use the correct public address.

## Production Deployment Checklist

1. Enable HTTPS via `SWML_SSL_ENABLED=true`
2. Set explicit basic auth credentials (do not rely on auto-generated in production)
3. Configure a reverse proxy (nginx, Caddy, etc.) for TLS termination
4. Set `SWML_DOMAIN` to your public domain
5. Use `ManualSetProxyUrl()` when behind a load balancer
6. Restrict `SWML_ALLOWED_ORIGINS` to your specific domains
7. Store credentials in environment variables, not in source code
