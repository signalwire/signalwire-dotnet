#!/usr/bin/env python3
"""enumerate_surface.py -- emit port_surface.json for the .NET SignalWire SDK.

This walks ``src/SignalWire/**/*.cs``, parses out namespace/class/public-method
structure with regex, and emits JSON matching the shape of
``porting-sdk/python_surface.json``.

Symbol naming contract:

* C# uses PascalCase for methods and properties; Python uses snake_case. The
  diff against ``python_surface.json`` is by Python-canonical symbol name, so
  every method emitted here gets translated PascalCase -> snake_case.
* Constructors are emitted as ``__init__``.
* Async methods named ``FooAsync`` are emitted as ``foo`` (matches Python
  reference, which has no Async suffix).
* C# namespaces map to Python's canonical module path via ``CLASS_MODULE_MAP``.
* ``Service`` (in SignalWire.SWML) renames to ``SWMLService`` (Python convention).
* ``Client`` (in SignalWire.Relay) renames to ``RelayClient``.
* Skills carry the ``Skill`` suffix in C# (e.g. ``WebSearchSkill``); the Python
  reference keeps that suffix, so no rename needed on those.
* ``IDisposable.Dispose``, ``ToString``, ``GetHashCode``, ``Equals``, and other
  .NET object overrides are skipped — they're language-required, not part of
  the SDK contract.

Regex parsing is fine for this SDK's size (~50 .cs files); we don't need
Roslyn.

Usage:
    python3 scripts/enumerate_surface.py            # write port_surface.json
    python3 scripts/enumerate_surface.py --check    # exit 1 on drift
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path


# ---------------------------------------------------------------------------
# C# class/struct/enum -> Python module mapping
# ---------------------------------------------------------------------------
#
# Every class in the .NET SDK has to be reported under a Python-reference
# dotted module name so the diff against ``python_surface.json`` lines up.
# Anything not in this map falls back to the native-namespace translation
# (``SignalWire.Rest.PhoneNumbers`` -> ``signalwire.rest.phone_numbers``).
CLASS_MODULE_MAP: dict[str, str] = {
    # -- agent ------------------------------------------------------------
    "AgentBase": "signalwire.core.agent_base",

    # -- item-I implemented subsystems (H/I turn) -------------------------
    # New hand classes routed to their reference core modules (class name
    # matches the reference leaf verbatim).
    "ConfigLoader": "signalwire.core.config_loader",
    "SecurityConfig": "signalwire.core.security_config",
    "AuthHandler": "signalwire.core.auth_handler",
    "WebService": "signalwire.web.web_service",
    "SwaigFunction": "signalwire.core.swaig_function",
    "BedrockAgent": "signalwire.agents.bedrock",
    "PromptManager": "signalwire.core.agent.prompt.manager",
    "ToolRegistry": "signalwire.core.agent.tools.registry",
    # SWML verb-handler trio (C# namespace SignalWire.SWML) -> the reference
    # signalwire.core.swml_handler module.
    "SWMLVerbHandler": "signalwire.core.swml_handler",
    "AIVerbHandler": "signalwire.core.swml_handler",
    "VerbHandlerRegistry": "signalwire.core.swml_handler",

    # -- contexts ---------------------------------------------------------
    "Context": "signalwire.core.contexts",
    "ContextBuilder": "signalwire.core.contexts",
    "GatherInfo": "signalwire.core.contexts",
    "GatherQuestion": "signalwire.core.contexts",
    "Step": "signalwire.core.contexts",

    # -- datamap ----------------------------------------------------------
    "DataMap": "signalwire.core.data_map",

    # -- swaig ------------------------------------------------------------
    "FunctionResult": "signalwire.core.function_result",

    # -- skills -----------------------------------------------------------
    "SkillBase": "signalwire.core.skill_base",
    "SkillManager": "signalwire.core.skill_manager",
    "SkillRegistry": "signalwire.skills.registry",

    # -- server -----------------------------------------------------------
    "AgentServer": "signalwire.agent_server",

    # -- security ---------------------------------------------------------
    "SessionManager": "signalwire.core.security.session_manager",
    # WebhookValidator is a static helper class in C# whose methods are
    # projected to free functions in
    # ``signalwire.core.security.webhook_validator`` to mirror Python's
    # module-level ``validate_webhook_signature`` / ``validate_request``.
    "WebhookValidator": "signalwire.core.security.webhook_validator",
    # SecurityUtils is a static helper class in C# whose methods are projected
    # to free functions in ``signalwire.core.security.security_utils`` to mirror
    # Python's module-level ``filter_sensitive_headers`` / ``redact_url`` /
    # ``is_valid_hostname``.
    "SecurityUtils": "signalwire.core.security.security_utils",
    # WebhookValidationMiddleware is a port-only adapter (Python ships a
    # FastAPI dependency-factory function instead). Document the addition
    # in PORT_ADDITIONS.md and place the .NET class under the parallel
    # ``signalwire.core.security.webhook_middleware`` path.
    "WebhookValidationMiddleware": "signalwire.core.security.webhook_middleware",

    # -- swml -------------------------------------------------------------
    # ``Service`` in SignalWire.SWML == Python's ``SWMLService``.
    # Renamed via CLASS_RENAME_MAP, mapped here.

    # -- pom --------------------------------------------------------------
    "PromptObjectModel": "signalwire.pom.pom",
    "Section": "signalwire.pom.pom",
    "PomBuilder": "signalwire.core.pom_builder",
    "SWMLBuilder": "signalwire.core.swml_builder",
    "SwmlRenderer": "signalwire.core.swml_renderer",
    "UrlValidator": "signalwire.utils.url_validator",
    "ExecutionMode": "signalwire.utils.execution_mode",
    # REST base layer: Python consolidates every base into signalwire.rest._base;
    # .NET splits each into its own file/namespace. Route them all to _base so
    # the module-consolidation idiom compares equal.
    "CrudWithAddresses": "signalwire.rest._base",
    "CrudResource": "signalwire.rest._base",
    "HttpClient": "signalwire.rest._base",
    "SignalWireRestError": "signalwire.rest._base",
    # plan 1.3b: the transport-failure typed error. Same module as its base
    # (Python consolidates the whole REST base layer into signalwire.rest._base).
    "SignalWireRestTransportError": "signalwire.rest._base",
    # RequestOptions envelope (plan 4.2): the per-request options value type
    # lives in the reference's signalwire.rest._request_options module (.NET's
    # auto-derived signalwire.rest.request_options drops the leading underscore).
    "RequestOptions": "signalwire.rest._request_options",

    # -- relay ------------------------------------------------------------
    "Call": "signalwire.relay.call",
    "Message": "signalwire.relay.message",
    # All Relay Action subclasses live under ``signalwire.relay.call`` in
    # Python (one big module). .NET splits each action into its own
    # source file / namespace.
    "Action": "signalwire.relay.call",
    "AIAction": "signalwire.relay.call",
    "CollectAction": "signalwire.relay.call",
    "ConnectAction": "signalwire.relay.call",
    "DetectAction": "signalwire.relay.call",
    "FaxAction": "signalwire.relay.call",
    "PayAction": "signalwire.relay.call",
    "PlayAction": "signalwire.relay.call",
    "RecordAction": "signalwire.relay.call",
    "ReferAction": "signalwire.relay.call",
    "SendDigitsAction": "signalwire.relay.call",
    "StandaloneCollectAction": "signalwire.relay.call",
    "StreamAction": "signalwire.relay.call",
    "TapAction": "signalwire.relay.call",
    "TranscribeAction": "signalwire.relay.call",
    "DialAction": "signalwire.relay.call",
    "DenoiseAction": "signalwire.relay.call",
    "EchoAction": "signalwire.relay.call",
    "QueueAction": "signalwire.relay.call",
    "PromptAction": "signalwire.relay.call",
    "StandaloneCollectAction": "signalwire.relay.call",
    "Event": "signalwire.relay.event",
    # -- relay events (item H/I) -----------------------------------------
    # The reference signalwire.relay.event module declares one typed event
    # class per RELAY event, each exposing a ``from_payload`` classmethod,
    # plus a module-level ``parse_event`` free function (projected below).
    "RelayEvent": "signalwire.relay.event",
    "CallReceiveEvent": "signalwire.relay.event",
    "CallStateEvent": "signalwire.relay.event",
    "CallingErrorEvent": "signalwire.relay.event",
    "CollectEvent": "signalwire.relay.event",
    "ConferenceEvent": "signalwire.relay.event",
    "ConnectEvent": "signalwire.relay.event",
    "DenoiseEvent": "signalwire.relay.event",
    "DetectEvent": "signalwire.relay.event",
    "DialEvent": "signalwire.relay.event",
    "EchoEvent": "signalwire.relay.event",
    "FaxEvent": "signalwire.relay.event",
    "HoldEvent": "signalwire.relay.event",
    "MessageReceiveEvent": "signalwire.relay.event",
    "MessageStateEvent": "signalwire.relay.event",
    "PayEvent": "signalwire.relay.event",
    "PlayEvent": "signalwire.relay.event",
    "QueueEvent": "signalwire.relay.event",
    "RecordEvent": "signalwire.relay.event",
    "ReferEvent": "signalwire.relay.event",
    "SendDigitsEvent": "signalwire.relay.event",
    "StreamEvent": "signalwire.relay.event",
    "TapEvent": "signalwire.relay.event",
    "TranscribeEvent": "signalwire.relay.event",
    # RelayError lives alongside RelayClient in the reference module.
    "RelayError": "signalwire.relay.client",

    # -- prefabs ----------------------------------------------------------
    "ConciergeAgent": "signalwire.prefabs.concierge",
    "FAQBotAgent": "signalwire.prefabs.faq_bot",
    "InfoGathererAgent": "signalwire.prefabs.info_gatherer",
    "ReceptionistAgent": "signalwire.prefabs.receptionist",
    "SurveyAgent": "signalwire.prefabs.survey",

    # -- skills (one canonical Python module per skill) -------------------
    "ApiNinjasTriviaSkill": "signalwire.skills.api_ninjas_trivia.skill",
    "ClaudeSkillsSkill": "signalwire.skills.claude_skills.skill",
    "CustomSkillsSkill": "signalwire.skills.custom_skills.skill",
    "DatasphereSkill": "signalwire.skills.datasphere.skill",
    "DatasphereServerlessSkill": "signalwire.skills.datasphere_serverless.skill",
    "DatetimeSkill": "signalwire.skills.datetime.skill",
    "GoogleMapsSkill": "signalwire.skills.google_maps.skill",
    "InfoGathererSkill": "signalwire.skills.info_gatherer.skill",
    "JokeSkill": "signalwire.skills.joke.skill",
    "MathSkill": "signalwire.skills.math.skill",
    "NativeVectorSearchSkill": "signalwire.skills.native_vector_search.skill",
    "PlayBackgroundFileSkill": "signalwire.skills.play_background_file.skill",
    "SpiderSkill": "signalwire.skills.spider.skill",
    "SwmlTransferSkill": "signalwire.skills.swml_transfer.skill",
    "WeatherApiSkill": "signalwire.skills.weather_api.skill",
    "WebSearchSkill": "signalwire.skills.web_search.skill",
    "WikipediaSearchSkill": "signalwire.skills.wikipedia_search.skill",
}


# (source_namespace, source_class) -> (target_module, target_class) for
# classes that get a Python-canonical rename.
CLASS_RENAME_MAP: dict[tuple[str, str], tuple[str, str]] = {
    ("SignalWire.SWML", "Service"): (
        "signalwire.core.swml_service", "SWMLService",
    ),
    # SignalWire.Relay's ``Client`` is Python's ``RelayClient``.
    ("SignalWire.Relay", "Client"): (
        "signalwire.relay.client", "RelayClient",
    ),
    # SignalWire.REST's ``RestClient`` is Python's
    # ``signalwire.rest.client.RestClient``. .NET's auto-derived module
    # ``signalwire.rest.rest_client`` doesn't match Python's canonical
    # path ``signalwire.rest.client``.
    ("SignalWire.REST", "RestClient"): (
        "signalwire.rest.client", "RestClient",
    ),
    # EffectiveRequestOptions is the .NET realization of the reference's
    # PRIVATE signalwire.rest._request_options._EffectiveOptions (the resolved
    # options record). Rename so a type-ref to it (resolve's return,
    # status_is_retryable's opts param) compares exact; the class itself is
    # dropped from the emitted surface (the oracle records no public class).
    ("SignalWire.REST", "EffectiveRequestOptions"): (
        "signalwire.rest._request_options", "_EffectiveOptions",
    ),
    # SignalWire.SWML.Schema is the .NET-idiomatic singleton wrapper
    # around the SWML JSON schema; Python keeps an instantiable
    # SchemaUtils helper at signalwire.utils.schema_utils. Rename so
    # the cross-language audit lines up.
    ("SignalWire.SWML", "Schema"): (
        "signalwire.utils.schema_utils", "SchemaUtils",
    ),
    ("SignalWire.SWML", "SchemaValidationError"): (
        "signalwire.utils.schema_utils", "SchemaValidationError",
    ),
    # .NET's REST namespace classes (in namespace ``SignalWire.REST.Namespaces``)
    # are named after the namespace (``Calling``, ``Fabric``); Python
    # places each in its own submodule and suffixes the class with
    # ``Namespace``.
    ("SignalWire.REST.Namespaces", "Calling"): (
        "signalwire.rest.namespaces.calling", "CallingNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Fabric"): (
        "signalwire.rest.namespaces.fabric", "FabricNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Datasphere"): (
        "signalwire.rest.namespaces.datasphere", "DatasphereNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Logs"): (
        "signalwire.rest.namespaces.logs", "LogsNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Project"): (
        "signalwire.rest.namespaces.project", "ProjectNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Registry"): (
        "signalwire.rest.namespaces.registry", "RegistryNamespace",
    ),
    ("SignalWire.REST.Namespaces", "Video"): (
        "signalwire.rest.namespaces.video", "VideoNamespace",
    ),
    # ----- Video sub-resources (Python video.py) -----
    ("SignalWire.REST.Namespaces", "VideoRooms"): (
        "signalwire.rest.namespaces.video", "VideoRooms",
    ),
    ("SignalWire.REST.Namespaces", "VideoRoomTokens"): (
        "signalwire.rest.namespaces.video", "VideoRoomTokens",
    ),
    ("SignalWire.REST.Namespaces", "VideoRoomSessions"): (
        "signalwire.rest.namespaces.video", "VideoRoomSessions",
    ),
    ("SignalWire.REST.Namespaces", "VideoRoomRecordings"): (
        "signalwire.rest.namespaces.video", "VideoRoomRecordings",
    ),
    ("SignalWire.REST.Namespaces", "VideoConferences"): (
        "signalwire.rest.namespaces.video", "VideoConferences",
    ),
    ("SignalWire.REST.Namespaces", "VideoConferenceTokens"): (
        "signalwire.rest.namespaces.video", "VideoConferenceTokens",
    ),
    ("SignalWire.REST.Namespaces", "VideoStreams"): (
        "signalwire.rest.namespaces.video", "VideoStreams",
    ),
    # ----- Logs sub-resources (Python logs.py) -----
    ("SignalWire.REST.Namespaces", "MessageLogs"): (
        "signalwire.rest.namespaces.logs", "MessageLogs",
    ),
    ("SignalWire.REST.Namespaces", "VoiceLogs"): (
        "signalwire.rest.namespaces.logs", "VoiceLogs",
    ),
    ("SignalWire.REST.Namespaces", "FaxLogs"): (
        "signalwire.rest.namespaces.logs", "FaxLogs",
    ),
    ("SignalWire.REST.Namespaces", "ConferenceLogs"): (
        "signalwire.rest.namespaces.logs", "ConferenceLogs",
    ),
    # ----- Registry sub-resources (Python registry.py) -----
    ("SignalWire.REST.Namespaces", "RegistryBrands"): (
        "signalwire.rest.namespaces.registry", "RegistryBrands",
    ),
    ("SignalWire.REST.Namespaces", "RegistryCampaigns"): (
        "signalwire.rest.namespaces.registry", "RegistryCampaigns",
    ),
    ("SignalWire.REST.Namespaces", "RegistryOrders"): (
        "signalwire.rest.namespaces.registry", "RegistryOrders",
    ),
    ("SignalWire.REST.Namespaces", "RegistryNumbers"): (
        "signalwire.rest.namespaces.registry", "RegistryNumbers",
    ),
    # ----- Fabric extras (helpers I added in C# for parity) -----
    # The three CrudWithAddresses subtypes mirror Python's PATCH/PUT/webhook
    # fabric resource base classes (fabric.py: FabricResource [PATCH update],
    # FabricResourcePUT [PUT update], AutoMaterializedWebhook).
    ("SignalWire.REST.Namespaces", "FabricResourcePatch"): (
        "signalwire.rest.namespaces.fabric", "FabricResource",
    ),
    ("SignalWire.REST.Namespaces", "FabricResourcePut"): (
        "signalwire.rest.namespaces.fabric", "FabricResourcePUT",
    ),
    ("SignalWire.REST.Namespaces", "AutoMaterializedWebhookResource"): (
        "signalwire.rest.namespaces.fabric", "AutoMaterializedWebhook",
    ),
    ("SignalWire.REST.Namespaces", "FabricAddresses"): (
        "signalwire.rest.namespaces.fabric", "FabricAddresses",
    ),
    ("SignalWire.REST.Namespaces", "FabricResources"): (
        "signalwire.rest.namespaces.fabric", "GenericResources",
    ),
    ("SignalWire.REST.Namespaces", "FabricTokens"): (
        "signalwire.rest.namespaces.fabric", "FabricTokens",
    ),
    ("SignalWire.REST.Namespaces", "SubscribersHelper"): (
        "signalwire.rest.namespaces.fabric", "SubscribersResource",
    ),
    ("SignalWire.REST.Namespaces", "CallFlowsHelper"): (
        "signalwire.rest.namespaces.fabric", "CallFlowsResource",
    ),
    ("SignalWire.REST.Namespaces", "ConferenceRoomsHelper"): (
        "signalwire.rest.namespaces.fabric", "ConferenceRoomsResource",
    ),
    ("SignalWire.REST.Namespaces", "CxmlApplicationsHelper"): (
        "signalwire.rest.namespaces.fabric", "CxmlApplicationsResource",
    ),
    ("SignalWire.REST.Namespaces", "FabricCallFlowsResource"): (
        "signalwire.rest.namespaces.fabric", "CallFlowsResource",
    ),
    ("SignalWire.REST.Namespaces", "FabricConferenceRoomsResource"): (
        "signalwire.rest.namespaces.fabric", "ConferenceRoomsResource",
    ),
    ("SignalWire.REST.Namespaces", "FabricCxmlApplicationsResource"): (
        "signalwire.rest.namespaces.fabric", "CxmlApplicationsResource",
    ),
    # ----- Small namespaces (Python files name them with "Resource" suffix) -----
    ("SignalWire.REST.Namespaces", "Mfa"): (
        "signalwire.rest.namespaces.mfa", "MfaResource",
    ),
    ("SignalWire.REST.Namespaces", "LookupResource"): (
        "signalwire.rest.namespaces.lookup", "LookupResource",
    ),
    ("SignalWire.REST.Namespaces", "PhoneNumbers"): (
        "signalwire.rest.namespaces.phone_numbers", "PhoneNumbersResource",
    ),
    ("SignalWire.REST.Namespaces", "SipProfile"): (
        "signalwire.rest.namespaces.sip_profile", "SipProfileResource",
    ),
    ("SignalWire.REST.Namespaces", "ShortCodes"): (
        "signalwire.rest.namespaces.short_codes", "ShortCodesResource",
    ),
    ("SignalWire.REST.Namespaces", "NumberGroups"): (
        "signalwire.rest.namespaces.number_groups", "NumberGroupsResource",
    ),
    ("SignalWire.REST.Namespaces", "ImportedNumbers"): (
        "signalwire.rest.namespaces.imported_numbers", "ImportedNumbersResource",
    ),
    ("SignalWire.REST.Namespaces", "ProjectTokens"): (
        "signalwire.rest.namespaces.project", "ProjectTokens",
    ),
    ("SignalWire.REST.Namespaces", "DatasphereNs"): (
        "signalwire.rest.namespaces.datasphere", "DatasphereNamespace",
    ),
    ("SignalWire.REST.Namespaces", "DatasphereDocuments"): (
        "signalwire.rest.namespaces.datasphere", "DatasphereDocuments",
    ),
    ("SignalWire.REST.Namespaces", "Addresses"): (
        "signalwire.rest.namespaces.addresses", "AddressesResource",
    ),
    ("SignalWire.REST.Namespaces", "Recordings"): (
        "signalwire.rest.namespaces.recordings", "RecordingsResource",
    ),
    ("SignalWire.REST.Namespaces", "Queues"): (
        "signalwire.rest.namespaces.queues", "QueuesResource",
    ),
    ("SignalWire.REST.Namespaces", "VerifiedCallers"): (
        "signalwire.rest.namespaces.verified_callers", "VerifiedCallersResource",
    ),
    ("SignalWire.REST.Namespaces", "ChatResource"): (
        "signalwire.rest.namespaces.chat", "ChatResource",
    ),
    ("SignalWire.REST.Namespaces", "PubSubResource"): (
        "signalwire.rest.namespaces.pubsub", "PubSubResource",
    ),
    # ----- PaginatedIterator (Python lives at _pagination, not paginated_iterator) -----
    ("SignalWire.REST", "PaginatedIterator"): (
        "signalwire.rest._pagination", "PaginatedIterator",
    ),
}


# Skill class renames -- our .NET names already carry the ``Skill`` suffix
# (e.g. ``WebSearchSkill``); the Python reference uses the same convention
# but the canonical class name itself sometimes differs (e.g. ``DataSphereSkill``
# in Python vs ``DatasphereSkill`` in .NET). Apply rename so the diff lines up.
SKILL_RENAMES: dict[str, str] = {
    "DatasphereSkill": "DataSphereSkill",
    "DatasphereServerlessSkill": "DataSphereServerlessSkill",
    "SwmlTransferSkill": "SWMLTransferSkill",
    # SWAIG acronym: the reference class is ``SWAIGFunction`` (the C# idiom
    # PascalCases the acronym to ``SwaigFunction``).
    "SwaigFunction": "SWAIGFunction",
    # DateTime: the reference class is ``DateTimeSkill`` (C# PascalCases to
    # ``DatetimeSkill``).
    "DatetimeSkill": "DateTimeSkill",
}


# Skill data-carrying PUBLIC PROPERTIES that mirror Python instance attributes
# (name / description / supports_multiple_instances / version) — set in
# ``__init__``, NOT recorded on the class surface. Dropped from every skill
# subclass's surface (they read as additions otherwise; identical idiom to the
# WebService/event data-property drop).
_SKILL_PROPERTY_EXTRAS = {
    "name", "description", "supports_multiple_instances", "version",
}

# SkillBase-provided methods every skill genuinely INHERITS (real, callable
# capability): the C# base declares them; the regex enumerator only sees a
# method where it is re-declared, so a subclass that inherits without overriding
# shows nothing. Project the reference-recorded subset per skill — restricted to
# methods that ACTUALLY exist on SkillBase (never invent surface). Keyed by the
# reference (post-rename) class name.
_SKILLBASE_INHERITABLE = {
    "cleanup", "get_global_data", "get_hints", "get_instance_key",
    "get_parameter_schema", "get_prompt_sections", "register_tools", "setup",
}
SKILL_INHERITED_PROJECTIONS: dict[str, list[str]] = {
    "ApiNinjasTriviaSkill": ["get_instance_key", "get_parameter_schema"],
    "ClaudeSkillsSkill": ["get_parameter_schema"],
    "DataSphereSkill": ["cleanup", "get_hints", "get_instance_key", "get_parameter_schema"],
    "DataSphereServerlessSkill": ["get_hints", "get_instance_key", "get_parameter_schema"],
    "DateTimeSkill": ["get_hints", "get_parameter_schema"],
    "GoogleMapsSkill": ["get_parameter_schema"],
    "InfoGathererSkill": ["get_instance_key", "get_parameter_schema"],
    "JokeSkill": ["get_hints", "get_parameter_schema"],
    "MathSkill": ["get_hints", "get_parameter_schema"],
    "NativeVectorSearchSkill": ["cleanup", "get_global_data", "get_instance_key",
                                 "get_parameter_schema", "get_prompt_sections"],
    "PlayBackgroundFileSkill": ["get_instance_key", "get_parameter_schema"],
    "SpiderSkill": ["cleanup", "get_instance_key", "get_parameter_schema"],
    "SWMLTransferSkill": ["get_instance_key", "get_parameter_schema"],
    "WeatherApiSkill": ["get_parameter_schema"],
    "WebSearchSkill": ["get_hints", "get_instance_key"],
    "WikipediaSearchSkill": ["get_hints", "get_parameter_schema"],
}


# Method-name renames applied AFTER pascal_to_snake. When .NET's PascalCase
# CamelCases something Python keeps as a single word (e.g. ``Foreach`` =>
# ``foreach``), the casing rule produces an extra underscore. The map below
# normalises those mismatches.
METHOD_RENAMES: dict[str, str] = {
    "for_each": "foreach",
    # .NET can't shadow the private LoadSchema() that runs in the
    # constructor. The public dict-returning equivalent is named
    # LoadSchemaPublic in C# but maps to Python's ``load_schema``.
    "load_schema_public": "load_schema",
}

# Methods we never emit. .NET's IDisposable/object overrides aren't part of
# the SDK contract.
SKIP_METHOD_NAMES: set[str] = {
    "Dispose", "DisposeAsync", "ToString", "GetHashCode", "Equals", "Finalize",
    "MemberwiseClone",
    # C# constructs that can superficially look like methods
    "operator", "using", "typedef", "friend", "template", "return",
    "if", "else", "for", "while", "do", "switch", "case", "lock",
    "try", "catch", "finally", "throw",
}


# Methods to project onto the AgentBase mixin classes Python uses but C# has
# flattened onto AgentBase. Mirrors enumerate_surface.py from the C++ port.
MIXIN_PROJECTIONS: dict[tuple[str, str], list[str]] = {
    ("signalwire.core.mixins.ai_config_mixin", "AIConfigMixin"): [
        "add_function_include", "add_hint", "add_hints", "add_internal_filler",
        "add_language", "add_mcp_server", "add_pattern_hint", "add_pronunciation",
        "enable_debug_events", "enable_mcp_server",
        "get_language_params",
        "set_function_includes", "set_global_data", "set_internal_fillers",
        "set_language_params", "set_languages", "set_multilingual",
        "set_native_functions",
        "set_param", "set_params",
        "set_post_prompt_llm_params", "set_prompt_llm_params",
        "set_pronunciations", "update_global_data",
    ],
    ("signalwire.core.mixins.auth_mixin", "AuthMixin"): [
        "get_basic_auth_credentials", "validate_basic_auth",
    ],
    ("signalwire.core.mixins.mcp_server_mixin", "MCPServerMixin"): [],
    ("signalwire.core.mixins.prompt_mixin", "PromptMixin"): [
        "contexts", "define_contexts", "get_contexts", "get_post_prompt",
        "get_prompt", "get_raw_prompt",
        "prompt_add_section", "prompt_add_subsection", "prompt_add_to_section",
        "prompt_has_section", "reset_contexts", "set_post_prompt",
        "set_prompt_pom", "set_prompt_text",
    ],
    # Python additionally extracted a ``PromptManager`` class that
    # PromptMixin delegates to. Most of the same methods exist there too
    # (the user-facing surface is identical — `agent.prompt_manager.X`
    # ≡ `agent.X`). Project the same set so the cross-language audit
    # treats both paths as covered.
    # PromptManager is a REAL C# class (its own ctor -> __init__ enumerates
    # directly, merged via the UNION projection below); do NOT list __init__
    # here or it would be pulled from AgentBase's ctor and stripped off
    # AgentBase.
    ("signalwire.core.agent.prompt.manager", "PromptManager"): [
        "define_contexts", "get_contexts", "get_post_prompt",
        "get_prompt", "get_raw_prompt",
        "prompt_add_section", "prompt_add_subsection", "prompt_add_to_section",
        "prompt_has_section", "set_post_prompt", "set_prompt_pom",
        "set_prompt_text",
    ],
    # Also project to PromptMixin since PromptManager-equivalent
    # methods live there in older Python (PromptMixin delegates to
    # PromptManager). When .NET's AgentBase exposes GetRawPrompt /
    # GetPostPrompt / GetContexts / SetPromptPom, those should also
    # satisfy PromptMixin parity checks.
    ("signalwire.core.mixins.serverless_mixin", "ServerlessMixin"): [
        "handle_serverless_request",
    ],
    ("signalwire.core.mixins.skill_mixin", "SkillMixin"): [
        "add_skill", "has_skill", "list_skills", "remove_skill",
    ],
    ("signalwire.core.mixins.state_mixin", "StateMixin"): [
        "validate_tool_token",
    ],
    ("signalwire.core.mixins.tool_mixin", "ToolMixin"): [
        "define_tool", "define_tools", "on_function_call",
        "register_swaig_function",
    ],
    # Python additionally extracted a ``ToolRegistry`` class that the
    # ToolMixin delegates to. Project the same set of query methods to
    # the ToolRegistry path so the cross-language audit treats methods
    # on .NET's Service (which inherits to AgentBase) as covering both.
    ("signalwire.core.agent.tools.registry", "ToolRegistry"): [
        "define_tool", "register_swaig_function",
        "has_function", "get_function", "get_all_functions",
        "remove_function",
    ],
    ("signalwire.core.mixins.web_mixin", "WebMixin"): [
        "as_router", "enable_debug_routes", "manual_set_proxy_url", "on_request",
        "on_swml_request", "register_routing_callback", "run", "serve",
        "set_dynamic_config_callback", "setup_graceful_shutdown",
    ],
}

# Method-NAME aliases per [module, class]: a C#-idiom method whose snake_case
# translation differs from the reference (singular/plural, an abbreviation, or
# a genuinely different idiom name) is renamed to the reference name here so the
# two compare EQUAL (Rule 2 — reconcile idiom in the enumerator, not omissions).
# Applied AFTER pascal_to_snake, per class.
SURFACE_METHOD_ALIASES: dict[tuple[str, str], dict[str, str]] = {
    # SkillBase: C# singular property/method names -> reference plural.
    ("signalwire.core.skill_base", "SkillBase"): {
        "get_hint": "get_hints",
        "get_prompt_section": "get_prompt_sections",
        "register_tool": "register_tools",
        "validate_env_var": "validate_env_vars",
    },
    # SkillRegistry: C# factory-name idiom -> reference class-name idiom.
    ("signalwire.skills.registry", "SkillRegistry"): {
        "get_factory": "get_skill_class",
    },
    ("signalwire.core.security.session_manager", "SessionManager"): {
        "create_token": "generate_token",
    },
    # SWAIGFunction: C# ``Call``/``Invoke`` -> the reference dunder ``__call__``.
    # (Keyed by the post-rename class name — emit_class_name maps
    # SwaigFunction -> SWAIGFunction before the alias lookup.)
    ("signalwire.core.swaig_function", "SWAIGFunction"): {
        "call": "__call__",
    },
    # BedrockAgent: C# ``Repr()`` -> the reference dunder ``__repr__``.
    ("signalwire.agents.bedrock", "BedrockAgent"): {
        "repr": "__repr__",
    },
    # Call: Python renames the reserved word ``pass`` -> ``pass_``; the C#
    # ``PassAsync`` translates to ``pass`` -> rename to the reference ``pass_``.
    ("signalwire.relay.call", "Call"): {
        "pass": "pass_",
    },
    # SkillManager: C# ``ListSkills`` -> the reference ``list_loaded_skills``
    # (the manager lists the LOADED skills, distinct from the registry's
    # list_skills of all available skills).
    ("signalwire.core.skill_manager", "SkillManager"): {
        "list_skills": "list_loaded_skills",
    },
    # RelayClient: the C# ``Protocol`` property -> the reference
    # ``relay_protocol`` (the negotiated RELAY sub-protocol identity).
    ("signalwire.relay.client", "RelayClient"): {
        "protocol": "relay_protocol",
    },
}

# Per-[module, class] method names to ADD to the class's surface even though no
# C# method produces them directly (a reference dunder the class semantically
# has — e.g. an iterable's ``__iter__``, a constructed object's ``__init__``
# whose C# ctor is non-public). ONLY for reference-present dunders that the C#
# idiom expresses without a matching public method-name. NOT a backdoor for
# undone work — each entry names a real capability the class already has.
SURFACE_METHOD_INJECTIONS: dict[tuple[str, str], list[str]] = {
    # SkillRegistry is a singleton (private ctor) but the reference records
    # ``__init__``; the object is constructed (via Instance) so the capability
    # exists — the C# ctor is simply private.
    ("signalwire.skills.registry", "SkillRegistry"): ["__init__"],
    # C# Schema is a singleton (private ctor) surfaced as SchemaUtils; the
    # object is constructed via Instance so ``__init__`` capability is real.
    ("signalwire.utils.schema_utils", "SchemaUtils"): ["__init__"],
    # Call / Message override C# ToString() (skipped by the enumerator) — the
    # reference records the ``__repr__`` dunder for both; the capability is real.
    ("signalwire.relay.call", "Call"): ["__repr__"],
    ("signalwire.relay.message", "Message"): ["__repr__"],
    # ContextBuilder / PromptManager / ToolRegistry / SkillBase are constructed
    # via their (implicit or base) C# ctors — ``__init__`` capability is real,
    # the enumerator just can't see an implicit ctor.
    ("signalwire.core.contexts", "ContextBuilder"): ["__init__"],
    ("signalwire.core.agent.prompt.manager", "PromptManager"): ["__init__"],
    ("signalwire.core.agent.tools.registry", "ToolRegistry"): ["__init__"],
    ("signalwire.core.skill_base", "SkillBase"): ["__init__"],
    # AgentBase inherits Service.GetFullUrl — the reference records get_full_url
    # on AgentBase itself; the capability is real via inheritance.
    ("signalwire.core.agent_base", "AgentBase"): ["get_full_url"],
    # Prefab agents inherit AgentBase.OnSummary / Service.OnSwmlRequest — the
    # reference records these per-prefab (Python overrides them); the C#
    # prefabs inherit the same callable capability.
    ("signalwire.prefabs.concierge", "ConciergeAgent"): ["on_summary"],
    ("signalwire.prefabs.faq_bot", "FAQBotAgent"): ["on_summary"],
    ("signalwire.prefabs.receptionist", "ReceptionistAgent"): ["on_summary"],
    ("signalwire.prefabs.survey", "SurveyAgent"): ["on_summary"],
    ("signalwire.prefabs.info_gatherer", "InfoGathererAgent"): ["on_swml_request"],
    # These skills define a Python ``__init__``; the C# skills are constructed
    # via ``new X()`` in the SkillRegistry factories, so the ctor capability is
    # real (the implicit C# ctor just isn't visible to the regex enumerator).
    ("signalwire.skills.api_ninjas_trivia.skill", "ApiNinjasTriviaSkill"): ["__init__"],
    ("signalwire.skills.play_background_file.skill", "PlayBackgroundFileSkill"): ["__init__"],
    ("signalwire.skills.spider.skill", "SpiderSkill"): ["__init__"],
    ("signalwire.skills.weather_api.skill", "WeatherApiSkill"): ["__init__"],
    # RequestOptions (plan 4.2): the reference's SURFACE lists ONLY merge() — the
    # dataclass fields (timeout/retries/retry_on_status/retry_backoff/abort_signal)
    # and __init__ are NOT surface symbols in Python (nor in go/ts/ruby/java). Do
    # NOT inject __init__/abort_signal; .NET's record still HAS them, but the
    # SURFACE projection matches the reference (fields reconcile in signatures, not
    # surface). This keeps the fleet uniform.
}

# Static "helper" C# classes whose METHODS are the reference's module-level
# FREE FUNCTIONS (Python declares them at module scope, not on a class). Map the
# C# class -> the reference module; its methods move to that module's
# ``functions[]`` and the class itself is NOT emitted. Method names are
# translated + alias-mapped first. ``method_aliases`` optionally renames a
# C#-idiom method to the reference free-function name.
FREE_FUNCTION_CLASSES: dict[str, dict] = {
    "SecurityUtils": {
        "module": "signalwire.core.security.security_utils",
        "aliases": {"filter_sensitive_header": "filter_sensitive_headers"},
    },
    "WebhookValidator": {
        "module": "signalwire.core.security.webhook_validator",
        "aliases": {},
    },
    "UrlValidator": {
        "module": "signalwire.utils.url_validator",
        # only ``validate_url`` is a reference free function; the
        # resolved-addresses helper is a port-only addition (recorded in
        # PORT_ADDITIONS.md) — keep just the canonical one.
        "aliases": {},
        "keep": {"validate_url"},
    },
    "ExecutionMode": {
        "module": "signalwire.utils",
        # get_execution_mode lives in logging_config in the reference; only
        # is_serverless_mode is a signalwire.utils free function.
        "aliases": {},
        "keep": {"is_serverless_mode"},
    },
    "LoggingConfig": {
        "module": "signalwire.core.logging_config",
        "aliases": {},
    },
    "TypeInference": {
        "module": "signalwire.core.agent.tools.type_inference",
        "aliases": {},
    },
    # RequestOptionsSupport.Resolve / StatusIsRetryable -> the module-level
    # signalwire.rest._request_options.resolve / status_is_retryable free
    # functions (plan 4.2). .NET has no module-level free functions, so the
    # static facade hosts them; the class itself is not emitted.
    "RequestOptionsSupport": {
        "module": "signalwire.rest._request_options",
        "aliases": {},
        "keep": {"resolve", "status_is_retryable"},
    },
    # RelayEvents.parse_event -> the module-level free function.
    "RelayEvents": {
        "module": "signalwire.relay.event",
        "aliases": {},
        "keep": {"parse_event"},
    },
}

# Class-method free-function projections: a C# ``public static`` method on a
# hand class that the reference exposes as a MODULE-level free function (the
# class stays, only these named methods move to the module's functions[]).
# [C#ClassName] -> (reference_module, [method reference-names]).
FREE_FUNCTION_PROJECTIONS: dict[str, tuple[str, list[str]]] = {
    # DataMap.create_expression_tool / create_simple_api_tool are module-level
    # in the reference (signalwire.core.data_map free functions).
    "DataMap": ("signalwire.core.data_map",
                ["create_expression_tool", "create_simple_api_tool"]),
    # ContextBuilder.CreateSimpleContext -> the module-level
    # signalwire.core.contexts.create_simple_context free function.
    "ContextBuilder": ("signalwire.core.contexts", ["create_simple_context"]),
}

# Top-level ``signalwire`` module free functions (Python __init__ re-exports).
# [C#ClassName] -> [(c#_method_snake, reference_function_name)]; the method is
# projected onto the ``signalwire`` module's functions[]. ``RestClient`` is a
# top-level class re-export the reference records as a function name.
TOPLEVEL_FUNCTION_PROJECTIONS: dict[str, list[tuple[str, str]]] = {
    "SkillRegistry": [
        ("register_skill", "register_skill"),
        ("add_skill_directory", "add_skill_directory"),
        ("list_skills", "list_skills"),
        ("get_all_skills_schema", "list_skills_with_params"),
    ],
}
# Additional bare top-level function names the reference records that map to a
# top-level class re-export (not a projected method).
TOPLEVEL_FUNCTION_NAMES: list[str] = ["RestClient"]

# Per-[module, class] method ALLOWLIST: for classes whose reference contract is
# a fixed, known method set, the C# idiom carries extra PUBLIC PROPERTIES that
# are data attributes (Python sets these as ``self.x = ...`` in ``__init__`` —
# NOT recorded on the class surface). Intersect the enumerated members with the
# reference set so those idiomatic data-properties don't read as port additions.
# A genuinely-missing reference method still surfaces as MISSING (checked
# separately), so this cannot mask undone work — it only drops idiom noise.
SURFACE_METHOD_ALLOWLIST: dict[tuple[str, str], set[str]] = {
    # RequestOptions (plan 4.2): the reference records exactly __init__ +
    # abort_signal + merge; the .NET record's per-field property accessors
    # (timeout/retries/...) are the dataclass fields Python sets in __init__,
    # not separate surface — drop them so the surface set matches the oracle.
    ("signalwire.rest._request_options", "RequestOptions"):
        {"__init__", "abort_signal", "merge"},
    ("signalwire.core.swaig_function", "SWAIGFunction"): {
        "__call__", "__init__", "execute", "to_swaig", "validate_args",
    },
    ("signalwire.web.web_service", "WebService"): {
        "__init__", "add_directory", "remove_directory", "start", "stop",
    },
    ("signalwire.relay.call", "StandaloneCollectAction"): {
        "__init__", "start_input_timers",
    },
    # REST base layer — restrict each consolidated base to its reference own
    # surface (the C# unified CrudResource carries read+write+config helpers that
    # the reference splits across BaseResource/ReadResource/CrudResource; the
    # read/base methods are recorded on those reference bases, projected below).
    ("signalwire.rest._base", "CrudResource"): {
        "create", "delete", "update",
    },
    ("signalwire.rest._base", "HttpClient"): {
        "__init__", "delete", "get", "patch", "post", "put",
    },
    ("signalwire.rest._base", "SignalWireRestError"): {
        "__init__",
    },
    # C# Schema -> reference SchemaUtils: keep exactly the reference own-surface
    # (the C# helper carries extra convenience accessors recorded as additions).
    ("signalwire.utils.schema_utils", "SchemaUtils"): {
        "__init__", "full_validation_available", "generate_method_body",
        "generate_method_signature", "get_all_verb_names", "get_verb_parameters",
        "get_verb_properties", "get_verb_required_properties", "load_schema",
        "validate_document", "validate_verb",
    },
}
# SWMLService is allowlisted SEPARATELY, in post-processing AFTER the mixin
# projection pools its (unrestricted) methods — many Service methods legitimately
# satisfy a mixin (define_tool/run/validate_basic_auth/…) while NOT being part of
# the reference SWMLService's own surface. Restricting inline would starve the
# mixin pool. The reference SWMLService's exact own-surface set:
_SWML_SERVICE_ALLOW = {
    "__getattr__", "__init__", "add_section", "add_verb", "add_verb_to_section",
    "as_router", "extract_sip_username", "full_validation_enabled",
    "get_basic_auth_credentials", "get_document", "handle_request",
    "manual_set_proxy_url", "on_request", "register_routing_callback",
    "register_verb_handler", "render_document", "reset_document", "serve", "stop",
}
# Every reference class in signalwire.relay.event exposes exactly ``from_payload``
# (the typed data fields are instance attributes, not surface). Restrict all
# event classes to that single method.
_RELAY_EVENT_ONLY = {"from_payload"}

# Relay Action control surface: the reference projects the control methods
# (stop/pause/resume/volume) directly onto each CONCRETE action (the internal
# Stoppable/Pausable/Volume bases are NOT cross-port symbols). .NET declares
# pause/resume/volume directly on the concrete action classes, and `stop` on the
# shared Action base (invoked via GetStopMethod). Project the canonical control
# method NAMES onto each concrete action per this map so the surface matches the
# oracle. reference_action -> [control methods it exposes].
RELAY_ACTION_CONTROL_METHODS: dict[str, list[str]] = {
    "PlayAction": ["stop", "pause", "resume", "volume"],
    "RecordAction": ["stop", "pause", "resume"],
    "CollectAction": ["stop", "pause", "resume", "volume"],
    "StandaloneCollectAction": ["stop"],
    "AIAction": ["stop"],
    "DetectAction": ["stop"],
    "FaxAction": ["stop"],
    "PayAction": ["stop"],
    "StreamAction": ["stop"],
    "TapAction": ["stop"],
    "TranscribeAction": ["stop"],
}


# ---------------------------------------------------------------------------
# Parsing
# ---------------------------------------------------------------------------

# C# file-scoped namespace: ``namespace SignalWire.Skills;``
FILE_NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][\w.]*)\s*;")
# C# block namespace: ``namespace SignalWire.Skills {``
BLOCK_NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][\w.]*)\s*\{")
# Class declaration:
#   public class Foo : Bar { ...
#   public sealed class Foo {
#   public abstract class Foo<T> {
#   public static class ReservedToolNames
CLASS_RE = re.compile(
    r"^\s*public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
    r"(?:class|struct|interface|record)\s+([A-Z][A-Za-z0-9_]*)"
)

# Public method or property declaration. We DON'T require the closing `)`
# to be on the same line as the header — many .NET methods wrap arguments
# across multiple lines. We only look for the opening `(`, optionally
# preceded by a generic parameter list `<...>`.
#
#   public void Foo(...)
#   public int Bar { get; }
#   public async Task<Foo> BazAsync(...)
#   public override SomeType Quux(...)
#   public virtual T1 Baz(...)
#   public static Foo Bar(...)
#   public DataMap Parameter(   <-- args start, continue on next line
METHOD_RE = re.compile(
    r"^\s*public\s+"
    # Optional modifiers between `public` and the return type
    r"(?:(?:override|virtual|static|async|sealed|new|extern|unsafe|readonly|partial)\s+)*"
    # Return type. Two shapes:
    #   plain identifier with generics/arrays/nullable, OR
    #   parenthesised tuple type like `(string User, string Password)`,
    #   optionally nullable: `(int Status, ...)? Validate(`.
    r"(?:[A-Za-z_][\w<>?,.\[\] *&]*\s+|\([^)]+\)\??\s+)?"
    r"(?P<name>[A-Z][A-Za-z0-9_]*)"
    # Optional generic parameter list, then mandatory opening paren.
    r"(?:\s*<[^>]*>)?\s*\("
)

# Property declaration. Two shapes:
#   1. Block-bodied:        public string Foo { get; set; } = "x";
#   2. Expression-bodied:   public Fabric Fabric => _fabric ??= new Fabric(_http);
# Both terminate the property header before any `(`. We match the property
# name by requiring the line is NOT a method (no `(` before the property
# accessor).
PROPERTY_RE = re.compile(
    r"^\s*public\s+"
    r"(?:(?:override|virtual|static|new|sealed|readonly|required)\s+)*"
    r"[A-Za-z_][\w<>?,.\[\] *&]*\s+"
    r"(?P<name>[A-Z][A-Za-z0-9_]*)"
    # Three accepted shapes:
    #   { get; ... }                -- block-bodied (single line)
    #   => <expression>;            -- expression-bodied (single line)
    #   =>                           -- expression-bodied with body on next line
    #   { ... at EOL                -- block-bodied with body across lines
    r"\s*(?:\{[^}]*\}|=>\s*[^;]*;|=>\s*$|\{\s*$)"
)


def strip_block_comments(text: str) -> str:
    """Remove /* ... */ comments (possibly multi-line)."""
    out = []
    i = 0
    n = len(text)
    while i < n:
        if text[i:i + 2] == "/*":
            end = text.find("*/", i + 2)
            if end == -1:
                break
            block = text[i:end + 2]
            out.append("\n" * block.count("\n"))
            i = end + 2
        else:
            out.append(text[i])
            i += 1
    return "".join(out)


def strip_line_comments(line: str) -> str:
    """Remove // and /// comments outside string literals."""
    # Quick check first: lines starting with `///` are XML doc comments only.
    stripped = line.lstrip()
    if stripped.startswith("//"):
        return ""
    # Inline `//` outside strings.
    in_str = False
    in_char = False
    escape = False
    for i, c in enumerate(line):
        if escape:
            escape = False
            continue
        if c == '\\':
            escape = True
            continue
        if not in_char and c == '"':
            in_str = not in_str
        elif not in_str and c == "'":
            in_char = not in_char
        elif not in_str and not in_char and line[i:i + 2] == "//":
            return line[:i]
    return line


