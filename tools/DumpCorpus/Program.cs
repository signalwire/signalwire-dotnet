// DumpCorpus — the .NET port's LAYER-D per-surface DUMP program for the
// cross-port behavioral differs (porting-sdk/scripts/diff_port_<surface>.py).
//
// Same pattern as tools/EmitCorpus (the EMISSION dump), extended to the five
// Layer-D behavioral surfaces. For a given surface it runs the shared corpus
// against the .NET SDK's native API, reduces each case to the observable
// artifact the differ compares, and prints ONE JSON object mapping
//
//     case-id -> observable-artifact
//
// to stdout. The differ runs this program, canonicalizes both sides, and
// byte-compares each entry against the python oracle. Only stdout carries the
// JSON object; every log/diagnostic goes to stderr.
//
// Usage (from the signalwire-dotnet repo root):
//
//     dotnet run --project tools/DumpCorpus -- <surface>
//         <surface> ∈ { wire, swml, state, http, wire-relay, envelope }
//
// or via the clean-stdout wrapper (mirrors scripts/emit-corpus.sh):
//
//     bash scripts/dump-corpus.sh <surface>
//     python3 .../diff_port_wire.py --port dotnet \
//         --dump-cmd 'bash scripts/dump-corpus.sh wire'
using System.Text.Json;
using SignalWire.Tools.DumpCorpus;

if (args.Length != 1)
{
    await Console.Error.WriteLineAsync(
        "usage: DumpCorpus <wire|swml|strict-render|state|http|wire-relay|envelope|secure-default|secret-scrub|pagination>")
        .ConfigureAwait(false);
    return 2;
}

try
{
    Dictionary<string, object?> output = args[0] switch
    {
        "wire" => WireDump.Build(),
        "swml" => SwmlDump.Build(),
        "strict-render" => StrictRenderDump.Build(),
        "state" => StateDump.Build(),
        "http" => HttpDump.Build(),
        "wire-relay" => await WireRelayDump.BuildAsync().ConfigureAwait(false),
        "envelope" => await EnvelopeDump.BuildAsync().ConfigureAwait(false),
        "secure-default" => SecureDefaultDump.Build(),
        "secret-scrub" => await SecretScrubDump.BuildAsync().ConfigureAwait(false),
        "pagination" => await PaginationDump.BuildAsync().ConfigureAwait(false),
        _ => throw new ArgumentException($"unknown surface '{args[0]}'"),
    };

    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(output, Canon.JsonOptions))
        .ConfigureAwait(false);
    return 0;
}
#pragma warning disable CA1031 // A CLI entry point must convert ANY failure into a
// non-zero exit + a diagnostic on stderr; letting it escape would print an
// unhandled-exception dump instead of the message the calling gate parses.
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"DumpCorpus[{args[0]}]: {ex}").ConfigureAwait(false);
    return 1;
}
#pragma warning restore CA1031
