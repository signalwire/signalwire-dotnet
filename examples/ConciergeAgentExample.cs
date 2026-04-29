// Concierge Agent Example
//
// A hotel concierge agent that helps guests with reservations,
// local recommendations, and hotel services.

using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name       = "Hotel Concierge",
    Route      = "/concierge",
    AutoAnswer = true,
});

agent.PromptAddSection(
    "Personality",
    "You are Pierre, a distinguished hotel concierge at The Grand Hotel. "
    + "You are warm, knowledgeable, and always eager to help guests."
);

agent.PromptAddSection("Services", "You can assist guests with:", new List<string>
{
    "Restaurant reservations and dining recommendations",
    "Local attractions and sightseeing suggestions",
    "Transportation arrangements (taxis, car service)",
    "Room service and hotel amenities",
    "Event and entertainment bookings",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object>
{
    ["ai_model"]              = "gpt-4.1-nano",
    ["end_of_speech_timeout"] = 600,
});

agent.DefineTool(
    name:        "make_reservation",
    description: "Make a restaurant reservation for the guest",
    parameters:  new Dictionary<string, object>
    {
        ["restaurant"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Restaurant name",
        },
        ["party_size"] = new Dictionary<string, object>
        {
            ["type"]        = "integer",
            ["description"] = "Number of guests",
        },
        ["time"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Reservation time (e.g. 7:00 PM)",
        },
    },
    handler: (args, raw) =>
    {
        var restaurant = args.GetValueOrDefault("restaurant")?.ToString() ?? "The Bistro";
        var size       = args.GetValueOrDefault("party_size")?.ToString() ?? "2";
        var time       = args.GetValueOrDefault("time")?.ToString()       ?? "7:00 PM";

        return new FunctionResult(
            $"Reservation confirmed at {restaurant} for {size} guests at {time}. "
            + "A confirmation has been sent to your room."
        );
    }
);

agent.DefineTool(
    name:        "arrange_transport",
    description: "Arrange transportation for the guest",
    parameters:  new Dictionary<string, object>
    {
        ["destination"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Where the guest wants to go",
        },
        ["type"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Transport type (taxi, car_service, shuttle)",
        },
    },
    handler: (args, raw) =>
    {
        var dest = args.GetValueOrDefault("destination")?.ToString() ?? "the airport";
        var type = args.GetValueOrDefault("type")?.ToString()        ?? "taxi";

        return new FunctionResult(
            $"A {type} has been arranged to {dest}. "
            + "It will arrive at the hotel entrance in approximately 10 minutes."
        );
    }
);

agent.DefineTool(
    name:        "room_service",
    description: "Place a room service order for the guest",
    parameters:  new Dictionary<string, object>
    {
        ["items"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Items to order",
        },
    },
    handler: (args, raw) =>
    {
        var items = args.GetValueOrDefault("items")?.ToString() ?? "water";
        return new FunctionResult(
            $"Room service order placed: {items}. "
            + "Your order will be delivered in approximately 20 minutes."
        );
    }
);

Console.WriteLine("Starting Hotel Concierge Agent");
Console.WriteLine("Available at: http://localhost:3000/concierge");

agent.Run();
