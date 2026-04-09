using Xunit;
using SignalWire.Contexts;

namespace SignalWire.Tests;

public class ContextsTests : IDisposable
{
    public ContextsTests() { }
    public void Dispose() { }

    // =================================================================
    //  GatherQuestion
    // =================================================================

    [Fact]
    public void GatherQuestion_ConstructionAndToDict()
    {
        var q = new GatherQuestion(new Dictionary<string, object>
        {
            ["key"] = "name",
            ["question"] = "What is your name?",
        });

        Assert.Equal("name", q.Key);
        var dict = q.ToDict();
        Assert.Equal("name", dict["key"]);
        Assert.Equal("What is your name?", dict["question"]);
        Assert.False(dict.ContainsKey("type"));
        Assert.False(dict.ContainsKey("confirm"));
        Assert.False(dict.ContainsKey("prompt"));
        Assert.False(dict.ContainsKey("functions"));
    }

    [Fact]
    public void GatherQuestion_WithAllOptions()
    {
        var q = new GatherQuestion(new Dictionary<string, object>
        {
            ["key"] = "age",
            ["question"] = "How old are you?",
            ["type"] = "integer",
            ["confirm"] = true,
            ["prompt"] = "Confirm age",
            ["functions"] = new List<string> { "validate_age" },
        });

        var dict = q.ToDict();
        Assert.Equal("age", dict["key"]);
        Assert.Equal("How old are you?", dict["question"]);
        Assert.Equal("integer", dict["type"]);
        Assert.True((bool)dict["confirm"]);
        Assert.Equal("Confirm age", dict["prompt"]);
        Assert.Equal(new List<string> { "validate_age" }, dict["functions"]);
    }

    [Fact]
    public void GatherQuestion_DefaultTypeOmitted()
    {
        var q = new GatherQuestion(new Dictionary<string, object>
        {
            ["key"] = "x", ["question"] = "Q?", ["type"] = "string",
        });
        Assert.False(q.ToDict().ContainsKey("type"));
    }

    [Fact]
    public void GatherQuestion_NonDefaultTypeIncluded()
    {
        var q = new GatherQuestion(new Dictionary<string, object>
        {
            ["key"] = "x", ["question"] = "Q?", ["type"] = "boolean",
        });
        Assert.Equal("boolean", q.ToDict()["type"]);
    }

    // =================================================================
    //  GatherInfo
    // =================================================================

    [Fact]
    public void GatherInfo_AddQuestionAndToDict()
    {
        var gi = new GatherInfo("result_key", "next_step", "Please answer");
        gi.AddQuestion(new Dictionary<string, object> { ["key"] = "color", ["question"] = "Favorite color?" });
        gi.AddQuestion(new Dictionary<string, object> { ["key"] = "food", ["question"] = "Favorite food?" });

        Assert.Equal(2, gi.Questions.Count);
        Assert.Equal("next_step", gi.CompletionAction);

        var dict = gi.ToDict();
        var questions = (List<Dictionary<string, object>>)dict["questions"];
        Assert.Equal(2, questions.Count);
        Assert.Equal("color", questions[0]["key"]);
        Assert.Equal("food", questions[1]["key"]);
        Assert.Equal("Please answer", dict["prompt"]);
        Assert.Equal("result_key", dict["output_key"]);
        Assert.Equal("next_step", dict["completion_action"]);
    }

    [Fact]
    public void GatherInfo_DefaultsOmitOptionalFields()
    {
        var gi = new GatherInfo();
        gi.AddQuestion(new Dictionary<string, object> { ["key"] = "k", ["question"] = "Q?" });

        var dict = gi.ToDict();
        Assert.False(dict.ContainsKey("prompt"));
        Assert.False(dict.ContainsKey("output_key"));
        Assert.False(dict.ContainsKey("completion_action"));
    }

    [Fact]
    public void GatherInfo_ChainsAddQuestion()
    {
        var gi = new GatherInfo();
        var result = gi.AddQuestion(new Dictionary<string, object> { ["key"] = "a", ["question"] = "A?" });
        Assert.Same(gi, result);
    }

