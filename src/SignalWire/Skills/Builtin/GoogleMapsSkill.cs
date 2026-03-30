using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Validate addresses and compute driving routes using Google Maps (DataMap).</summary>
public sealed class GoogleMapsSkill : SkillBase
{
    public override string Name => "google_maps";
    public override string Description => "Validate addresses and compute driving routes using Google Maps";

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("api_key", out var k) && k is string s && s.Length > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var apiKey = Params.TryGetValue("api_key", out var k) ? k as string ?? "" : "";
        var lookupToolName = Params.TryGetValue("lookup_tool_name", out var ln) ? ln as string ?? "lookup_address" : "lookup_address";
        var routeToolName = Params.TryGetValue("route_tool_name", out var rn) ? rn as string ?? "compute_route" : "compute_route";

        // lookup_address DataMap tool
        var lookupDef = new Dictionary<string, object>
        {
            ["function"] = lookupToolName,
            ["purpose"] = "Look up and validate an address using Google Maps Geocoding",
            ["argument"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["address"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The address to look up",
                    },
                },
                ["required"] = new List<string> { "address" },
            },
            ["data_map"] = new Dictionary<string, object>
            {
                ["webhooks"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["url"] = $"https://maps.googleapis.com/maps/api/geocode/json?address=${{enc:args.address}}&key={apiKey}",
                        ["method"] = "GET",
                        ["output"] = new Dictionary<string, object>
                        {
                            ["response"] = "Address found: ${results[0].formatted_address}. "
                                + "Latitude: ${results[0].geometry.location.lat}, "
                                + "Longitude: ${results[0].geometry.location.lng}",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                        ["error_output"] = new Dictionary<string, object>
                        {
                            ["response"] = "Unable to look up the address. Please check the address and try again.",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                    },
                },
            },
        };

        // compute_route DataMap tool
        var routeDef = new Dictionary<string, object>
        {
            ["function"] = routeToolName,
            ["purpose"] = "Compute a driving route between two locations using Google Maps",
            ["argument"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["origin_lat"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Latitude of the origin" },
                    ["origin_lng"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Longitude of the origin" },
                    ["dest_lat"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Latitude of the destination" },
                    ["dest_lng"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Longitude of the destination" },
                },
                ["required"] = new List<string> { "origin_lat", "origin_lng", "dest_lat", "dest_lng" },
            },
            ["data_map"] = new Dictionary<string, object>
            {
                ["webhooks"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["url"] = "https://routes.googleapis.com/directions/v2:computeRoutes",
                        ["method"] = "POST",
                        ["headers"] = new Dictionary<string, object>
                        {
                            ["X-Goog-Api-Key"] = apiKey,
                            ["X-Goog-FieldMask"] = "routes.duration,routes.distanceMeters,routes.legs",
                            ["Content-Type"] = "application/json",
                        },
                        ["output"] = new Dictionary<string, object>
                        {
                            ["response"] = "Route computed. Distance: ${routes[0].distanceMeters} meters, Duration: ${routes[0].duration}",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                        ["error_output"] = new Dictionary<string, object>
                        {
                            ["response"] = "Unable to compute route between the specified locations.",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                    },
                },
            },
        };

        Agent.RegisterSwaigFunction(lookupDef);
        Agent.RegisterSwaigFunction(routeDef);
    }

    public override List<string> GetHints() =>
        ["address", "location", "route", "directions", "miles", "distance"];

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        return [new Dictionary<string, object>
        {
            ["title"] = "Google Maps",
            ["body"] = "You can look up addresses and compute driving routes.",
            ["bullets"] = new List<string>
            {
                "Use lookup_address to validate and geocode an address.",
                "Use compute_route to get driving distance and duration between two coordinates.",
                "First look up addresses to get coordinates, then compute routes between them.",
            },
        }];
    }
}
