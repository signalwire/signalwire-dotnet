namespace SignalWire.REST.Namespaces;

/// <summary>
/// Calling API namespace.
///
/// Provides 37 call-control command methods that each POST to
/// /api/calling/calls with a JSON body containing the command name,
/// an optional call ID, and parameters.
/// </summary>
public class Calling
{
    private readonly HttpClient _client;
    private readonly string _projectId;

    private const string BasePath = "/api/calling/calls";

    public Calling(HttpClient client, string projectId)
    {
        _client = client;
        _projectId = projectId;
    }

    public HttpClient Client => _client;
    public string ProjectId => _projectId;
    public string GetBasePath() => BasePath;

    // ------------------------------------------------------------------
    // Internal execute helper
    // ------------------------------------------------------------------

    private Task<Dictionary<string, object?>> ExecuteAsync(
        string command, string? callId, Dictionary<string, object?>? parms = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["params"] = parms ?? new Dictionary<string, object?>(),
        };

        if (callId is not null)
        {
            body["id"] = callId;
        }

        return _client.PostAsync(BasePath, body);
    }

    // ------------------------------------------------------------------
    // Call lifecycle (5)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> DialAsync(Dictionary<string, object?>? parms = null)
        => ExecuteAsync("dial", null, parms);

    public Task<Dictionary<string, object?>> UpdateCallAsync(Dictionary<string, object?>? parms = null)
        => ExecuteAsync("update", null, parms);

    public Task<Dictionary<string, object?>> EndAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.end", callId, parms);

    public Task<Dictionary<string, object?>> TransferAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.transfer", callId, parms);

    public Task<Dictionary<string, object?>> DisconnectAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.disconnect", callId, parms);

    // ------------------------------------------------------------------
    // Play (5)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> PlayAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.play", callId, parms);

    public Task<Dictionary<string, object?>> PlayPauseAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.play.pause", callId, parms);

    public Task<Dictionary<string, object?>> PlayResumeAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.play.resume", callId, parms);

    public Task<Dictionary<string, object?>> PlayStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.play.stop", callId, parms);

    public Task<Dictionary<string, object?>> PlayVolumeAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.play.volume", callId, parms);

    // ------------------------------------------------------------------
    // Record (4)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> RecordAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.record", callId, parms);

    public Task<Dictionary<string, object?>> RecordPauseAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.record.pause", callId, parms);

    public Task<Dictionary<string, object?>> RecordResumeAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.record.resume", callId, parms);

    public Task<Dictionary<string, object?>> RecordStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.record.stop", callId, parms);

    // ------------------------------------------------------------------
    // Collect (3)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> CollectAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.collect", callId, parms);

    public Task<Dictionary<string, object?>> CollectStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.collect.stop", callId, parms);

    public Task<Dictionary<string, object?>> CollectStartInputTimersAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.collect.start_input_timers", callId, parms);

    // ------------------------------------------------------------------
    // Detect (2)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> DetectAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.detect", callId, parms);

    public Task<Dictionary<string, object?>> DetectStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.detect.stop", callId, parms);

    // ------------------------------------------------------------------
    // Tap (2)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> TapAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.tap", callId, parms);

    public Task<Dictionary<string, object?>> TapStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.tap.stop", callId, parms);

    // ------------------------------------------------------------------
    // Stream (2)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> StreamAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.stream", callId, parms);

    public Task<Dictionary<string, object?>> StreamStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.stream.stop", callId, parms);

    // ------------------------------------------------------------------
    // Denoise (2)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> DenoiseAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.denoise", callId, parms);

    public Task<Dictionary<string, object?>> DenoiseStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.denoise.stop", callId, parms);

    // ------------------------------------------------------------------
    // Transcribe (2)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> TranscribeAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.transcribe", callId, parms);

    public Task<Dictionary<string, object?>> TranscribeStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.transcribe.stop", callId, parms);

    // ------------------------------------------------------------------
    // AI (4)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> AiMessageAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.ai_message", callId, parms);

    public Task<Dictionary<string, object?>> AiHoldAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.ai_hold", callId, parms);

    public Task<Dictionary<string, object?>> AiUnholdAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.ai_unhold", callId, parms);

    public Task<Dictionary<string, object?>> AiStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.ai.stop", callId, parms);

    // ------------------------------------------------------------------
    // Live transcribe / translate (2)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> LiveTranscribeAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.live_transcribe", callId, parms);

    public Task<Dictionary<string, object?>> LiveTranslateAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.live_translate", callId, parms);

    // ------------------------------------------------------------------
    // Fax (2)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> SendFaxStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.send_fax.stop", callId, parms);

    public Task<Dictionary<string, object?>> ReceiveFaxStopAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.receive_fax.stop", callId, parms);

    // ------------------------------------------------------------------
    // SIP (1)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> ReferAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.refer", callId, parms);

    // ------------------------------------------------------------------
    // Custom events (1)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> UserEventAsync(string callId, Dictionary<string, object?>? parms = null)
        => ExecuteAsync("calling.user_event", callId, parms);
}
