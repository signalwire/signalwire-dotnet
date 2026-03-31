# WebService Documentation

The `WebService` class provides static file serving capabilities for the SignalWire AI Agents SDK. It follows the same architectural pattern as other SDK services, allowing it to run as a standalone service or alongside your AI agents.

## Table of Contents
- [Overview](#overview)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Security Features](#security-features)
- [HTTPS/SSL Support](#httpsssl-support)
- [API Endpoints](#api-endpoints)
- [Usage Examples](#usage-examples)
- [Deployment Patterns](#deployment-patterns)

## Overview

WebService is designed to serve static files with configurable security features. It is suitable for:
- Serving agent documentation and API specs
- Hosting static assets (images, CSS, JavaScript)
- Serving generated reports and exports
- Providing configuration files and templates
- Hosting agent UI components

### Key Features
- **Multiple directory mounting** - Serve different directories at different URL paths
- **Security-first design** - Authentication, CORS, security headers, file filtering
- **HTTPS support** - Full SSL/TLS support with PEM files
- **Directory browsing** - Optional HTML directory listings
- **MIME type handling** - Automatic content-type detection
- **Path traversal protection** - Prevents access outside designated directories
- **File filtering** - Allow/block specific file extensions

## Installation

WebService is included in the core SignalWire AI Agents SDK:

```bash
dotnet add package SignalWire.Agents
```

## Quick Start

```csharp
using SignalWire.Services;

// Create a service to serve files
var service = new WebService(new WebServiceOptions
{
    Port = 8002,
    Directories = new Dictionary<string, string>
    {
        ["/docs"]   = "./documentation",
        ["/assets"] = "./static/assets",
    },
});

// Start the service
service.Start();
// Service available at http://localhost:8002
// Basic Auth: dev:w00t (auto-generated)
```

## Configuration

WebService can be configured through multiple methods (in order of priority):

### 1. Constructor Parameters

```csharp
var service = new WebService(new WebServiceOptions
{
    Port                    = 8002,
    Directories             = new Dictionary<string, string>
    {
        ["/docs"]   = "./documentation",
        ["/assets"] = "./static",
    },
    BasicAuth               = ("admin", "secret"),
    EnableDirectoryBrowsing = true,
    AllowedExtensions       = new[] { ".html", ".css", ".js" },
    BlockedExtensions       = new[] { ".env", ".key" },
    MaxFileSize             = 100 * 1024 * 1024, // 100 MB
    EnableCors              = true,
});
```

### 2. Environment Variables

```bash
# Basic authentication
export SWML_BASIC_AUTH_USER="admin"
export SWML_BASIC_AUTH_PASS="secretpassword"

# SSL/HTTPS configuration
export SWML_SSL_ENABLED=true
export SWML_SSL_CERT="/path/to/cert.pem"
export SWML_SSL_KEY="/path/to/key.pem"

# Security settings
export SWML_ALLOWED_HOSTS="example.com,*.example.com"
export SWML_CORS_ORIGINS="https://app.example.com"
```

### 3. Configuration File

Create a `web.json` or `swml_web.json` file:

```json
{
    "service": {
        "port": 8002,
        "directories": {
            "/docs": "./documentation",
            "/api": "./api-specs",
            "/reports": "./generated/reports"
        },
        "enable_directory_browsing": true,
        "max_file_size": 52428800,
        "allowed_extensions": [".html", ".css", ".js", ".json", ".pdf"],
        "blocked_extensions": [".env", ".key", ".pem"]
    },
    "security": {
        "basic_auth": {
            "username": "admin",
            "password": "secure123"
        },
        "ssl_enabled": true,
        "ssl_cert": "/etc/ssl/certs/server.crt",
        "ssl_key": "/etc/ssl/private/server.key",
        "allowed_hosts": ["*"],
        "cors_origins": ["*"]
    }
}
```

## Security Features

### Basic Authentication

WebService implements HTTP Basic Authentication. Credentials can be set via:

1. **Constructor**: `BasicAuth = ("username", "password")`
2. **Environment**: `SWML_BASIC_AUTH_USER` and `SWML_BASIC_AUTH_PASS`
3. **Config file**: `security.basic_auth` section
4. **Auto-generated**: If not specified, generates random credentials

### File Security

#### Default Blocked Extensions/Files
- `.env`, `.git`, `.gitignore`
- `.key`, `.pem`, `.crt`
- `.dll`, `.exe`
- `.DS_Store`, `.swp`

#### Path Traversal Protection
WebService prevents access outside designated directories:
```
# These attempts will be blocked:
# GET /docs/../../../etc/passwd
# GET /docs/./././../config.json
```

#### File Size Limits
Default maximum file size is 100 MB. Configure with:
```csharp
var service = new WebService(new WebServiceOptions
{
    MaxFileSize = 50 * 1024 * 1024 // 50 MB
});
```

### Security Headers

Automatically adds security headers to all responses:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Strict-Transport-Security` (when HTTPS is enabled)

## HTTPS/SSL Support

WebService provides multiple ways to enable HTTPS:

### Method 1: Environment Variables

```bash
# Using file paths
export SWML_SSL_CERT="/path/to/cert.pem"
export SWML_SSL_KEY="/path/to/key.pem"
```

### Method 2: Direct Parameters

```csharp
var service = new WebService(new WebServiceOptions
{
    Directories = new Dictionary<string, string> { ["/docs"] = "./docs" }
});

service.Start(
    sslCert: "/path/to/cert.pem",
    sslKey:  "/path/to/key.pem"
);
// Service available at https://localhost:8002
```

### Method 3: Configuration File

```json
{
    "security": {
        "ssl_enabled": true,
        "ssl_cert": "/etc/ssl/certs/server.crt",
        "ssl_key": "/etc/ssl/private/server.key"
    }
}
```

### Generating Self-Signed Certificates

For development/testing:

```bash
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem \
    -days 365 -nodes -subj "/CN=localhost"

export SWML_SSL_CERT="cert.pem"
export SWML_SSL_KEY="key.pem"
```

## API Endpoints

### GET /health
Health check endpoint (no authentication required)

**Response:**
```json
{
    "status": "healthy",
    "directories": ["/docs", "/assets"],
    "ssl_enabled": false,
    "auth_required": true,
    "directory_browsing": true
}
```

### GET /
Root endpoint showing available directories

**Response:** HTML page listing all mounted directories

### GET /{route}/{file_path}
Serve files from mounted directories

**Parameters:**
- `route`: The mounted directory route (e.g., `/docs`)
- `file_path`: Path to file within the directory

**Response:**
- File content with appropriate MIME type
- 404 if file not found
- 403 if file type blocked or directory browsing disabled

## Usage Examples

### Basic File Serving

```csharp
using SignalWire.Services;

var service = new WebService(new WebServiceOptions
{
    Directories = new Dictionary<string, string>
    {
        ["/docs"] = "./documentation",
        ["/api"]  = "./api-specs",
    },
});
service.Start();

// Files accessible at:
// http://localhost:8002/docs/index.html
// http://localhost:8002/api/swagger.json
```

### With Directory Browsing

```csharp
var service = new WebService(new WebServiceOptions
{
    Directories             = new Dictionary<string, string> { ["/files"] = "./public" },
    EnableDirectoryBrowsing = true,
});
service.Start();

// Browse files at: http://localhost:8002/files/
```

### Restricted File Types

```csharp
var service = new WebService(new WebServiceOptions
{
    Directories             = new Dictionary<string, string> { ["/web"] = "./www" },
    AllowedExtensions       = new[] { ".html", ".css", ".js", ".png", ".jpg", ".woff2" },
    EnableDirectoryBrowsing = false,
});
```

### Dynamic Directory Management

```csharp
var service = new WebService();

service.AddDirectory("/docs",    "./documentation");
service.AddDirectory("/reports", "./generated/reports");

service.RemoveDirectory("/reports");

service.Start();
```

### With Custom Authentication

```csharp
var service = new WebService(new WebServiceOptions
{
    Directories = new Dictionary<string, string> { ["/private"] = "./sensitive-docs" },
    BasicAuth   = ("admin", "super-secret-password"),
});
service.Start();
```

## Deployment Patterns

### Standalone Service

Run WebService as a dedicated static file server:

```csharp
// Program.cs
using SignalWire.Services;

var service = new WebService(new WebServiceOptions
{
    Port        = 8002,
    Directories = new Dictionary<string, string>
    {
        ["/docs"]      = "/var/www/docs",
        ["/assets"]    = "/var/www/assets",
        ["/downloads"] = "/var/www/downloads",
    },
});
service.Start();
```

### Alongside AI Agents

Run WebService alongside your AI agents on different ports:

```csharp
using SignalWire.Agent;
using SignalWire.Services;

// Start WebService in background
var webThread = new Thread(() =>
{
    var web = new WebService(new WebServiceOptions
    {
        Port        = 8002,
        Directories = new Dictionary<string, string> { ["/docs"] = "./agent-docs" },
    });
    web.Start();
});
webThread.IsBackground = true;
webThread.Start();

// Run your agent
var agent = new AgentBase(new AgentOptions
{
    Name  = "My Agent",
    Route = "/agent",
    Port  = 3000,
});
agent.Run();
```

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY ./publish /app
COPY ./static /app/static

EXPOSE 8002

ENTRYPOINT ["dotnet", "MyWebService.dll"]
```

### Nginx Reverse Proxy

For production, use Nginx as a reverse proxy:

```nginx
server {
    listen 443 ssl http2;
    server_name static.example.com;

    ssl_certificate /etc/ssl/certs/example.com.crt;
    ssl_certificate_key /etc/ssl/private/example.com.key;

    location / {
        proxy_pass http://localhost:8002;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        location ~* \.(jpg|jpeg|png|gif|ico|css|js)$ {
            proxy_pass http://localhost:8002;
            expires 1h;
            add_header Cache-Control "public, immutable";
        }
    }
}
```

## Best Practices

### Security
1. **Always use HTTPS in production** - Protect data in transit
2. **Change default credentials** - Never use auto-generated auth in production
3. **Restrict file types** - Use `AllowedExtensions` to whitelist safe files
4. **Disable directory browsing** - Turn off in production environments
5. **Use reverse proxy** - Put Nginx/Apache in front for additional security

### Performance
1. **Set appropriate cache headers** - WebService adds 1-hour cache by default
2. **Limit file sizes** - Adjust `MaxFileSize` based on your needs
3. **Use CDN for static assets** - Offload traffic for better performance
4. **Compress large files** - Use gzip/brotli at reverse proxy level

### Organization
1. **Separate content types** - Use different routes for different file types
2. **Version your assets** - Include version in path (e.g., `/assets/v1/`)
3. **Use index.html** - Provide default files for directories
4. **Document your structure** - Maintain clear directory organization

## API Reference

### WebServiceOptions Class

```csharp
public class WebServiceOptions
{
    public int Port { get; set; } = 8002;
    public Dictionary<string, string>? Directories { get; set; }
    public (string Username, string Password)? BasicAuth { get; set; }
    public string? ConfigFile { get; set; }
    public bool EnableDirectoryBrowsing { get; set; } = false;
    public string[]? AllowedExtensions { get; set; }
    public string[]? BlockedExtensions { get; set; }
    public int MaxFileSize { get; set; } = 100 * 1024 * 1024;
    public bool EnableCors { get; set; } = true;
}
```

### WebService Methods

#### Start()
```csharp
public void Start(
    string host = "0.0.0.0",
    int? port = null,
    string? sslCert = null,
    string? sslKey = null)
```
Start the web service.

#### AddDirectory()
```csharp
public void AddDirectory(string route, string directory)
```
Add a new directory to serve.

#### RemoveDirectory()
```csharp
public void RemoveDirectory(string route)
```
Remove a directory from being served.

## Summary

WebService provides a secure, configurable static file server that integrates with the SignalWire AI Agents SDK. It follows the same architectural patterns as other SDK services, making it familiar and easy to use while providing configurable security features and flexible deployment options.
