using System.Diagnostics.CodeAnalysis;
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

    /// <summary>The venue this concierge represents.
    /// (equivalent to Python's <c>venue_name</c>.)</summary>
    [SuppressMessage("Naming", "CA1721", Justification = "Both the property and the get_* accessor are part of the cross-port surface: the property is the reference attribute (readback), the Get* method the pre-existing cross-port accessor.")]
    public string VenueName => _venueName;

    /// <summary>The services the venue offers.
    /// (equivalent to Python's <c>services</c>.)</summary>
    [SuppressMessage("Naming", "CA1721", Justification = "Both the property and the get_* accessor are part of the cross-port surface: the property is the reference attribute (readback), the Get* method the pre-existing cross-port accessor.")]
    public IReadOnlyList<string> Services => _services;

    /// <summary>The venue's amenities, keyed by name.
    /// (equivalent to Python's <c>amenities</c>.)</summary>
    [SuppressMessage("Naming", "CA1721", Justification = "Both the property and the get_* accessor are part of the cross-port surface: the property is the reference attribute (readback), the Get* method the pre-existing cross-port accessor.")]
    public IReadOnlyDictionary<string, Dictionary<string, object>> Amenities => _amenities;

    /// <summary>The venue's opening hours, keyed by day/label.
    /// (equivalent to Python's <c>hours_of_operation</c>.)</summary>
    public IReadOnlyDictionary<string, string> HoursOfOperation { get; }

    /// <summary>Extra instructions appended to the agent's prompt.
    /// (equivalent to Python's <c>special_instructions</c>.)</summary>
    public IReadOnlyList<string> SpecialInstructions { get; }

    public ConciergeAgent(
        string name,
        Dictionary<string, object> venueInfo,
        Dictionary<string, object>? options = null)
        : base(CreateOptions(name, options))
    {
        ArgumentNullException.ThrowIfNull(venueInfo);
        _venueName = venueInfo.TryGetValue("venue_name", out var vn) ? vn as string ?? "Venue" : "Venue";
        _services = venueInfo.TryGetValue("services", out var sv) && sv is List<string> sl ? sl : [];
        _amenities = venueInfo.TryGetValue("amenities", out var am) && am is Dictionary<string, Dictionary<string, object>> ad ? ad : [];

        // The reference STORES both (concierge.py:78-79) and defaults the hours
        // to a single "default" entry; they were previously read into locals,
        // rendered, and then dropped — a caller could not read back what it set.
        var hoursOfOperation = venueInfo.TryGetValue("hours_of_operation", out var ho) && ho is Dictionary<string, string> hd && hd.Count > 0
            ? hd
            : new Dictionary<string, string> { ["default"] = "9 AM - 5 PM" };
        var specialInstructions = venueInfo.TryGetValue("special_instructions", out var si) && si is List<string> sil ? sil : [];
        HoursOfOperation = hoursOfOperation;
        SpecialInstructions = specialInstructions;
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

        DefineTool(
            "check_availability",
            "Check availability for a service or amenity",
            new Dictionary<string, object>
            {
                ["service"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Service or amenity to check" },
                ["date"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Date to check (optional)" },
            },
            CheckAvailability);

        DefineTool(
            "get_directions",
            "Get directions to a service or amenity within the venue",
            new Dictionary<string, object>
            {
                ["destination"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The amenity or area to get directions to" },
            },
            GetDirections);
    }

    /// <summary>SWAIG tool handler for the ``check_availability`` tool.
    /// (equivalent to Python's ``ConciergeAgent.check_availability(args, raw_data)``.)</summary>
    public FunctionResult CheckAvailability(Dictionary<string, object> args, Dictionary<string, object?> rawData)
    {
        ArgumentNullException.ThrowIfNull(args);
        var service = args.TryGetValue("service", out var s) ? s as string ?? "" : "";
        var date = args.TryGetValue("date", out var d) ? d as string ?? "" : "";
        var response = $"Checking availability for {service} at {_venueName}";
        if (date.Length > 0) response += $" on {date}";
        return new FunctionResult(response);
    }

    /// <summary>SWAIG tool handler for the ``get_directions`` tool.
    /// (equivalent to Python's ``ConciergeAgent.get_directions(args, raw_data)``.)</summary>
    public FunctionResult GetDirections(Dictionary<string, object> args, Dictionary<string, object?> rawData)
    {
        ArgumentNullException.ThrowIfNull(args);
        var destination = args.TryGetValue("destination", out var d) ? d as string ?? "" : "";

        foreach (var (amenityName, info) in _amenities)
        {
            if (amenityName.Equals(destination, StringComparison.OrdinalIgnoreCase))
            {
                var location = info.TryGetValue("location", out var l) ? l as string ?? "location not specified" : "location not specified";
                return new FunctionResult($"The {amenityName} at {_venueName} is located at: {location}");
            }
        }

        return new FunctionResult($"Directions to {destination} at {_venueName}: please ask the front desk for assistance.");
    }

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public string GetVenueName() => _venueName;

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public IReadOnlyList<string> GetServices() => _services;

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public Dictionary<string, Dictionary<string, object>> GetAmenities() => _amenities;

    private static AgentOptions CreateOptions(string name, Dictionary<string, object>? options)
    {
        ArgumentNullException.ThrowIfNull(name);
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
