// Room and SIP Example
//
// Demonstrates using join_room, sip_refer, and join_conference
// FunctionResult actions for multi-party communication and SIP transfers.

using SignalWire.SWAIG;
using System.Text.Json;

void PrintResult(string label, FunctionResult result)
{
    Console.WriteLine($"=== {label} ===");
    Console.WriteLine(JsonSerializer.Serialize(result.ToDict(),
        new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine();
}

// 1. Basic room join
var roomJoin = new FunctionResult("Joining the support team room");
roomJoin.JoinRoom("support_team_room");
PrintResult("Basic Room Join", roomJoin);

// 2. Conference room with metadata
var conference = new FunctionResult("Setting up daily standup meeting");
conference.JoinRoom("daily_standup_room");
conference.UpdateGlobalData(new Dictionary<string, object>
{
    ["meeting_active"] = true,
    ["room_name"]      = "daily_standup_room",
});
PrintResult("Conference Room", conference);

// 3. Basic SIP REFER
var sipRefer = new FunctionResult("Transferring your call to support");
sipRefer.SipRefer("sip:support@company.com");
PrintResult("Basic SIP REFER", sipRefer);

// 4. Advanced SIP REFER
var advancedSip = new FunctionResult("Transferring to technical support");
advancedSip.SipRefer("sip:tech-specialist@pbx.company.com:5060");
advancedSip.UpdateGlobalData(new Dictionary<string, object>
{
    ["transfer_completed"]  = true,
    ["transfer_destination"] = "tech-specialist@pbx.company.com",
});
PrintResult("Advanced SIP REFER", advancedSip);

// 5. Customer service workflow
var serviceRoom = new FunctionResult("Connecting to customer service");
serviceRoom.JoinRoom("customer_service_room");
PrintResult("Service Room Join", serviceRoom);

var escalate = new FunctionResult("Escalating to manager");
escalate.SipRefer("sip:manager@customer-service.company.com");
escalate.UpdateGlobalData(new Dictionary<string, object>
{
    ["escalated"]          = true,
    ["escalation_reason"]  = "customer_request",
});
PrintResult("Escalate to Manager", escalate);

// 6. Emergency escalation
var emergency = new FunctionResult("Emergency escalation in progress");
emergency.JoinRoom("emergency_response_room");
emergency.SipRefer("sip:emergency-manager@company.com:5060");
emergency.UpdateGlobalData(new Dictionary<string, object>
{
    ["emergency_active"]       = true,
    ["response_team_notified"] = true,
});
PrintResult("Emergency Escalation", emergency);

Console.WriteLine("All room and SIP examples completed.");
Console.WriteLine("\nKey Features:");
Console.WriteLine("- RELAY room joining for multi-party communication");
Console.WriteLine("- SIP REFER for call transfers in SIP environments");
Console.WriteLine("- Global data management for workflow state");
Console.WriteLine("- Method chaining with other actions");
