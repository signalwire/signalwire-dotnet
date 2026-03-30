using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Get current date, time, and timezone information.</summary>
public sealed class DatetimeSkill : SkillBase
{
    public override string Name => "datetime";
    public override string Description => "Get current date, time, and timezone information";

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters) => true;

    public override void RegisterTools(AgentBase agent)
    {
        DefineTool(
            "get_current_time",
            "Get the current time, optionally in a specific timezone",
            new Dictionary<string, object>
            {
                ["timezone"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Timezone name (e.g., America/New_York, Europe/London). Defaults to UTC.",
                },
            },
            (args, rawData) =>
            {
                var result = new FunctionResult();
                var tz = args.TryGetValue("timezone", out var tzObj) ? tzObj as string ?? "UTC" : "UTC";

                try
                {
                    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(tz);
                    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
                    result.SetResponse($"The current time in {tz} is {now:HH:mm:ss}");
                }
                catch (TimeZoneNotFoundException)
                {
                    result.SetResponse($"Invalid timezone: {tz}");
                }

                return result;
            });

        DefineTool(
            "get_current_date",
            "Get the current date",
            new Dictionary<string, object>
            {
                ["timezone"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Timezone name (e.g., America/New_York, Europe/London). Defaults to UTC.",
                },
            },
            (args, rawData) =>
            {
                var result = new FunctionResult();
                var tz = args.TryGetValue("timezone", out var tzObj) ? tzObj as string ?? "UTC" : "UTC";

                try
                {
                    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(tz);
                    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
                    result.SetResponse($"The current date in {tz} is {now:yyyy-MM-dd (dddd, MMMM d, yyyy)}");
                }
                catch (TimeZoneNotFoundException)
                {
                    result.SetResponse($"Invalid timezone: {tz}");
                }

                return result;
            });
    }

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        return [new Dictionary<string, object>
        {
            ["title"] = "Date and Time Information",
            ["body"] = "You have access to date and time tools.",
            ["bullets"] = new List<string>
            {
                "Use get_current_time to retrieve the current time in any timezone.",
                "Use get_current_date to retrieve the current date in any timezone.",
                "Default timezone is UTC if none is specified.",
            },
        }];
    }
}