# ---------------------------------------------------------------------------
# Per-file parser
# ---------------------------------------------------------------------------

def parse_cs_file(path: Path) -> list[tuple[str, str, list[str]]]:
    """Return list of (namespace, class_name, public_member_names).

    Methods + properties are returned untranslated (PascalCase).
    """
    raw = path.read_text(encoding="utf-8", errors="replace")
    text = strip_block_comments(raw)

    namespace = ""
    # Stack of (kind, name, brace_depth_at_entry, visibility) — we track
    # current class for method assignment.
    scope_stack: list[tuple[str, str, int]] = []
    brace_depth = 0
    file_namespace_seen = False

    # class -> ordered list of member names
    members: dict[str, list[str]] = {}
    # Every class opened in the file (name preserved) — used to surface a
    # genuinely METHOD-LESS generated-type class, which would otherwise be
    # dropped (it contributes no member to ``members``). Only emitted for the
    # generated-type namespaces (see below), so a hand class that legitimately
    # has no public surface is NOT spuriously added.
    opened_classes: list[str] = []

    for raw_line in text.splitlines():
        line = strip_line_comments(raw_line)
        if not line.strip():
            continue

        # File-scoped namespace
        if not file_namespace_seen:
            m = FILE_NAMESPACE_RE.match(line)
            if m:
                namespace = m.group(1)
                file_namespace_seen = True
                continue

        # Block-scoped namespace
        m = BLOCK_NAMESPACE_RE.match(line)
        if m:
            namespace = m.group(1)
            scope_stack.append(("namespace", namespace, brace_depth))
            brace_depth += line.count("{") - line.count("}")
            continue

        # Class / struct / interface / record opener
        cls_m = CLASS_RE.match(line)
        if cls_m and "{" in line:
            class_name = cls_m.group(1)
            opened_classes.append(class_name)
            scope_stack.append(("class", class_name, brace_depth))
            brace_depth += line.count("{") - line.count("}")
            continue
        if cls_m and "{" not in line:
            # Class header on a line without `{` — happens with constraints
            # like `public class Foo<T> where T : new()`. Look ahead to next `{`.
            class_name = cls_m.group(1)
            opened_classes.append(class_name)
            scope_stack.append(("class", class_name, brace_depth))
            # Don't change brace_depth yet; the next `{` line will handle it.
            continue

        # Inside a class scope?
        current_class = None
        for kind, name, _depth in reversed(scope_stack):
            if kind == "class":
                current_class = name
                break

        if current_class is not None and brace_depth == _class_body_depth(scope_stack):
            # Try property first (single line with `{`)
            m = PROPERTY_RE.match(line)
            if m:
                name = m.group("name")
                if name not in SKIP_METHOD_NAMES and not name.startswith("_"):
                    members.setdefault(current_class, []).append(name)
                # Properties may close on the same line; update braces.
                brace_depth += line.count("{") - line.count("}")
                continue

            # Method declaration. Only count if the line contains a paren.
            if "(" in line:
                m = METHOD_RE.match(line)
                if m:
                    name = m.group("name")
                    if name not in SKIP_METHOD_NAMES and not name.startswith("_"):
                        # Constructor: name == class name
                        if name == current_class:
                            members.setdefault(current_class, []).append("__init__")
                        else:
                            members.setdefault(current_class, []).append(name)

        # Update brace tracking
        opens = line.count("{")
        closes = line.count("}")
        brace_depth += opens - closes

        # Pop scopes whose brace_depth has been exited
        while scope_stack and brace_depth <= scope_stack[-1][2]:
            scope_stack.pop()

    findings: list[tuple[str, str, list[str]]] = []
    for cls, names in members.items():
        # Dedup preserving order, then sort
        seen: list[str] = []
        seen_set: set[str] = set()
        for n in names:
            if n not in seen_set:
                seen.append(n)
                seen_set.add(n)
        findings.append((namespace, cls, sorted(seen)))
    # Surface method-less generated-type classes the member scan produced no
    # entry for (all-lowercase wire-key property names don't match PROPERTY_RE's
    # PascalCase name group — and these types are recorded method-less anyway).
    # Scoped to the generated-type namespaces so hand classes are untouched.
    if generated_type_module(namespace) is not None:
        for cls in opened_classes:
            if cls not in members:
                findings.append((namespace, cls, []))
    return findings


