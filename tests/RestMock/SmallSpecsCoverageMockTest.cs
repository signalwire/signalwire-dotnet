/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using SignalWire.REST.Namespaces;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Full success+error coverage for the small canonical spec groups (14 routes).
/// Translated 1:1 from
/// <c>signalwire-go/pkg/rest/namespaces/small_specs_coverage_mock_test.go</c>.
///
/// Routes covered:
///   project.create_token         POST   /api/project/tokens
///   project.update_token         PATCH  /api/project/tokens/{token_id}
///   project.delete_token         DELETE /api/project/tokens/{token_id}
///   voice.list_voice_logs        GET    /api/voice/logs
///   voice.get_voice_log          GET    /api/voice/logs/{id}
///   voice.list_voice_log_events  GET    /api/voice/logs/{id}/events
///   fax.list_fax_logs            GET    /api/fax/logs
///   fax.get_fax_log              GET    /api/fax/logs/{id}
///   message.list_message_logs    GET    /api/messaging/logs
///   message.get_message_log      GET    /api/messaging/logs/{id}
///   logs.list_conferences        GET    /api/logs/conferences
///   calling.call-commands        POST   /api/calling/calls
///   chat.create_chat_token       POST   /api/chat/tokens
///   pubsub.create_token          POST   /api/pubsub/tokens
/// </summary>
public class SmallSpecsCoverageMockTest : CoverageBase
{
    public SmallSpecsCoverageMockTest(MockServerFixture fixture) : base(fixture) { }

    private Logs NewLogs() => new(NewHttp());
    private Project NewProject() => new(NewHttp());
    private Calling NewCalling() => new(NewHttp(), Fixture.Project);
    private ChatResource NewChat() => new(NewHttp());
    private PubSubResource NewPubSub() => new(NewHttp());

    // ---------- project.create_token ----------

