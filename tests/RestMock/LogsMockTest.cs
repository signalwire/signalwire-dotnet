/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using SignalWire.REST;
using SignalWire.REST.Namespaces.Generated;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Mock-backed behavioral tests for the generated Logs namespace (messages,
/// voice, fax, conferences) — exercises path construction + response parsing
/// through the code-generated resource tree.
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_logs_mock.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class LogsMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public LogsMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private LogsNamespace NewLogs()
    {
        var http = _fixture.NewHttp();
        return new LogsNamespace(http);
    }

    // ---- Message Logs -----------------------------------------------

    [Fact]
    public async Task MessageLogs_List_ReturnsDict()
    {
        if (!_fixture.Available) return;
        var logs = NewLogs();
        var body = await logs.Messages.ListAsync();
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/messaging/logs", last.Path);
        Assert.Equal("message.list_message_logs", last.MatchedRoute);
    }

    [Fact]
    public async Task MessageLogs_Get_UsesIdInPath()
    {
        if (!_fixture.Available) return;
        var logs = NewLogs();
        var body = await logs.Messages.GetAsync("ml-42");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/messaging/logs/ml-42", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    // ---- Voice Logs --------------------------------------------------

    [Fact]
    public async Task VoiceLogs_List_ReturnsDict()
    {
        if (!_fixture.Available) return;
        var logs = NewLogs();
        var body = await logs.Voice.ListAsync();
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/voice/logs", last.Path);
        Assert.Equal("voice.list_voice_logs", last.MatchedRoute);
    }

    [Fact]
    public async Task VoiceLogs_Get_UsesIdInPath()
    {
        if (!_fixture.Available) return;
        var logs = NewLogs();
        var body = await logs.Voice.GetAsync("vl-99");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/voice/logs/vl-99", last.Path);
    }

    // ---- Fax Logs ----------------------------------------------------

    [Fact]
    public async Task FaxLogs_List_ReturnsDict()
    {
        if (!_fixture.Available) return;
        var logs = NewLogs();
        var body = await logs.Fax.ListAsync();
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fax/logs", last.Path);
        Assert.Equal("fax.list_fax_logs", last.MatchedRoute);
    }

    [Fact]
    public async Task FaxLogs_Get_UsesIdInPath()
    {
        if (!_fixture.Available) return;
        var logs = NewLogs();
        var body = await logs.Fax.GetAsync("fl-7");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fax/logs/fl-7", last.Path);
    }

    // ---- Conference Logs --------------------------------------------

    [Fact]
    public async Task ConferenceLogs_List_ReturnsDict()
    {
        if (!_fixture.Available) return;
        var logs = NewLogs();
        var body = await logs.Conferences.ListAsync();
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/logs/conferences", last.Path);
        Assert.Equal("logs.list_conferences", last.MatchedRoute);
    }
}