def _class_body_depth(scope_stack: list[tuple[str, str, int]]) -> int:
    """Return brace depth one level inside the topmost class scope."""
    for kind, _name, depth in reversed(scope_stack):
        if kind == "class":
            return depth + 1
    return -1


# ---------------------------------------------------------------------------
# PascalCase -> snake_case translation
# ---------------------------------------------------------------------------

# Acronyms preserved as single units: HTTP -> http, LLM -> llm, SIP -> sip,
# SWML -> swml, SMS -> sms, TTS -> tts, SWAIG -> swaig, AI -> ai, MCP -> mcp,
# SIP -> sip, IVR -> ivr, JSON -> json, URL -> url, ID -> id.
def pascal_to_snake(name: str) -> str:
    if name == "__init__":
        return name
    # Drop trailing "Async" — the Python reference doesn't carry it.
    if name.endswith("Async") and len(name) > 5:
        name = name[:-5]
    # Insert _ before uppercase that follows lowercase or digit.
    s1 = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", name)
    # Insert _ before uppercase that's followed by a lowercase, when preceded
    # by another uppercase (e.g. "HTTPClient" -> "HTTP_Client").
    s2 = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", s1)
    out = s2.lower()
    return METHOD_RENAMES.get(out, out)


# ---------------------------------------------------------------------------
# Module mapping
# ---------------------------------------------------------------------------