    [Fact]
    public async Task ProjectCreateToken_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewProject().Tokens.CreateAsync(new() { ["name"] = "tok-1" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/project/tokens", "project.create_token");
        Assert.Equal("tok-1", StringField(j, "name"));
    }

    [Fact]
    public async Task ProjectCreateToken_Error()
    {
        if (!Fixture.Available) return;
        var c = NewProject();
        var status = await AssertErrorAsync("project.create_token", 422,
            () => c.Tokens.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    // ---------- project.update_token (PATCH) ----------

    [Fact]
    public async Task ProjectUpdateToken_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewProject().Tokens.UpdateAsync("tok-7", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PATCH", "/api/project/tokens/tok-7", "project.update_token");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task ProjectUpdateToken_Error()
    {
        if (!Fixture.Available) return;
        var c = NewProject();
        var status = await AssertErrorAsync("project.update_token", 404,
            () => c.Tokens.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    // ---------- project.delete_token ----------

    [Fact]
    public async Task ProjectDeleteToken_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewProject().Tokens.DeleteAsync("tok-7");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/project/tokens/tok-7", "project.delete_token");
    }

    [Fact]
    public async Task ProjectDeleteToken_Error()
    {
        if (!Fixture.Available) return;
        var c = NewProject();
        var status = await AssertErrorAsync("project.delete_token", 404,
            () => c.Tokens.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    // ---------- voice.list_voice_logs ----------

    [Fact]
    public async Task VoiceListLogs_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewLogs().Voice.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", "/api/voice/logs", "voice.list_voice_logs");
    }

    [Fact]
    public async Task VoiceListLogs_Error()
    {
        if (!Fixture.Available) return;
        var c = NewLogs();
        var status = await AssertErrorAsync("voice.list_voice_logs", 500,
            () => c.Voice.ListAsync());
        Assert.Equal(500, status);
    }

    // ---------- voice.get_voice_log ----------

    [Fact]
    public async Task VoiceGetLog_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewLogs().Voice.GetAsync("vl-99");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/voice/logs/vl-99", "voice.get_voice_log");
    }

    [Fact]
    public async Task VoiceGetLog_Error()
    {
        if (!Fixture.Available) return;
        var c = NewLogs();
        var status = await AssertErrorAsync("voice.get_voice_log", 404,
            () => c.Voice.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    // ---------- voice.list_voice_log_events ----------

    [Fact]
    public async Task VoiceListLogEvents_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewLogs().Voice.ListEventsAsync("vl-99");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/voice/logs/vl-99/events", "voice.list_voice_log_events");
    }

    [Fact]
    public async Task VoiceListLogEvents_Error()
    {
        if (!Fixture.Available) return;
        var c = NewLogs();
        var status = await AssertErrorAsync("voice.list_voice_log_events", 404,
            () => c.Voice.ListEventsAsync("missing"));
        Assert.Equal(404, status);
    }

    // ---------- fax.list_fax_logs ----------

    [Fact]
    public async Task FaxListLogs_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewLogs().Fax.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fax/logs", "fax.list_fax_logs");
    }

    [Fact]
    public async Task FaxListLogs_Error()
    {
        if (!Fixture.Available) return;
        var c = NewLogs();
        var status = await AssertErrorAsync("fax.list_fax_logs", 500,
            () => c.Fax.ListAsync());
        Assert.Equal(500, status);
    }

    // ---------- fax.get_fax_log ----------

    [Fact]
    public async Task FaxGetLog_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewLogs().Fax.GetAsync("fl-7");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fax/logs/fl-7", "fax.get_fax_log");
    }

    [Fact]
    public async Task FaxGetLog_Error()
    {
        if (!Fixture.Available) return;
        var c = NewLogs();
        var status = await AssertErrorAsync("fax.get_fax_log", 404,
            () => c.Fax.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    // ---------- message.list_message_logs ----------

    [Fact]
    public async Task MessageListLogs_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewLogs().Messages.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", "/api/messaging/logs", "message.list_message_logs");
    }

    [Fact]
    public async Task MessageListLogs_Error()
    {
        if (!Fixture.Available) return;
        var c = NewLogs();
        var status = await AssertErrorAsync("message.list_message_logs", 500,
            () => c.Messages.ListAsync());
        Assert.Equal(500, status);
    }

    // ---------- message.get_message_log ----------

    [Fact]
    public async Task MessageGetLog_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewLogs().Messages.GetAsync("ml-42");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/messaging/logs/ml-42", "message.get_message_log");
    }

    [Fact]
    public async Task MessageGetLog_Error()
    {
        if (!Fixture.Available) return;
        var c = NewLogs();
        var status = await AssertErrorAsync("message.get_message_log", 404,
            () => c.Messages.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    // ---------- logs.list_conferences ----------

    [Fact]
    public async Task LogsListConferences_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewLogs().Conferences.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", "/api/logs/conferences", "logs.list_conferences");
    }

    [Fact]
    public async Task LogsListConferences_Error()
    {
        if (!Fixture.Available) return;
        var c = NewLogs();
        var status = await AssertErrorAsync("logs.list_conferences", 500,
            () => c.Conferences.ListAsync());
        Assert.Equal(500, status);
    }

    // ---------- calling.call-commands ----------

    [Fact]
    public async Task CallingDial_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCalling().DialAsync(new()
        {
            ["url"] = "https://example.com/swml",
            ["to"] = "+15551234567",
        });
        Assert.True(body.ContainsKey("id"));
        var j = AssertRoute("POST", "/api/calling/calls", "calling.call-commands");
        Assert.Equal("dial", StringField(j, "command"));
    }

    [Fact]
    public async Task CallingDial_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCalling();
        var status = await AssertErrorAsync("calling.call-commands", 422,
            () => c.DialAsync(new() { ["url"] = "https://example.com/swml", ["to"] = "+15551234567" }));
        Assert.Equal(422, status);
    }

    // ---------- chat.create_chat_token ----------

    [Fact]
    public async Task ChatCreateToken_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewChat().CreateTokenAsync(new()
        {
            ["channels"] = new Dictionary<string, object?>
            {
                ["room"] = new Dictionary<string, object?> { ["read"] = true },
            },
        });
        Assert.NotNull(body);
        AssertRoute("POST", "/api/chat/tokens", "chat.create_chat_token");
    }

    [Fact]
    public async Task ChatCreateToken_Error()
    {
        if (!Fixture.Available) return;
        var c = NewChat();
        var status = await AssertErrorAsync("chat.create_chat_token", 422,
            () => c.CreateTokenAsync(new()));
        Assert.Equal(422, status);
    }

    // ---------- pubsub.create_token ----------

    [Fact]
    public async Task PubSubCreateToken_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewPubSub().CreateTokenAsync(new()
        {
            ["channels"] = new Dictionary<string, object?>
            {
                ["updates"] = new Dictionary<string, object?> { ["read"] = true },
            },
        });
        Assert.NotNull(body);
        AssertRoute("POST", "/api/pubsub/tokens", "pubsub.create_token");
    }

    [Fact]
    public async Task PubSubCreateToken_Error()
    {
        if (!Fixture.Available) return;
        var c = NewPubSub();
        var status = await AssertErrorAsync("pubsub.create_token", 422,
            () => c.CreateTokenAsync(new()));
        Assert.Equal(422, status);
    }
}
