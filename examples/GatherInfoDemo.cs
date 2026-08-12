// Gather Info Mode Demo
//
// Demonstrates the contexts system's gather_info mode for structured
// data collection using the low-level contexts API with steps.

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name = "Patient Intake Agent",
    Route = "/patient-intake",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a friendly medical office intake assistant. "
    + "Collect patient information accurately and professionally."
);

// Define contexts with gather info steps. `SetGatherInfo` configures the gather
// block (output_key / prompt); each question is added with `AddGatherQuestion`
// and is keyed by `key` (the field the answer lands under in the output object)
// and `question` (the text put to the caller).
var ctx = agent.DefineContexts();

var intake = ctx.AddContext("default");

intake.AddStep("demographics")
    .SetText("Collect the patient's basic information.")
    .SetGatherInfo(new Dictionary<string, object>
    {
        ["output_key"] = "patient_demographics",
        ["prompt"] = "Please collect the following patient information.",
    })
    .AddGatherQuestion(new Dictionary<string, object> { ["key"] = "full_name", ["question"] = "What is your full name?" })
    .AddGatherQuestion(new Dictionary<string, object> { ["key"] = "date_of_birth", ["question"] = "What is your date of birth?" })
    .AddGatherQuestion(new Dictionary<string, object> { ["key"] = "phone_number", ["question"] = "What is your phone number?" })
    .AddGatherQuestion(new Dictionary<string, object> { ["key"] = "email", ["question"] = "What is your email address?" })
    .SetValidSteps(new List<string> { "symptoms" });

intake.AddStep("symptoms")
    .SetText("Ask about the patient's current symptoms.")
    .SetGatherInfo(new Dictionary<string, object>
    {
        ["output_key"] = "patient_symptoms",
        ["prompt"] = "Now let's talk about why you're visiting today.",
    })
    .AddGatherQuestion(new Dictionary<string, object> { ["key"] = "reason_for_visit", ["question"] = "What is the main reason for your visit?" })
    .AddGatherQuestion(new Dictionary<string, object> { ["key"] = "symptom_duration", ["question"] = "How long have you had these symptoms?" })
    .AddGatherQuestion(new Dictionary<string, object> { ["key"] = "pain_level", ["question"] = "On a scale of 1-10, rate your discomfort." })
    .SetValidSteps(new List<string> { "confirmation" });

intake.AddStep("confirmation")
    .SetText("Summarize all information and confirm with the patient that everything is correct.")
    .SetStepCriteria("Patient has confirmed all information is correct");

Console.WriteLine("Starting Patient Intake Agent");
Console.WriteLine("Available at: http://localhost:3000/patient-intake");

agent.Run();