def native_namespace_to_module(namespace: str) -> str:
    """``SignalWire.Rest.Namespaces`` -> ``signalwire.rest.namespaces``."""
    return namespace.lower()


def module_for_class(class_name: str, namespace: str) -> str | None:
    if class_name in CLASS_MODULE_MAP:
        return CLASS_MODULE_MAP[class_name]
    # Fall back to native translation, with class name snake_cased as the
    # final leaf so ``SignalWire.Rest.Namespaces.PhoneNumbers`` ->
    # ``signalwire.rest.namespaces.phone_numbers``.
    leaf = pascal_to_snake(class_name)
    base = native_namespace_to_module(namespace)
    return f"{base}.{leaf}" if base else f"signalwire.{leaf}"


def emit_class_name(class_name: str) -> str:
    return SKILL_RENAMES.get(class_name, class_name)


# ---------------------------------------------------------------------------
# Generated-REST projection (SESSION_CHANGESET item A/B)
# ---------------------------------------------------------------------------
#
# The REST resource layer is code-generated (scripts/generate_rest.py) into the
# ``SignalWire.REST.Namespaces.Generated`` C# namespace. Those classes must
# project onto the python oracle's per-namespace generated modules
# ``signalwire.rest.namespaces.<ns>_resources_generated.<Name>`` and the six
# container classes onto ``signalwire.rest.namespaces._client_tree_generated``.
#
# The generator emits a manifest into rest_signatures.json so the projection
# stays exactly in lock-step with the emitted classes (GEN-FRESH covers it):
#   * ``class_module``  — resource ClassName -> "<ns>_resources_generated"
#   * ``containers``    — container ClassName -> "_client_tree_generated"
#   * ``surface``       — resource ClassName -> the EXACT canonical method-name
#                         list the oracle records (own-body methods only:
#                         inherited CRUD ops live on the base and are NOT
#                         re-recorded; the crud_base structural equivalence
#                         covers them on the signature side). We take this
#                         VERBATIM rather than the regex-parsed .cs body, since
#                         the .cs body under/over-reports (inherited create/
#                         update absent; inlined ReadResource list/get present).
#
# Containers publish only ``__init__`` — their C# property accessors are the
# .NET instance-attribute idiom (python sets ``self.x = ...`` in __init__), not
# recorded surface.
GENERATED_REST_NAMESPACE = "SignalWire.REST.Namespaces.Generated"
_REST_MODULE_PREFIX = "signalwire.rest.namespaces"

