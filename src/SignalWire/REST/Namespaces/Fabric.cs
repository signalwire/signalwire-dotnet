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

    // Lazily-initialised sub-resources
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
    // Python-parity sub-resources (FabricNamespace.cxml_applications, etc.)
    private CrudResource? _cxmlApplications;
    private CrudResource? _cxmlScripts;
    private CrudResource? _cxmlWebhooks;
    private CrudResource? _freeswitchConnectors;
    private CrudResource? _relayApplications;
    private CrudResource? _resources;
    private CrudResource? _sipGateways;
    private CrudResource? _swmlWebhooks;
    private CrudResource? _tokens;

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

    // ------------------------------------------------------------------
    // Python-parity sub-resources (Python's FabricNamespace exposes
    // these; .NET previously did not). See Python source:
    //   signalwire/rest/namespaces/fabric.py::FabricNamespace.__init__
    // ------------------------------------------------------------------

    public CrudResource CxmlApplications =>
        _cxmlApplications ??= new CrudResource(_client, $"{Base}/cxml_applications");

    public CrudResource CxmlScripts =>
        _cxmlScripts ??= new CrudResource(_client, $"{Base}/cxml_scripts");

    public CrudResource CxmlWebhooks =>
        _cxmlWebhooks ??= new CrudResource(_client, $"{Base}/cxml_webhooks");

    public CrudResource FreeswitchConnectors =>
        _freeswitchConnectors ??= new CrudResource(_client, $"{Base}/freeswitch_connectors");

    public CrudResource RelayApplications =>
        _relayApplications ??= new CrudResource(_client, $"{Base}/relay_applications");

    public CrudResource Resources =>
        _resources ??= new CrudResource(_client, Base);

    public CrudResource SipGateways =>
        _sipGateways ??= new CrudResource(_client, $"{Base}/sip_gateways");

    public CrudResource SwmlWebhooks =>
        _swmlWebhooks ??= new CrudResource(_client, $"{Base}/swml_webhooks");

    /// <summary>
    /// Fabric tokens resource — note this lives at the top-level
    /// ``/api/fabric/tokens`` path, NOT under ``/api/fabric/resources``.
    /// </summary>
    public CrudResource Tokens =>
        _tokens ??= new CrudResource(_client, "/api/fabric/tokens");
}
