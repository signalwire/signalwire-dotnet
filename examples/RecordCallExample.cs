// Record Call Example
//
// Demonstrates using record_call and stop_record_call actions
// with FunctionResult to control background call recording.

using SignalWire.SWAIG;
using System.Text.Json;

void PrintResult(string label, FunctionResult result)
{
    Console.WriteLine($"=== {label} ===");
    Console.WriteLine(JsonSerializer.Serialize(result.ToDict(),
        new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine();
}

// 1. Basic recording
var basic = new FunctionResult("Starting basic call recording");
basic.RecordCall();
PrintResult("Basic Recording", basic);

// 2. Advanced recording with custom settings
var advanced = new FunctionResult("Starting advanced call recording");
advanced.RecordCall(
    controlId: "support_call_001",
    stereo:    true,
    format:    "mp3"
);
PrintResult("Advanced Recording", advanced);

// 3. Stop a specific recording
var stop = new FunctionResult("Ending call recording");
stop.StopRecordCall("support_call_001");
PrintResult("Stop Recording", stop);

// 4. Customer service workflow: start recording
var startCs = new FunctionResult("Transferring you to a customer service agent");
startCs.RecordCall(controlId: "cs_transfer_001", format: "mp3");
startCs.UpdateGlobalData(new Dictionary<string, object> { ["recording_id"] = "cs_transfer_001" });
PrintResult("Customer Service - Start", startCs);

// 5. Customer service workflow: stop recording
var endCs = new FunctionResult("Call recording stopped");
endCs.StopRecordCall("cs_transfer_001");
PrintResult("Customer Service - Stop", endCs);

// 6. Compliance recording
var compliance = new FunctionResult("This call is being recorded for compliance purposes");
compliance.RecordCall(
    controlId: "compliance_rec_001",
    stereo:    true,
    format:    "wav"
);
PrintResult("Compliance Recording", compliance);

Console.WriteLine("All recording examples completed.");
Console.WriteLine("\nKey Features:");
Console.WriteLine("- Basic and advanced recording configurations");
Console.WriteLine("- Background recording that doesn't block execution");
Console.WriteLine("- Customizable audio format and quality settings");
Console.WriteLine("- Method chaining with other actions");