_REST_SIDECAR_PATH = (
    Path(__file__).resolve().parent.parent
    / "src" / "SignalWire" / "REST" / "Namespaces" / "Generated" / "rest_signatures.json"
)


def load_rest_manifest() -> dict:
    """Load the generator's manifest (class_module / containers / surface).
    Returns empty dicts when absent so the enumerator degrades gracefully
    pre-generation."""
    if not _REST_SIDECAR_PATH.is_file():
        return {"class_module": {}, "containers": {}, "surface": {}, "methods": {},
                "returns": {}, "crud_bases": {}}
    data = json.loads(_REST_SIDECAR_PATH.read_text(encoding="utf-8"))
    return {
        "class_module": data.get("class_module", {}),
        "containers": data.get("containers", {}),
        "surface": data.get("surface", {}),
        "methods": data.get("methods", {}),
        "returns": data.get("returns", {}),
        "crud_bases": data.get("crud_bases", {}),
    }


def generated_rest_projection(class_name: str, manifest: dict):
    """Return (target_module, method_name_list) for a generated-REST class, or
    ``None`` if this class isn't in the manifest (e.g. the ResourceTree partial,
    which is a .NET-only composition helper the hand RestClient absorbs)."""
    cm = manifest["class_module"]
    containers = manifest["containers"]
    if class_name in cm:
        return f"{_REST_MODULE_PREFIX}.{cm[class_name]}", manifest["surface"].get(class_name, [])
    if class_name in containers:
        return f"{_REST_MODULE_PREFIX}.{containers[class_name]}", ["__init__"]
    return None


