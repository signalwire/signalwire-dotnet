using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Prefabs;

/// <summary>
/// Prefab concierge agent for venue/hotel style interactions.
/// Registers <c>check_availability</c> and <c>get_directions</c> tools.
/// </summary>
public class ConciergeAgent : AgentBase
{
    private readonly string _venueName;
    private readonly List<string> _services;
    private readonly Dictionary<string, Dictionary<string, object>> _amenities;

    public ConciergeAgent(
        string name,
        Dictionary<string, object> venueInfo,
        Dictionary<string, object>? options = null)
        : base(CreateOptions(name, options))
    {
        _venueName = venueInfo.TryGetValue("venue_name", out var vn) ? vn as string ?? "Venue" : "Venue";
        _services = venueInfo.TryGetValue("services", out var sv) && sv is List<string> sl ? sl : [];
        _amenities = venueInfo.TryGetValue("amenities", out var am) && am is Dictionary<string, Dictionary<string, object>> ad ? ad : [];

        var hoursOfOperation = venueInfo.TryGetValue("hours_of_operation", out var ho) && ho is Dictionary<string, string> hd ? hd : [];
        var specialInstructions = venueInfo.TryGetValue("special_instructions", out var si) && si is List<string> sil ? sil : [];
        var welcomeMessage = venueInfo.TryGetValue("welcome_message", out var wm) ? wm as string : null;

        var welcome = welcomeMessage ?? $"Welcome to {_venueName}. How can I assist you today?";

        SetGlobalData(new Dictionary<string, object>
        {
            ["venue_name"] = _venueName,
            ["services"] = _services,
            ["amenities"] = _amenities,
        });

        PromptAddSection("Concierge Role", $"You are the virtual concierge for {_venueName}. {welcome}",
        [
            "Welcome users and explain available services",
            "Answer questions about amenities, hours, and directions",
            "Help with bookings and reservations",
            "Provide personalized recommendations",
        ]);

        if (_services.Count > 0) PromptAddSection("Available Services", "", _services);

        if (_amenities.Count > 0)
        {
            var amenityBullets = new List<string>();
            foreach (var (amenityName, info) in _amenities)
            {
                var desc = amenityName;
                if (info.TryGetValue("hours", out var h) && h is string hours) desc += " - Hours: " + hours;
                if (info.TryGetValue("location", out var l) && l is string loc) desc += " - Location: " + loc;
                amenityBullets.Add(desc);
            }
            PromptAddSection("Amenities", "", amenityBullets);
        }

        if (hoursOfOperation.Count > 0)
        {
            var hourBullets = hoursOfOperation.Select(kvp => $"{kvp.Key}: {kvp.Value}").ToList();
            PromptAddSection("Hours of Operation", "", hourBullets);
        }

        if (specialInstructions.Count > 0) PromptAddSection("Special Instructions", "", specialInstructions);

        var capturedVenueName = _venueName;

        DefineTool(
            "check_availability",
            "Check availability for a service or amenity",
            new Dictionary<string, object>
            {
                ["service"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Service or amenity to check" },
                ["date"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Date to check (optional)" },
            },
            (args, rawData) =>
            {
                var service = args.TryGetValue("service", out var s) ? s as string ?? "" : "";
                var date = args.TryGetValue("date", out var d) ? d as string ?? "" : "";
                var response = $"Checking availability for {service} at {capturedVenueName}";
                if (date.Length > 0) response += $" on {date}";
                return new FunctionResult(response);
            });

        var capturedAmenities = _amenities;

        DefineTool(
            "get_directions",
            "Get directions to a service or amenity within the venue",
            new Dictionary<string, object>
            {
                ["destination"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The amenity or area to get directions to" },
            },
            (args, rawData) =>
            {
                var destination = args.TryGetValue("destination", out var d) ? d as string ?? "" : "";
                var destinationLower = destination.ToLowerInvariant();

                foreach (var (amenityName, info) in capturedAmenities)
                {
                    if (amenityName.ToLowerInvariant() == destinationLower)
                    {
                        var location = info.TryGetValue("location", out var l) ? l as string ?? "location not specified" : "location not specified";
                        return new FunctionResult($"The {amenityName} at {capturedVenueName} is located at: {location}");
                    }
                }

                return new FunctionResult($"Directions to {destination} at {capturedVenueName}: please ask the front desk for assistance.");
            });
    }

    public string GetVenueName() => _venueName;
    public List<string> GetServices() => _services;
    public Dictionary<string, Dictionary<string, object>> GetAmenities() => _amenities;

    private static AgentOptions CreateOptions(string name, Dictionary<string, object>? options)
    {
        return new AgentOptions
        {
            Name = name.Length > 0 ? name : "concierge",
            Route = options?.TryGetValue("route", out var r) == true ? r as string ?? "/concierge" : "/concierge",
            BasicAuthUser = options?.TryGetValue("basic_auth_user", out var u) == true ? u as string : null,
            BasicAuthPassword = options?.TryGetValue("basic_auth_password", out var p) == true ? p as string : null,
            UsePom = true,
        };
    }
}
