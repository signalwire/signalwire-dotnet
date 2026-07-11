# SignalWire AI Agents SDK -- .NET Port

## Project Overview

.NET port of the SignalWire AI Agents SDK. Generates SWML documents, handles SWAIG webhooks, and provides RELAY/REST clients for the SignalWire platform.

Package: `SignalWire.Sdk` (NuGet)
Runtime: .NET 8.0+
Languages: C#, F#, VB.NET (any CLR language)

## Development Commands

Format, lint, and test go through the three canonical wrapper scripts under
`scripts/`. They self-bootstrap the .NET toolchain (via `scripts/_env.sh`) and
run from ANY working directory — do NOT invoke `dotnet format` / `dotnet build`
/ `dotnet test` directly; use these so local and CI behave identically:

```bash
# Format (default APPLY; --check = verify-only, the CI mode)
bash scripts/run-format.sh
bash scripts/run-format.sh --check

# Lint (analyzer build: AnalysisMode=All, TreatWarningsAsErrors)
bash scripts/run-lint.sh

# Test — runs the 3 target frameworks SEPARATELY (net8.0 -> net9.0 -> net10.0);
# per-TFM serialization avoids the cross-TFM TLS-listener contention. Optional
# filter arg is passed through to `dotnet test --filter`.
bash scripts/run-tests.sh
bash scripts/run-tests.sh "FullyQualifiedName~LoggerTests"

# Full local gate set (invokes the three scripts above for FMT/LINT/TEST)
bash scripts/run-ci.sh
```

Other build tasks (not gated) still use `dotnet` directly:

```bash
dotnet build -c Release      # build release
dotnet pack  -c Release      # pack NuGet package
```

## Architecture

### Core Components

1. **Logger** (`Logging/Logger.cs`) -- Singleton loggers, level filtering, env var config
2. **SWML Document** (`SWML/`) -- Document model, schema loading, auto-vivified verb methods
3. **SWMLService** (`SWML/Service.cs`) -- HTTP server base with auth and security headers
4. **AgentBase** (`Agent/AgentBase.cs`) -- Main agent class composing all features
5. **FunctionResult** (`SWAIG/FunctionResult.cs`) -- Fluent builder for tool responses (40+ actions)
6. **DataMap** (`DataMap/DataMap.cs`) -- Server-side API tools
7. **Contexts** (`Contexts/`) -- Multi-step conversation workflows
8. **Skills** (`Skills/`) -- 17 built-in skills with registry/manager
9. **Prefabs** (`Prefabs/`) -- 5 ready-made agent patterns
10. **AgentServer** (`Server/AgentServer.cs`) -- Multi-agent hosting
11. **RelayClient** (`Relay/Client.cs`) -- WebSocket real-time call control
12. **RestClient** (`REST/RestClient.cs`) -- Synchronous HTTP API client

### Key Patterns

- **Method chaining**: All config methods return `this`
- **Auto-vivification**: SWML verb methods invoked dynamically
- **Dynamic config**: Per-request agent cloning for multi-tenancy
- **Timing-safe auth**: Use `CryptographicOperations.FixedTimeEquals()` for all credential comparisons
- **Schema-driven**: 38 SWML verbs extracted from embedded schema at runtime
- **async/await**: RELAY client uses native .NET async throughout

### .NET-Specific Conventions

- Nullable reference types enabled
- C# 12 with file-scoped namespaces
- xUnit for testing
- `System.Text.Json` for JSON serialization
- `System.Security.Cryptography` for HMAC and random bytes

## File Locations

- Source: `src/SignalWire/`
- Tests: `tests/`
- Examples: `examples/`
- RELAY docs: `relay/`
- REST docs: `rest/`
- General docs: `docs/`
- CLI tools: `bin/`
- SWML schema: `src/SignalWire/SWML/schema.json`

## Reference Implementation

The Python SDK at `~/src/signalwire-python` is the source of truth.
The porting guide is at `~/src/porting-sdk/PORTING_GUIDE.md`.
Progress is tracked in `PROGRESS.md`.