    // =================================================================
    //  Step
    // =================================================================

    [Fact]
    public void Step_SetTextAndToDict()
    {
        var step = new Step("greeting");
        step.SetText("Hello, welcome!");
        var dict = step.ToDict();
        Assert.Equal("greeting", dict["name"]);
        Assert.Equal("Hello, welcome!", dict["text"]);
    }

    [Fact]
    public void Step_AddSection_PomRendering()
    {
        var step = new Step("intro");
        step.AddSection("Greeting", "Say hello to the caller.");
        step.AddSection("Instructions", "Ask for their name.");

        var dict = step.ToDict();
        var text = (string)dict["text"];
        Assert.Contains("## Greeting", text);
        Assert.Contains("Say hello to the caller.", text);
        Assert.Contains("## Instructions", text);
        Assert.Contains("Ask for their name.", text);
    }

    [Fact]
    public void Step_AddBullets()
    {
        var step = new Step("rules");
        step.AddBullets("Rules", ["Be polite", "Be concise"]);

        var dict = step.ToDict();
        var text = (string)dict["text"];
        Assert.Contains("## Rules", text);
        Assert.Contains("- Be polite", text);
        Assert.Contains("- Be concise", text);
    }

    [Fact]
    public void Step_ClearSections()
    {
        var step = new Step("s");
        step.SetText("initial");
        step.ClearSections();
        step.AddSection("Fresh", "New content");
        var text = (string)step.ToDict()["text"];
        Assert.Contains("## Fresh", text);
    }

    [Fact]
    public void Step_ClearSections_RemovesBothTextAndSections()
    {
        var step = new Step("s");
        step.AddSection("Title", "Body");
        step.ClearSections();
        step.SetText("Plain text");
        Assert.Equal("Plain text", step.ToDict()["text"]);
    }

    [Fact]
    public void Step_SetTextAfterSections_Throws()
    {
        var step = new Step("s");
        step.AddSection("T", "B");
        Assert.Throws<InvalidOperationException>(() => step.SetText("conflict"));
    }

    [Fact]
    public void Step_AddSectionAfterSetText_Throws()
    {
        var step = new Step("s");
        step.SetText("plain");
        Assert.Throws<InvalidOperationException>(() => step.AddSection("T", "B"));
    }

    [Fact]
    public void Step_AddBulletsAfterSetText_Throws()
    {
        var step = new Step("s");
        step.SetText("plain");
        Assert.Throws<InvalidOperationException>(() => step.AddBullets("T", ["a"]));
    }

