// Gather Info Mode Demo
//
// Demonstrates the contexts system's gather_info mode for structured
// data collection using the low-level contexts API with steps.

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "Patient Intake Agent",
    Route = "/patient-intake",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a friendly medical office intake assistant. "
    + "Collect patient information accurately and professionally."
);

// Define contexts with gather info steps
var ctx = agent.DefineContexts();

ctx.AddContext("default", new Dictionary<string, object>
{
    ["steps"] = new List<Dictionary<string, object>>
    {
        new()
        {
            ["name"]   = "demographics",
            ["prompt"] = "Collect the patient's basic information.",
            ["gather_info"] = new Dictionary<string, object>
            {
                ["output_key"] = "patient_demographics",
                ["prompt"]     = "Please collect the following patient information.",
                ["questions"]  = new List<Dictionary<string, object>>
                {
                    new() { ["field"] = "full_name",     ["text"] = "What is your full name?" },
                    new() { ["field"] = "date_of_birth", ["text"] = "What is your date of birth?" },
                    new() { ["field"] = "phone_number",  ["text"] = "What is your phone number?" },
                    new() { ["field"] = "email",         ["text"] = "What is your email address?" },
                },
            },
            ["valid_steps"] = new List<string> { "symptoms" },
        },
        new()
        {
            ["name"]   = "symptoms",
            ["prompt"] = "Ask about the patient's current symptoms.",
            ["gather_info"] = new Dictionary<string, object>
            {
                ["output_key"] = "patient_symptoms",
                ["prompt"]     = "Now let's talk about why you're visiting today.",
                ["questions"]  = new List<Dictionary<string, object>>
                {
                    new() { ["field"] = "reason_for_visit",  ["text"] = "What is the main reason for your visit?" },
                    new() { ["field"] = "symptom_duration",  ["text"] = "How long have you had these symptoms?" },
                    new() { ["field"] = "pain_level",        ["text"] = "On a scale of 1-10, rate your discomfort." },
                },
            },
            ["valid_steps"] = new List<string> { "confirmation" },
        },
        new()
        {
            ["name"]     = "confirmation",
            ["prompt"]   = "Summarize all information and confirm with the patient that everything is correct.",
            ["criteria"] = "Patient has confirmed all information is correct",
        },
    },
});

Console.WriteLine("Starting Patient Intake Agent");
Console.WriteLine("Available at: http://localhost:3000/patient-intake");

agent.Run();
