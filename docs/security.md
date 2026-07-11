# Security Configuration Guide (.NET)

## Overview

The SignalWire .NET SDK provides a unified security configuration system with secure defaults for HTTPS, basic authentication, and security headers.

<!-- snippet-setup -->
```csharp
using SignalWire.Agent;
using System.Collections.Generic;
// Shared context for the fragments below: a constructed `agent`.
AgentBase agent = new AgentBase(new AgentOptions { Name = "a", Route = "/a" });
```

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
| `SWML_SSL_VERIFY_MODE` | `CERT_REQUIRED` | Peer-certificate verification mode |
| `SWML_DOMAIN` | - | Domain name for URL generation |

### Authentication

| Variable | Default | Description |
|----------|---------|-------------|
| `SWML_BASIC_AUTH_USER` | `signalwire` | Basic auth username |
| `SWML_BASIC_AUTH_PASSWORD` | *auto-generated* | Basic auth password (32-char token) |
| `SIGNALWIRE_SIGNING_KEY` | - | Signing key used to validate inbound webhook signatures |

### Security Headers

| Variable | Default | Description |
|----------|---------|-------------|
| `SWML_RATE_LIMIT` | `60` | Requests per minute per IP |
| `SWML_USE_HSTS` | `true` | Emit the HTTP Strict-Transport-Security header |
| `SWML_HSTS_MAX_AGE` | `31536000` | HSTS `max-age` in seconds (default 1 year) |

### Request Limits and URL Validation

| Variable | Default | Description |
|----------|---------|-------------|
| `SWML_MAX_REQUEST_SIZE` | `10485760` | Maximum inbound request body size in bytes (default 10MB) |
| `SWML_REQUEST_TIMEOUT` | `30` | Per-request timeout in seconds |
| `SWML_ALLOW_PRIVATE_URLS` | `false` | Allow SWML/webhook URLs that resolve to private/loopback addresses (SSRF guard bypass) |

## Authentication Details

### Auto-Generated Credentials

When no credentials are provided, the SDK generates secure credentials automatically:

```csharp
using System;
using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions { Name = "my-agent" });

// Credentials are auto-generated; GetBasicAuthCredentials() returns the resolved pair
var (user, password) = agent.GetBasicAuthCredentials();
Console.WriteLine($"User: {user}");
Console.WriteLine($"Pass: {password}");
```

### Explicit Credentials

```csharp
using System;
using SignalWire.Agent;

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
6. Store credentials in environment variables, not in source code