    [Fact]
    public void Step_SetStepCriteria()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetStepCriteria("User wants to order");
        Assert.Equal("User wants to order", step.ToDict()["step_criteria"]);
    }

    [Fact]
    public void Step_SetFunctions_String()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetFunctions("none");
        Assert.Equal("none", step.ToDict()["functions"]);
    }

    [Fact]
    public void Step_SetFunctions_List()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetFunctions(new List<string> { "lookup_user", "get_balance" });
        Assert.Equal(new List<string> { "lookup_user", "get_balance" }, step.ToDict()["functions"]);
    }

    [Fact]
    public void Step_SetValidSteps()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetValidSteps(["step_a", "step_b"]);
        Assert.Equal(new List<string> { "step_a", "step_b" }, step.ToDict()["valid_steps"]);
        Assert.Equal(new List<string> { "step_a", "step_b" }, step.ValidSteps);
    }

    [Fact]
    public void Step_SetValidContexts()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetValidContexts(["billing", "support"]);
        Assert.Equal(new List<string> { "billing", "support" }, step.ToDict()["valid_contexts"]);
        Assert.Equal(new List<string> { "billing", "support" }, step.ValidContexts);
    }

    [Fact]
    public void Step_SetEnd()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetEnd(true);
        Assert.True((bool)step.ToDict()["end"]);
    }

    [Fact]
    public void Step_SetEnd_FalseOmitted()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetEnd(false);
        Assert.False(step.ToDict().ContainsKey("end"));
    }

    [Fact]
    public void Step_SetSkipUserTurn()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetSkipUserTurn(true);
        Assert.True((bool)step.ToDict()["skip_user_turn"]);
    }

    [Fact]
    public void Step_SetSkipUserTurn_FalseOmitted()
    {
        var step = new Step("s");
        step.SetText("text");
        Assert.False(step.ToDict().ContainsKey("skip_user_turn"));
    }

    [Fact]
    public void Step_SetSkipToNextStep()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetSkipToNextStep(true);
        Assert.True((bool)step.ToDict()["skip_to_next_step"]);
    }

    [Fact]
    public void Step_SetSkipToNextStep_FalseOmitted()
    {
        var step = new Step("s");
        step.SetText("text");
        Assert.False(step.ToDict().ContainsKey("skip_to_next_step"));
    }

    [Fact]
    public void Step_SetGatherInfo()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetGatherInfo(new Dictionary<string, object>
        {
            ["output_key"] = "info", ["completion_action"] = "next_step", ["prompt"] = "Answer these questions",
        });
        step.AddGatherQuestion(new Dictionary<string, object> { ["key"] = "name", ["question"] = "Your name?" });

        var dict = step.ToDict();
        var gi = (Dictionary<string, object>)dict["gather_info"];
        Assert.Equal("info", gi["output_key"]);
        Assert.Equal("next_step", gi["completion_action"]);
        Assert.Equal("Answer these questions", gi["prompt"]);
        var questions = (List<Dictionary<string, object>>)gi["questions"];
        Assert.Single(questions);
    }

    [Fact]
    public void Step_AddGatherQuestion_AutoInitializes()
    {
        var step = new Step("s");
        step.SetText("text");
        step.AddGatherQuestion(new Dictionary<string, object> { ["key"] = "email", ["question"] = "Your email?" });
        Assert.NotNull(step.GatherInfoData);
        Assert.Single(step.GatherInfoData.Questions);
    }

    [Fact]
    public void Step_ToDict_FullSerialization()
    {
        var step = new Step("full_step");
        step.SetText("Do the thing");
        step.SetStepCriteria("When user asks");
        step.SetFunctions(new List<string> { "fn_a" });
        step.SetValidSteps(["next"]);
        step.SetValidContexts(["other"]);
        step.SetEnd(true);
        step.SetSkipUserTurn(true);
        step.SetSkipToNextStep(true);

        var dict = step.ToDict();
        Assert.Equal("full_step", dict["name"]);
        Assert.Equal("Do the thing", dict["text"]);
        Assert.Equal("When user asks", dict["step_criteria"]);
        Assert.True((bool)dict["end"]);
        Assert.True((bool)dict["skip_user_turn"]);
        Assert.True((bool)dict["skip_to_next_step"]);
    }

    [Fact]
    public void Step_MinimalToDict_OmitsOptionalKeys()
    {
        var step = new Step("minimal");
        step.SetText("Just text");
        var dict = step.ToDict();
        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey("name"));
        Assert.True(dict.ContainsKey("text"));
    }

    [Fact]
    public void Step_ResetFields()
    {
        var step = new Step("s");
        step.SetText("text");
        step.SetResetSystemPrompt("New system prompt");
        step.SetResetUserPrompt("New user prompt");
        step.SetResetConsolidate(true);
        step.SetResetFullReset(true);

        var dict = step.ToDict();
        var reset = (Dictionary<string, object>)dict["reset"];
        Assert.Equal("New system prompt", reset["system_prompt"]);
        Assert.Equal("New user prompt", reset["user_prompt"]);
        Assert.True((bool)reset["consolidate"]);
        Assert.True((bool)reset["full_reset"]);
    }

    [Fact]
    public void Step_NoResetWhenNotSet()
    {
        var step = new Step("s");
        step.SetText("text");
        Assert.False(step.ToDict().ContainsKey("reset"));
    }

    // =================================================================
    //  Context
    // =================================================================

    [Fact]
    public void Context_AddStepAndGetStep()
    {
        var ctx = new Context("default");
        var step = ctx.AddStep("greeting", new Dictionary<string, object> { ["text"] = "Hello" });

        Assert.IsType<Step>(step);
        Assert.Same(step, ctx.GetStep("greeting"));
        Assert.Null(ctx.GetStep("nonexistent"));
    }

    [Fact]
    public void Context_AddStepWithShorthandOpts()
    {
        var ctx = new Context("default");
        var step = ctx.AddStep("s1", new Dictionary<string, object>
        {
            ["text"] = "Hello",
            ["step_criteria"] = "always",
            ["functions"] = new List<string> { "fn1" },
            ["valid_steps"] = new List<string> { "s2" },
            ["valid_contexts"] = new List<string> { "other" },
        });
        var dict = step.ToDict();
        Assert.Equal("Hello", dict["text"]);
        Assert.Equal("always", dict["step_criteria"]);
    }

    [Fact]
    public void Context_DuplicateStep_Throws()
    {
        var ctx = new Context("default");
        ctx.AddStep("greeting", new Dictionary<string, object> { ["text"] = "Hi" });
        var ex = Assert.Throws<InvalidOperationException>(
            () => ctx.AddStep("greeting", new Dictionary<string, object> { ["text"] = "Hi again" }));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public void Context_RemoveStep()
    {
        var ctx = new Context("default");
        ctx.AddStep("a", new Dictionary<string, object> { ["text"] = "A" });
        ctx.AddStep("b", new Dictionary<string, object> { ["text"] = "B" });
        ctx.RemoveStep("a");

        Assert.Null(ctx.GetStep("a"));
        Assert.NotNull(ctx.GetStep("b"));
        Assert.Equal(["b"], ctx.GetStepOrder());
    }

    [Fact]
    public void Context_RemoveNonexistentStep_IsNoop()
    {
        var ctx = new Context("default");
        ctx.AddStep("a", new Dictionary<string, object> { ["text"] = "A" });
        ctx.RemoveStep("nonexistent");
        Assert.Single(ctx.GetSteps());
    }

    [Fact]
    public void Context_MoveStep()
    {
        var ctx = new Context("default");
        ctx.AddStep("a", new Dictionary<string, object> { ["text"] = "A" });
        ctx.AddStep("b", new Dictionary<string, object> { ["text"] = "B" });
        ctx.AddStep("c", new Dictionary<string, object> { ["text"] = "C" });
        ctx.MoveStep("c", 0);
        Assert.Equal(new List<string> { "c", "a", "b" }, ctx.GetStepOrder());
    }

    [Fact]
    public void Context_MoveStep_ToMiddle()
    {
        var ctx = new Context("default");
        ctx.AddStep("a", new Dictionary<string, object> { ["text"] = "A" });
        ctx.AddStep("b", new Dictionary<string, object> { ["text"] = "B" });
        ctx.AddStep("c", new Dictionary<string, object> { ["text"] = "C" });
        ctx.MoveStep("a", 1);
        Assert.Equal(new List<string> { "b", "a", "c" }, ctx.GetStepOrder());
    }

    [Fact]
    public void Context_MoveNonexistentStep_Throws()
    {
        var ctx = new Context("default");
        ctx.AddStep("a", new Dictionary<string, object> { ["text"] = "A" });
        Assert.Throws<InvalidOperationException>(() => ctx.MoveStep("z", 0));
    }

    // -- Prompt modes --

    [Fact]
    public void Context_PromptTextMode()
    {
        var ctx = new Context("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Step text" });
        ctx.SetPrompt("You are a helpful assistant.");
        Assert.Equal("You are a helpful assistant.", ctx.ToDict()["prompt"]);
    }

    [Fact]
    public void Context_PromptPomMode()
    {
        var ctx = new Context("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Step text" });
        ctx.AddSection("Role", "You are a concierge.");
        ctx.AddBullets("Rules", ["Be kind", "Be brief"]);

        var prompt = (string)ctx.ToDict()["prompt"];
        Assert.Contains("## Role", prompt);
        Assert.Contains("You are a concierge.", prompt);
        Assert.Contains("- Be kind", prompt);
    }

    [Fact]
    public void Context_SetPromptAfterSections_Throws()
    {
        var ctx = new Context("default");
        ctx.AddSection("T", "B");
        Assert.Throws<InvalidOperationException>(() => ctx.SetPrompt("conflict"));
    }

    [Fact]
    public void Context_AddSectionAfterSetPrompt_Throws()
    {
        var ctx = new Context("default");
        ctx.SetPrompt("text");
        Assert.Throws<InvalidOperationException>(() => ctx.AddSection("T", "B"));
    }

    [Fact]
    public void Context_AddBulletsAfterSetPrompt_Throws()
    {
        var ctx = new Context("default");
        ctx.SetPrompt("text");
        Assert.Throws<InvalidOperationException>(() => ctx.AddBullets("T", ["a"]));
    }

    // -- System prompt --

    [Fact]
    public void Context_SystemPromptText()
    {
        var ctx = new Context("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        ctx.SetSystemPrompt("System instructions here.");
        Assert.Equal("System instructions here.", ctx.ToDict()["system_prompt"]);
    }

    [Fact]
    public void Context_SystemPromptPom()
    {
        var ctx = new Context("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        ctx.AddSystemSection("Behavior", "Be professional.");
        ctx.AddSystemBullets("Constraints", ["No profanity"]);

        var sp = (string)ctx.ToDict()["system_prompt"];
        Assert.Contains("## Behavior", sp);
        Assert.Contains("Be professional.", sp);
        Assert.Contains("- No profanity", sp);
    }

    [Fact]
    public void Context_SystemPromptConflicts()
    {
        var ctx = new Context("default");
        ctx.SetSystemPrompt("plain");
        Assert.Throws<InvalidOperationException>(() => ctx.AddSystemSection("T", "B"));
    }

    [Fact]
    public void Context_SystemBulletsConflict()
    {
        var ctx = new Context("default");
        ctx.SetSystemPrompt("plain");
        Assert.Throws<InvalidOperationException>(() => ctx.AddSystemBullets("T", ["a"]));
    }

    // -- Fillers --

    [Fact]
    public void Context_EnterFillers()
    {
        var ctx = new Context("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        ctx.SetEnterFillers(new Dictionary<string, List<string>> { ["en"] = ["Please wait", "One moment"] });
        var dict = ctx.ToDict();
        var fillers = (Dictionary<string, List<string>>)dict["enter_fillers"];
        Assert.Equal(["Please wait", "One moment"], fillers["en"]);
    }

    [Fact]
    public void Context_AddEnterFiller()
    {
        var ctx = new Context("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        ctx.AddEnterFiller("en", "Hold on");
        ctx.AddEnterFiller("en", "Just a sec");
        ctx.AddEnterFiller("es", "Un momento");

        var fillers = (Dictionary<string, List<string>>)ctx.ToDict()["enter_fillers"];
        Assert.Equal(["Hold on", "Just a sec"], fillers["en"]);
        Assert.Equal(["Un momento"], fillers["es"]);
    }

    [Fact]
    public void Context_AddExitFiller()
    {
        var ctx = new Context("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        ctx.AddExitFiller("en", "Bye");
        ctx.AddExitFiller("en", "See you");
        var fillers = (Dictionary<string, List<string>>)ctx.ToDict()["exit_fillers"];
        Assert.Equal(["Bye", "See you"], fillers["en"]);
    }

    [Fact]
    public void Context_FillersOmittedWhenNotSet()
    {
        var ctx = new Context("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        var dict = ctx.ToDict();
        Assert.False(dict.ContainsKey("enter_fillers"));
        Assert.False(dict.ContainsKey("exit_fillers"));
    }

    // -- Context toDict --

    [Fact]
    public void Context_ToDict_StepOrdering()
    {
        var ctx = new Context("default");
        ctx.AddStep("first", new Dictionary<string, object> { ["text"] = "First" });
        ctx.AddStep("second", new Dictionary<string, object> { ["text"] = "Second" });
        ctx.AddStep("third", new Dictionary<string, object> { ["text"] = "Third" });

        var steps = (List<Dictionary<string, object>>)ctx.ToDict()["steps"];
        Assert.Equal("first", steps[0]["name"]);
        Assert.Equal("second", steps[1]["name"]);
        Assert.Equal("third", steps[2]["name"]);
    }

    [Fact]
    public void Context_ToDict_OmitsUnsetOptionalFields()
    {
        var ctx = new Context("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Hi" });
        var dict = ctx.ToDict();
        Assert.True(dict.ContainsKey("steps"));
        Assert.False(dict.ContainsKey("prompt"));
        Assert.False(dict.ContainsKey("system_prompt"));
        Assert.False(dict.ContainsKey("post_prompt"));
        Assert.False(dict.ContainsKey("user_prompt"));
        Assert.False(dict.ContainsKey("consolidate"));
        Assert.False(dict.ContainsKey("full_reset"));
        Assert.False(dict.ContainsKey("isolated"));
        Assert.False(dict.ContainsKey("valid_contexts"));
        Assert.False(dict.ContainsKey("valid_steps"));
    }

    // =================================================================
    //  ContextBuilder
    // =================================================================

    [Fact]
    public void ContextBuilder_AddContextAndGetContext()
    {
        var builder = new ContextBuilder();
        var ctx = builder.AddContext("default");
        Assert.IsType<Context>(ctx);
        Assert.Same(ctx, builder.GetContext("default"));
        Assert.Null(builder.GetContext("nonexistent"));
    }

    [Fact]
    public void ContextBuilder_HasContexts()
    {
        var builder = new ContextBuilder();
        Assert.False(builder.HasContexts());
        builder.AddContext("default");
        Assert.True(builder.HasContexts());
    }

    [Fact]
    public void ContextBuilder_DuplicateContext_Throws()
    {
        var builder = new ContextBuilder();
        builder.AddContext("default");
        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddContext("default"));
        Assert.Contains("already exists", ex.Message);
    }

    // -- Validate --

    [Fact]
    public void Validate_SingleContextMustBeDefault()
    {
        var builder = new ContextBuilder();
        var ctx = builder.AddContext("custom");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Hello" });
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("must be named 'default'", errors[0]);
    }

    [Fact]
    public void Validate_SingleContextNamedDefaultPasses()
    {
        var builder = new ContextBuilder();
        builder.AddContext("default").AddStep("s", new Dictionary<string, object> { ["text"] = "Hello" });
        Assert.Empty(builder.Validate());
    }

    [Fact]
    public void Validate_EmptyContextRejected()
    {
        var builder = new ContextBuilder();
        builder.AddContext("default");
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("must have at least one step", errors[0]);
    }

    [Fact]
    public void Validate_NoContextsRejectsEmpty()
    {
        var builder = new ContextBuilder();
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("At least one context", errors[0]);
    }

    [Fact]
    public void Validate_MultipleContextsNonDefaultAllowed()
    {
        var builder = new ContextBuilder();
        builder.AddContext("billing").AddStep("s", new Dictionary<string, object> { ["text"] = "Billing" });
        builder.AddContext("support").AddStep("s", new Dictionary<string, object> { ["text"] = "Support" });
        Assert.Empty(builder.Validate());
    }

    [Fact]
    public void Validate_InvalidStepReference()
    {
        var builder = new ContextBuilder();
        builder.AddContext("default").AddStep("s1", new Dictionary<string, object> { ["text"] = "Step 1" })
            .SetValidSteps(["nonexistent"]);
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("unknown step 'nonexistent'", errors[0]);
    }

    [Fact]
    public void Validate_NextStepReferenceIsAllowed()
    {
        var builder = new ContextBuilder();
        builder.AddContext("default").AddStep("s1", new Dictionary<string, object> { ["text"] = "Step 1" })
            .SetValidSteps(["next"]);
        Assert.Empty(builder.Validate());
    }

    [Fact]
    public void Validate_InvalidContextReference_AtContextLevel()
    {
        var builder = new ContextBuilder();
        var ctx = builder.AddContext("default");
        ctx.AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        ctx.SetValidContexts(["ghost"]);
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("unknown context 'ghost'", errors[0]);
    }

    [Fact]
    public void Validate_InvalidContextReference_AtStepLevel()
    {
        var builder = new ContextBuilder();
        builder.AddContext("default").AddStep("s", new Dictionary<string, object> { ["text"] = "Step" })
            .SetValidContexts(["ghost"]);
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("unknown context 'ghost'", errors[0]);
    }

    [Fact]
    public void Validate_GatherInfoNoQuestions()
    {
        var builder = new ContextBuilder();
        var step = builder.AddContext("default").AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        step.SetGatherInfo(new Dictionary<string, object> { ["output_key"] = "info" });
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("no questions", errors[0]);
    }

    [Fact]
    public void Validate_GatherInfoDuplicateKeys()
    {
        var builder = new ContextBuilder();
        var step = builder.AddContext("default").AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        step.AddGatherQuestion(new Dictionary<string, object> { ["key"] = "name", ["question"] = "Name?" });
        step.AddGatherQuestion(new Dictionary<string, object> { ["key"] = "name", ["question"] = "Name again?" });
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("duplicate gather_info question key 'name'", errors[0]);
    }

    [Fact]
    public void Validate_GatherInfoCompletionActionNextStepLastStep()
    {
        var builder = new ContextBuilder();
        var step = builder.AddContext("default").AddStep("last", new Dictionary<string, object> { ["text"] = "Step" });
        step.SetGatherInfo(new Dictionary<string, object> { ["completion_action"] = "next_step" });
        step.AddGatherQuestion(new Dictionary<string, object> { ["key"] = "q", ["question"] = "Q?" });
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("next_step", errors[0]);
        Assert.Contains("last step", errors[0]);
    }

    [Fact]
    public void Validate_GatherInfoCompletionActionReferencesUnknownStep()
    {
        var builder = new ContextBuilder();
        var step = builder.AddContext("default").AddStep("s", new Dictionary<string, object> { ["text"] = "Step" });
        step.SetGatherInfo(new Dictionary<string, object> { ["completion_action"] = "unknown_step" });
        step.AddGatherQuestion(new Dictionary<string, object> { ["key"] = "q", ["question"] = "Q?" });
        var errors = builder.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains("unknown_step", errors[0]);
        Assert.Contains("is not a step", errors[0]);
    }

    // -- ContextBuilder toDict --

    [Fact]
    public void ContextBuilder_ToDict()
    {
        var builder = new ContextBuilder();
        var ctx = builder.AddContext("default");
        ctx.AddStep("greeting", new Dictionary<string, object> { ["text"] = "Hello!" });
        ctx.SetPrompt("Be helpful");

        var dict = builder.ToDict();
        Assert.True(dict.ContainsKey("default"));
        var ctxDict = (Dictionary<string, object>)dict["default"];
        var steps = (List<Dictionary<string, object>>)ctxDict["steps"];
        Assert.Single(steps);
        Assert.Equal("greeting", steps[0]["name"]);
        Assert.Equal("Be helpful", ctxDict["prompt"]);
    }

    [Fact]
    public void ContextBuilder_ToDict_PreservesOrder()
    {
        var builder = new ContextBuilder();
        builder.AddContext("billing").AddStep("s", new Dictionary<string, object> { ["text"] = "Bill" });
        builder.AddContext("support").AddStep("s", new Dictionary<string, object> { ["text"] = "Sup" });
        builder.AddContext("default").AddStep("s", new Dictionary<string, object> { ["text"] = "Main" });

        var keys = builder.ToDict().Keys.ToList();
        Assert.Equal(["billing", "support", "default"], keys);
    }

    [Fact]
    public void ContextBuilder_ToDict_ThrowsOnValidationFailure()
    {
        var builder = new ContextBuilder();
        builder.AddContext("default");
        var ex = Assert.Throws<InvalidOperationException>(() => builder.ToDict());
        Assert.Contains("Validation failed", ex.Message);
    }

    // -- createSimpleContext --

    [Fact]
    public void CreateSimpleContext()
    {
        var builder = ContextBuilder.CreateSimpleContext("default");
        Assert.IsType<ContextBuilder>(builder);
        Assert.True(builder.HasContexts());
        Assert.NotNull(builder.GetContext("default"));
    }

    [Fact]
    public void CreateSimpleContext_CanBeUsedEndToEnd()
    {
        var builder = ContextBuilder.CreateSimpleContext("default");
        builder.GetContext("default")!.AddStep("greet", new Dictionary<string, object> { ["text"] = "Hi there" });
        var dict = builder.ToDict();
        Assert.True(dict.ContainsKey("default"));
        var ctxDict = (Dictionary<string, object>)dict["default"];
        var steps = (List<Dictionary<string, object>>)ctxDict["steps"];
        Assert.Equal("greet", steps[0]["name"]);
    }

    // -- Method chaining --

    [Fact]
    public void Context_MethodChaining()
    {
        var ctx = new Context("default");
        Assert.Same(ctx, ctx.SetPrompt("text"));

        var ctx2 = new Context("c2");
        Assert.Same(ctx2, ctx2.SetSystemPrompt("sys"));
        Assert.Same(ctx2, ctx2.SetPostPrompt("post"));
        Assert.Same(ctx2, ctx2.SetConsolidate(true));
        Assert.Same(ctx2, ctx2.SetFullReset(true));
        Assert.Same(ctx2, ctx2.SetUserPrompt("u"));
        Assert.Same(ctx2, ctx2.SetIsolated(true));
        Assert.Same(ctx2, ctx2.SetValidContexts([]));
        Assert.Same(ctx2, ctx2.SetValidSteps([]));
        Assert.Same(ctx2, ctx2.SetEnterFillers(new Dictionary<string, List<string>>()));
        Assert.Same(ctx2, ctx2.SetExitFillers(new Dictionary<string, List<string>>()));
        Assert.Same(ctx2, ctx2.RemoveStep("nope"));
    }

    [Fact]
    public void Step_MethodChaining()
    {
        var step = new Step("s");
        Assert.Same(step, step.SetText("t"));
        step.ClearSections();
        Assert.Same(step, step.AddSection("T", "B"));
        step.ClearSections();
        Assert.Same(step, step.AddBullets("T", ["a"]));
        step.ClearSections();
        step.SetText("t");
        Assert.Same(step, step.SetStepCriteria("c"));
        Assert.Same(step, step.SetFunctions("none"));
        Assert.Same(step, step.SetValidSteps([]));
        Assert.Same(step, step.SetValidContexts([]));
        Assert.Same(step, step.SetEnd(true));
        Assert.Same(step, step.SetSkipUserTurn(true));
        Assert.Same(step, step.SetSkipToNextStep(true));
        Assert.Same(step, step.SetGatherInfo(new Dictionary<string, object>()));
        Assert.Same(step, step.AddGatherQuestion(new Dictionary<string, object> { ["key"] = "k", ["question"] = "Q?" }));
        Assert.Same(step, step.ClearSections());
        Assert.Same(step, step.SetResetSystemPrompt("sp"));
        Assert.Same(step, step.SetResetUserPrompt("up"));
        Assert.Same(step, step.SetResetConsolidate(true));
        Assert.Same(step, step.SetResetFullReset(true));
    }
}
