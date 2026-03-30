namespace SignalWire.REST.Namespaces;

/// <summary>
/// Fabric API namespace.
///
/// Groups all Fabric sub-resources (subscribers, SIP endpoints, call flows,
/// SWML scripts, conference rooms, AI agents, etc.) under a single object.
/// Each sub-resource is lazily initialised as a <see cref="CrudResource"/>
/// pointing at the correct API path under /api/fabric/resources.
/// </summary>
public class Fabric
{
    private readonly HttpClient _client;

    private const string Base = "/api/fabric/resources";

    // 13 lazily-initialised sub-resources
    private CrudResource? _subscribers;
    private CrudResource? _sipEndpoints;
    private CrudResource? _addresses;
    private CrudResource? _callFlows;
    private CrudResource? _swmlScripts;
    private CrudResource? _conversations;
    private CrudResource? _conferenceRooms;
    private CrudResource? _dialPlans;
    private CrudResource? _freeclimbApps;
    private CrudResource? _callQueues;
    private CrudResource? _aiAgents;
    private CrudResource? _sipProfiles;
    private CrudResource? _phoneNumbers;

    public Fabric(HttpClient client)
    {
        _client = client;
    }

    public HttpClient Client => _client;

    // ------------------------------------------------------------------
    // Sub-resource accessors (lazy)
    // ------------------------------------------------------------------

    public CrudResource Subscribers =>
        _subscribers ??= new CrudResource(_client, $"{Base}/subscribers");

    public CrudResource SipEndpoints =>
        _sipEndpoints ??= new CrudResource(_client, $"{Base}/sip_endpoints");

    public CrudResource Addresses =>
        _addresses ??= new CrudResource(_client, $"{Base}/addresses");

    public CrudResource CallFlows =>
        _callFlows ??= new CrudResource(_client, $"{Base}/call_flows");

    public CrudResource SwmlScripts =>
        _swmlScripts ??= new CrudResource(_client, $"{Base}/swml_scripts");

    public CrudResource Conversations =>
        _conversations ??= new CrudResource(_client, $"{Base}/conversations");

    public CrudResource ConferenceRooms =>
        _conferenceRooms ??= new CrudResource(_client, $"{Base}/conference_rooms");

    public CrudResource DialPlans =>
        _dialPlans ??= new CrudResource(_client, $"{Base}/dial_plans");

    public CrudResource FreeclimbApps =>
        _freeclimbApps ??= new CrudResource(_client, $"{Base}/freeclimb_apps");

    public CrudResource CallQueues =>
        _callQueues ??= new CrudResource(_client, $"{Base}/call_queues");

    public CrudResource AiAgents =>
        _aiAgents ??= new CrudResource(_client, $"{Base}/ai_agents");

    public CrudResource SipProfiles =>
        _sipProfiles ??= new CrudResource(_client, $"{Base}/sip_profiles");

    public CrudResource PhoneNumbers =>
        _phoneNumbers ??= new CrudResource(_client, $"{Base}/phone_numbers");
}
