// Tap Example
//
// Demonstrates using the tap action on FunctionResult to set up
// real-time media tapping (streaming call audio to an external endpoint).

using SignalWire.SWAIG;
using System.Text.Json;

void PrintResult(string label, FunctionResult result)
{
    Console.WriteLine($"=== {label} ===");
    Console.WriteLine(JsonSerializer.Serialize(result.ToDict(),
        new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine();
}

// 1. Basic tap setup
var basicTap = new FunctionResult("Starting audio tap for quality monitoring");
basicTap.Tap(new Dictionary<string, object>
{
    ["uri"]       = "wss://monitor.example.com/tap",
    ["direction"] = "both",
    ["codec"]     = "PCMU",
});
PrintResult("Basic Tap", basicTap);

// 2. Tap with control ID for later stop
var controlledTap = new FunctionResult("Starting controlled audio tap");
controlledTap.Tap(new Dictionary<string, object>
{
    ["uri"]        = "wss://analytics.example.com/stream",
    ["control_id"] = "analytics_tap_001",
    ["direction"]  = "listen",
    ["codec"]      = "PCMA",
});
PrintResult("Controlled Tap", controlledTap);

// 3. Stop a tap
var stopTap = new FunctionResult("Stopping analytics tap");
stopTap.StopTap("analytics_tap_001");
PrintResult("Stop Tap", stopTap);

// 4. Real-time transcription tap
var transcriptionTap = new FunctionResult("Enabling real-time transcription");
transcriptionTap.Tap(new Dictionary<string, object>
{
    ["uri"]        = "wss://transcription.example.com/stream",
    ["control_id"] = "transcription_001",
    ["direction"]  = "both",
});
transcriptionTap.UpdateGlobalData(new Dictionary<string, object>
{
    ["transcription_active"] = true,
    ["tap_id"]               = "transcription_001",
});
PrintResult("Transcription Tap", transcriptionTap);

Console.WriteLine("All tap examples completed.");
Console.WriteLine("\nKey Features:");
Console.WriteLine("- Real-time audio streaming to external endpoints");
Console.WriteLine("- Directional tapping (both, listen, speak)");
Console.WriteLine("- Control IDs for starting/stopping taps");
Console.WriteLine("- Integration with transcription and analytics services");