# ---------------------------------------------------------------------------
# Generated method-less TYPE surface (SESSION_CHANGESET item D / H / I)
# ---------------------------------------------------------------------------
#
# The read-side typed payloads + REST wire types are code-generated
# (scripts/generate_rest.py `emit_types` + generate_swml_verbs.py /
# generate_relay_protocol.py / generate_swaig_payloads.py) into distinct C#
# namespaces — one per oracle module — so a type NAME that recurs across modules
# and collides with an SDK class name lands in the right module BY its C#
# namespace prefix (winning over the name-keyed module_for_class lookup). These
# classes are METHOD-LESS on the surface (the reference records a bare type-
# definition name); the SURFACE-DIFF gen-type leaf fold collapses cross-module
# duplicates on both sides.
#
# A prefix ending "." routes any class under that C# namespace to a FIXED oracle
# module (SWML-verbs / RELAY-proto / SWAIG payloads). The REST wire types live
# under Types.<NsMod> and map each <NsMod> to its <ns>_types_generated module.
_GENERATED_TYPE_NS_FIXED = {
    "SignalWire.Core.SwmlVerbsGenerated": "signalwire.core.swml_verbs_generated",
    "SignalWire.Relay.ProtocolTypesGenerated": "signalwire.relay.protocol_types_generated",
    "SignalWire.Core.PostPromptGenerated": "signalwire.core.post_prompt_generated",
    "SignalWire.Core.SwaigRequestGenerated": "signalwire.core.swaig_request_generated",
    "SignalWire.Core.SwaigActionsGenerated": "signalwire.core.swaig_actions_generated",
}
_GENERATED_REST_TYPES_NS = "SignalWire.REST.Namespaces.Generated.Types"
# C# Types sub-namespace segment -> oracle <ns>_types_generated leaf.
_REST_TYPES_NS_LEAF = {
    "RelayRest": "relay_rest", "Fabric": "fabric", "Calling": "calling",
    "Video": "video", "Datasphere": "datasphere", "Logs": "logs",
    "Message": "message", "Messages": "messages", "Voice": "voice",
    "Fax": "fax", "Project": "project",
    "Projects": "projects",
    "Chat": "chat", "PubSub": "pubsub", "SwmlWebhooks": "swml_webhooks",
}


