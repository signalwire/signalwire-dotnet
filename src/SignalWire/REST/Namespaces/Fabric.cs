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
    private FabricResourcePut? _subscribers;
    private FabricResourcePut? _sipEndpoints;
    private FabricCallFlowsResource? _callFlows;
    private FabricResourcePut? _swmlScripts;
    private FabricConferenceRoomsResource? _conferenceRooms;
    private FabricResourcePatch? _aiAgents;
    // Python-parity sub-resources (FabricNamespace.cxml_applications, etc.)
    private FabricCxmlApplicationsResource? _cxmlApplications;
    private FabricResourcePut? _cxmlScripts;
    private AutoMaterializedWebhookResource? _cxmlWebhooks;
    private FabricResourcePut? _freeswitchConnectors;
    private FabricResourcePut? _relayApplications;
    private FabricResourcePatch? _sipGateways;
    private AutoMaterializedWebhookResource? _swmlWebhooks;
    // Python-parity helpers (assignable from tests; not present in the
    // original .NET Fabric surface).
    private FabricAddresses? _fabricAddresses;
    private FabricResources? _fabricResources;
    private FabricTokens? _fabricTokens;
    private SubscribersHelper? _subscribersHelper;
    private CallFlowsHelper? _callFlowsHelper;
    private ConferenceRoomsHelper? _conferenceRoomsHelper;
    private CxmlApplicationsHelper? _cxmlApplicationsHelper;

    public Fabric(HttpClient client)
    {
        _client = client;
    }

    // Python-parity helper accessors.

    /// <summary>Top-level Fabric addresses (read-only — list/get) that lives
    /// at /api/fabric/addresses (NOT under /api/fabric/resources).</summary>
    public FabricAddresses AddressesTopLevel =>
        _fabricAddresses ??= new FabricAddresses(_client, "/api/fabric/addresses");

    /// <summary>Generic resources operations (list/get/delete/list_addresses/
    /// assign_domain_application) at /api/fabric/resources.</summary>
    public FabricResources ResourcesGeneric =>
        _fabricResources ??= new FabricResources(_client, "/api/fabric/resources");

    /// <summary>Subscriber/guest/invite/embed token endpoints under
    /// /api/fabric. Distinct from the per-account ``Tokens`` accessor which
    /// hits /api/fabric/tokens.</summary>
    public FabricTokens TokensApi =>
        _fabricTokens ??= new FabricTokens(_client);

    /// <summary>Subscriber-scoped SIP-endpoint operations
    /// (get/update/delete) under /api/fabric/resources/subscribers.</summary>
    public SubscribersHelper SubscribersOps =>
        _subscribersHelper ??= new SubscribersHelper(_client, "/api/fabric/resources/subscribers");

    /// <summary>CallFlows singular-path operations (list_addresses,
    /// list_versions, deploy_version).</summary>
    public CallFlowsHelper CallFlowsOps =>
        _callFlowsHelper ??= new CallFlowsHelper(_client, "/api/fabric/resources/call_flows");

    /// <summary>ConferenceRooms singular-path operations (list_addresses).</summary>
    public ConferenceRoomsHelper ConferenceRoomsOps =>
        _conferenceRoomsHelper ??= new ConferenceRoomsHelper(_client, "/api/fabric/resources/conference_rooms");

    /// <summary>cXML applications helper that exposes the deliberate
    /// NotImplementedException on Create (matching Python's
    /// ``CxmlApplicationsResource.create``).</summary>
    public CxmlApplicationsHelper CxmlApplicationsOps =>
        _cxmlApplicationsHelper ??= new CxmlApplicationsHelper();

    public HttpClient Client => _client;

    // ------------------------------------------------------------------
    // Sub-resource accessors (lazy)
    // ------------------------------------------------------------------

    public FabricResourcePut Subscribers =>
        _subscribers ??= new FabricResourcePut(_client, $"{Base}/subscribers");

    public FabricResourcePut SipEndpoints =>
        _sipEndpoints ??= new FabricResourcePut(_client, $"{Base}/sip_endpoints");

    public FabricCallFlowsResource CallFlows =>
        _callFlows ??= new FabricCallFlowsResource(_client, $"{Base}/call_flows");

    public FabricResourcePut SwmlScripts =>
        _swmlScripts ??= new FabricResourcePut(_client, $"{Base}/swml_scripts");

    public FabricConferenceRoomsResource ConferenceRooms =>
        _conferenceRooms ??= new FabricConferenceRoomsResource(_client, $"{Base}/conference_rooms");

    public FabricResourcePatch AiAgents =>
        _aiAgents ??= new FabricResourcePatch(_client, $"{Base}/ai_agents");

    // ------------------------------------------------------------------
    // Python-parity sub-resources (Python's FabricNamespace exposes
    // these; .NET previously did not). See Python source:
    //   signalwire/rest/namespaces/fabric.py::FabricNamespace.__init__
    // ------------------------------------------------------------------

    public FabricCxmlApplicationsResource CxmlApplications =>
        _cxmlApplications ??= new FabricCxmlApplicationsResource(_client, $"{Base}/cxml_applications");

    public FabricResourcePut CxmlScripts =>
        _cxmlScripts ??= new FabricResourcePut(_client, $"{Base}/cxml_scripts");

    public AutoMaterializedWebhookResource CxmlWebhooks =>
        _cxmlWebhooks ??= new AutoMaterializedWebhookResource(_client, $"{Base}/cxml_webhooks");

    public FabricResourcePut FreeswitchConnectors =>
        _freeswitchConnectors ??= new FabricResourcePut(_client, $"{Base}/freeswitch_connectors");

    public FabricResourcePut RelayApplications =>
        _relayApplications ??= new FabricResourcePut(_client, $"{Base}/relay_applications");

    public FabricResourcePatch SipGateways =>
        _sipGateways ??= new FabricResourcePatch(_client, $"{Base}/sip_gateways");

    public AutoMaterializedWebhookResource SwmlWebhooks =>
        _swmlWebhooks ??= new AutoMaterializedWebhookResource(_client, $"{Base}/swml_webhooks");

}