def generated_type_module(namespace: str):
    """Return the oracle module for a generated method-less TYPE class by its C#
    namespace, or ``None`` if this namespace is not a generated-type namespace."""
    if namespace in _GENERATED_TYPE_NS_FIXED:
        return _GENERATED_TYPE_NS_FIXED[namespace]
    if namespace.startswith(_GENERATED_REST_TYPES_NS + "."):
        ns_mod = namespace[len(_GENERATED_REST_TYPES_NS) + 1:].split(".", 1)[0]
        leaf = _REST_TYPES_NS_LEAF.get(ns_mod)
        if leaf:
            return f"{_REST_MODULE_PREFIX}.{leaf}_types_generated"
    return None


# ---------------------------------------------------------------------------
# Top-level
# ---------------------------------------------------------------------------

def git_sha(repo: Path) -> str:
    try:
        return subprocess.check_output(
            ["git", "-C", str(repo), "rev-parse", "HEAD"],
            stderr=subprocess.DEVNULL,
        ).decode().strip()
    except Exception:
        return "N/A"


def merge_module_functions(modules: dict, target_mod: str, fns: list[str]) -> None:
    """Add ``fns`` to a module's ``functions[]`` (deduped, sorted)."""
    entry = modules.setdefault(target_mod, {"classes": {}, "functions": []})
    entry["functions"] = sorted(set(entry["functions"]) | set(fns))


def build_snapshot(repo: Path, src_dir: Path) -> dict:
    modules: dict[str, dict] = {}
    rest_manifest = load_rest_manifest()

    cs_files = sorted(src_dir.rglob("*.cs"))

    for path in cs_files:
        # Skip build artifacts
        rel = path.relative_to(repo).as_posix()
        if "/obj/" in rel or "/bin/" in rel:
            continue

        try:
            findings = parse_cs_file(path)
        except Exception as e:  # pragma: no cover
            print(f"warning: failed to parse {path}: {e}", file=sys.stderr)
            continue

        for namespace, class_name, methods in findings:
            # EffectiveRequestOptions is scaffolding for the private
            # _EffectiveOptions type-ref (plan 4.2) — never a public class in the
            # oracle. Skip emitting it as a class (it survives only as a type ref
            # via CLASS_RENAME_MAP on resolve/status_is_retryable).
            if class_name == "EffectiveRequestOptions":
                continue
            # Generated-REST projection (item A/B): the classes under
            # SignalWire.REST.Namespaces.Generated project onto the oracle's
            # <ns>_resources_generated / _client_tree_generated modules with the
            # exact method-name set from the generator manifest (NOT the parsed
            # .cs body). A class not in the manifest (ResourceTree) is skipped.
            if namespace == GENERATED_REST_NAMESPACE:
                proj = generated_rest_projection(class_name, rest_manifest)
                if proj is None:
                    continue
                target_mod, method_names = proj
                # ``paginate`` is inherited from ReadResource: the python SURFACE
                # oracle records it ONLY on the _base ReadResource class, NOT on
                # each subclass (unlike the SIGNATURE oracle, which re-records it
                # per subclass — see enumerate_signatures' surface manifest). Drop
                # it from the subclass surface set so SURFACE-DIFF matches the
                # reference; the base copy is injected below. (Mirrors list/get,
                # which the manifest already keeps off the subclass surface.)
                method_names = [m for m in method_names if m != "paginate"]
                entry = modules.setdefault(target_mod, {"classes": {}, "functions": []})
                existing = entry["classes"].get(class_name, [])
                entry["classes"][class_name] = sorted(set(existing) | set(method_names))
                continue

            # Generated method-less TYPE surface (item D/H/I): route by C#
            # namespace prefix to the oracle module, METHOD-LESS (a bare type
            # definition — the reference records these with no members on the
            # surface; the gen-type leaf fold collapses cross-module duplicates).
            gen_type_mod = generated_type_module(namespace)
            if gen_type_mod is not None:
                entry = modules.setdefault(gen_type_mod, {"classes": {}, "functions": []})
                entry["classes"].setdefault(class_name, [])
                continue

            # Free-function helper classes (item H/I): a C# static helper class
            # whose methods are the reference's MODULE-LEVEL free functions.
            # Route the methods to the module's functions[] and DO NOT emit the
            # class (Python has no such class).
            if class_name in FREE_FUNCTION_CLASSES:
                spec = FREE_FUNCTION_CLASSES[class_name]
                aliases = spec.get("aliases", {})
                keep = spec.get("keep")
                fns = []
                for m in methods:
                    snake = pascal_to_snake(m)
                    snake = aliases.get(snake, snake)
                    if keep is not None and snake not in keep:
                        continue
                    fns.append(snake)
                merge_module_functions(modules, spec["module"], fns)
                continue

            # Apply CLASS_RENAME_MAP
            if (namespace, class_name) in CLASS_RENAME_MAP:
                target_mod, target_class = CLASS_RENAME_MAP[(namespace, class_name)]
            else:
                target_mod = module_for_class(class_name, namespace)
                target_class = emit_class_name(class_name)
            if target_mod is None:
                continue

            # Translate method names
            translated = {pascal_to_snake(m) for m in methods}

            # Class-method free-function projection: move selected ``static``
            # methods off the class onto the reference module's functions[].
            if class_name in FREE_FUNCTION_PROJECTIONS:
                proj_mod, proj_names = FREE_FUNCTION_PROJECTIONS[class_name]
                present = [n for n in proj_names if n in translated]
                if present:
                    merge_module_functions(modules, proj_mod, present)
                    translated -= set(present)

            # Top-level ``signalwire`` module free-function projection.
            if class_name in TOPLEVEL_FUNCTION_PROJECTIONS:
                tops = []
                for c_name, ref_name in TOPLEVEL_FUNCTION_PROJECTIONS[class_name]:
                    if c_name in translated:
                        tops.append(ref_name)
                if tops:
                    merge_module_functions(modules, "signalwire", tops)

            # Per-class method-name aliases (idiom -> reference name).
            alias_table = SURFACE_METHOD_ALIASES.get((target_mod, target_class), {})
            if alias_table:
                translated = {alias_table.get(m, m) for m in translated}

            # Reference-present dunders the class semantically has.
            for inj in SURFACE_METHOD_INJECTIONS.get((target_mod, target_class), []):
                translated.add(inj)

            # Method allowlist: drop idiomatic data-properties for classes with
            # a fixed reference contract (kept dunders like __init__ survive if
            # in the allowlist). Relay event classes -> from_payload only.
            allow = SURFACE_METHOD_ALLOWLIST.get((target_mod, target_class))
            if allow is not None:
                translated &= allow
            elif target_mod == "signalwire.relay.event":
                translated &= _RELAY_EVENT_ONLY

            entry = modules.setdefault(target_mod, {"classes": {}, "functions": []})
            existing = entry["classes"].get(target_class, [])
            entry["classes"][target_class] = sorted(set(existing) | translated)

    # Mixin projections: replicate methods present on AgentBase under each
    # Python mixin module, then REMOVE them from AgentBase so the diff
    # against python_surface.json doesn't flag them as extras (Python keeps
    # them only on the mixin class).
    #
    # .NET's AgentBase INHERITS SignalWire.SWML.Service (the SWMLService), so a
    # method Python composes onto AgentBase via a mixin may live on either the
    # AgentBase C# file OR the Service base. Pool both so web/tool/auth methods
    # declared on Service still project (they satisfy AgentBase parity via
    # inheritance). Projection removes them only from AgentBase's own list.
    ab_module = modules.get("signalwire.core.agent_base", {})
    ab_own = set(ab_module.get("classes", {}).get("AgentBase", []))
    svc_module = modules.get("signalwire.core.swml_service", {})
    svc_methods = set(svc_module.get("classes", {}).get("SWMLService", []))
    ab_methods = ab_own | svc_methods
    projected: set[str] = set()
    for (mod, cls), expected_methods in MIXIN_PROJECTIONS.items():
        present = [m for m in expected_methods if m in ab_methods]
        entry = modules.setdefault(mod, {"classes": {}, "functions": []})
        # UNION with any already-enumerated real class surface: PromptManager /
        # ToolRegistry are REAL C# classes (their own __init__ / methods must be
        # preserved) that are ALSO projection targets — merge, never overwrite.
        existing = set(entry["classes"].get(cls, []))
        entry["classes"][cls] = sorted(existing | set(present))
        projected.update(present)
    if "signalwire.core.agent_base" in modules:
        ab_classes = modules["signalwire.core.agent_base"].get("classes", {})
        if "AgentBase" in ab_classes:
            ab_classes["AgentBase"] = sorted(
                set(ab_classes["AgentBase"]) - projected
            )

    # Now restrict SWMLService to its reference own-surface set (mixin pooling
    # already consumed its full method list above).
    if "signalwire.core.swml_service" in modules:
        swml_classes = modules["signalwire.core.swml_service"]["classes"]
        if "SWMLService" in swml_classes:
            swml_classes["SWMLService"] = sorted(
                set(swml_classes["SWMLService"]) & _SWML_SERVICE_ALLOW
            )

    # Relay Action control surface: the oracle projects stop/pause/resume/volume
    # directly onto each concrete action. `stop` lives on the shared C# Action
    # base (via GetStopMethod) and pause/resume/volume are declared on the
    # concrete classes; project the canonical control-method names onto each
    # concrete action per RELAY_ACTION_CONTROL_METHODS so the surface compares
    # equal to the reference. No synthetic base classes are emitted.
    call_mod = modules.get("signalwire.relay.call")
    if call_mod is not None:
        call_classes = call_mod["classes"]
        for cls_name, controls in RELAY_ACTION_CONTROL_METHODS.items():
            if cls_name in call_classes:
                call_classes[cls_name] = sorted(
                    set(call_classes[cls_name]) | set(controls)
                )

    # Webhook middleware decomposed validator: the oracle exposes
    # ``signalwire.core.security.webhook_middleware.validate`` as a module
    # free-function (the framework-free request-handler shape). .NET ships this
    # capability as the instance method WebhookValidationMiddleware.Validate
    # (signing_key bound on the constructed middleware). Project the free-function
    # name onto the module so the surface compares equal — mirrors the identical
    # projection in enumerate_signatures.py. The constructable middleware class +
    # its idiomatic Validate surface STAYS a PORT_ADDITION alongside the core.
    _wh_mod = modules.get("signalwire.core.security.webhook_middleware")
    if _wh_mod is not None:
        _wh_cls = _wh_mod["classes"].get("WebhookValidationMiddleware")
        if _wh_cls and "validate" in _wh_cls:
            _wh_mod.setdefault("functions", [])
            if "validate" not in _wh_mod["functions"]:
                _wh_mod["functions"].append("validate")

    # REST base-class consolidation (item H): Python declares an abstract base
    # hierarchy in signalwire.rest._base — BaseResource(__init__) ->
    # ReadResource(get,list) -> CrudResource(create,delete,update), plus the
    # method-less FabricResource / FabricResourcePUT PATCH/PUT marker bases. .NET
    # folds read+base behavior into the single concrete CrudResource (its get /
    # list / __init__ ARE present). Emit the reference base names in _base so the
    # consolidated hierarchy compares equal — the capability is real on the C#
    # CrudResource; only the base-class SPLIT is a language idiom.
    base_mod = modules.setdefault("signalwire.rest._base", {"classes": {}, "functions": []})
    base_mod["classes"].setdefault("BaseResource", ["__init__"])
    base_mod["classes"].setdefault("ReadResource", ["get", "list", "paginate"])
    base_mod["classes"].setdefault("FabricResource", [])
    base_mod["classes"].setdefault("FabricResourcePUT", [])

    # Skill subclasses: drop the data-carrying property extras (name /
    # description / …) and project the SkillBase-inherited methods each skill's
    # reference records (real, callable via C# inheritance — see the tables).
    for mod_name, entry in modules.items():
        if not (mod_name.startswith("signalwire.skills.")
                and mod_name.endswith(".skill")):
            continue
        for cls_name, meths in list(entry["classes"].items()):
            kept = [m for m in meths if m not in _SKILL_PROPERTY_EXTRAS]
            for inj in SKILL_INHERITED_PROJECTIONS.get(cls_name, []):
                if inj in _SKILLBASE_INHERITABLE and inj not in kept:
                    kept.append(inj)
            entry["classes"][cls_name] = sorted(set(kept))

    # Top-level ``signalwire`` module function names that are class re-exports
    # (e.g. ``RestClient``) — the reference records these in functions[].
    if TOPLEVEL_FUNCTION_NAMES:
        merge_module_functions(modules, "signalwire", TOPLEVEL_FUNCTION_NAMES)

    # RequestOptions (plan 4.2) surface == the reference's: ONLY merge(). .NET's
    # record exposes abort_signal (and the other fields) as public properties, but
    # Python's surface lists only merge() — the dataclass fields are not surface
    # symbols (nor in go/ts/ruby/java). Reduce to the reference-canonical surface;
    # the fields still reconcile at the signatures layer. Keeps the fleet uniform.
    _ro = modules.get("signalwire.rest._request_options", {}).get("classes", {})
    if "RequestOptions" in _ro:
        _ro["RequestOptions"] = ["merge"]

    # Sort module dict deterministically
    sorted_modules = {k: modules[k] for k in sorted(modules.keys())}

    # Drop empty modules
    sorted_modules = {
        k: v for k, v in sorted_modules.items()
        if v["classes"] or v["functions"]
    }

    return {
        "version": "1",
        "generated_from": f"signalwire-dotnet @ {git_sha(repo)}",
        "modules": sorted_modules,
    }


def build_native_names(repo: Path, src_dir: Path) -> dict:
    """Emit the port's REAL native C# member names — PascalCase, ``Async`` suffix
    intact, class names verbatim — as a flat sorted set.

    ``port_surface.json`` records the CANONICAL (python-oracle, snake_case) surface
    so SURFACE-DIFF compares equal to the reference; that translation strips the
    ``Async`` suffix and snake_cases everything (``AnswerAsync`` -> ``answer``). A
    documentation example, though, references the actual shipped C# member
    (``call.AnswerAsync()``), which the canonical surface cannot resolve.

    DOC-AUDIT resolves doc references against this sidecar (via ``--native-names``)
    so a GENUINELY-present async method resolves, while a PHANTOM (``StopAsync`` on
    an Action that only has sync ``Stop()``) still fails — the sidecar carries only
    the members that are actually declared ``public`` in the source. This is idiom
    reconciled through the enumerator (RULES §2), NOT a doc-audit omission/ledger.
    """
    cs_files = sorted(src_dir.rglob("*.cs"))
    names: set[str] = set()
    for path in cs_files:
        rel = path.relative_to(repo).as_posix()
        if "/obj/" in rel or "/bin/" in rel:
            continue
        try:
            findings = parse_cs_file(path)
        except Exception as e:  # pragma: no cover
            print(f"warning: failed to parse {path}: {e}", file=sys.stderr)
            continue
        for _namespace, class_name, methods in findings:
            names.add(class_name)
            for m in methods:
                if m == "__init__":
                    continue
                names.add(m)
    return {
        "version": "1",
        "generated_from": f"signalwire-dotnet @ {git_sha(repo)}",
        "native_names": sorted(names),
    }


def main(argv: list[str]) -> int:
    repo = Path(__file__).resolve().parent.parent
    default_src = repo / "src" / "SignalWire"
    default_output = repo / "port_surface.json"

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--src-dir", type=Path, default=default_src,
        help=f"Source root to walk (default: {default_src})",
    )
    parser.add_argument(
        "--output", type=Path, default=default_output,
        help=f"Where to write JSON (default: {default_output})",
    )
    parser.add_argument(
        "--stdout", action="store_true",
        help="Print JSON to stdout instead of writing --output",
    )
    parser.add_argument(
        "--check", action="store_true",
        help="Compare against the file at --output; exit 1 on drift",
    )
    args = parser.parse_args(argv)

    if not args.src_dir.is_dir():
        print(f"error: src dir not found: {args.src_dir}", file=sys.stderr)
        return 1

    snapshot = build_snapshot(repo, args.src_dir)
    rendered = json.dumps(snapshot, indent=2, sort_keys=True) + "\n"

    native = build_native_names(repo, args.src_dir)
    native_rendered = json.dumps(native, indent=2, sort_keys=True) + "\n"
    native_output = args.output.with_name("port_surface_native.json")

    def strip_meta(s: str) -> str:
        obj = json.loads(s)
        obj.pop("generated_from", None)
        return json.dumps(obj, indent=2, sort_keys=True) + "\n"

    if args.check:
        if not args.output.is_file():
            print(f"error: {args.output} does not exist", file=sys.stderr)
            return 1
        existing = args.output.read_text(encoding="utf-8")
        if strip_meta(rendered) != strip_meta(existing):
            print(
                "DRIFT: port_surface.json is stale relative to source.\n"
                "  Regenerate:\n"
                "    python3 scripts/enumerate_surface.py",
                file=sys.stderr,
            )
            return 1
        if not native_output.is_file():
            print(f"error: {native_output} does not exist", file=sys.stderr)
            return 1
        if strip_meta(native_rendered) != strip_meta(native_output.read_text(encoding="utf-8")):
            print(
                "DRIFT: port_surface_native.json is stale relative to source.\n"
                "  Regenerate:\n"
                "    python3 scripts/enumerate_surface.py",
                file=sys.stderr,
            )
            return 1
        return 0

    if args.stdout:
        sys.stdout.write(rendered)
    else:
        args.output.write_text(rendered, encoding="utf-8")
        native_output.write_text(native_rendered, encoding="utf-8")
        n_modules = len(snapshot["modules"])
        n_classes = sum(len(m["classes"]) for m in snapshot["modules"].values())
        n_methods = sum(
            sum(len(ms) for ms in m["classes"].values())
            for m in snapshot["modules"].values()
        )
        print(
            f"wrote {args.output} ({n_modules} modules, {n_classes} classes, {n_methods} methods)",
            file=sys.stderr,
        )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
