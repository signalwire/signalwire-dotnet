using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace SignalWire.SWML;

/// <summary>
/// Metadata about a single SWML verb parsed from the schema.
/// </summary>
/// <param name="Name">The actual verb name used in SWML documents (e.g. "answer").</param>
/// <param name="SchemaName">The definition key in the JSON schema (e.g. "Answer").</param>
/// <param name="Definition">The full JSON schema definition for this verb.</param>
public sealed record VerbInfo(string Name, string SchemaName, JsonElement Definition);

/// <summary>Validation error raised by SchemaUtils.ValidateVerb when a
/// verb config violates its schema.
/// </summary>
[SuppressMessage("Naming", "CA1710", Justification = "Type name matches the cross-port surface (Python SchemaValidationError); renaming to *Exception would break parity.")]
public class SchemaValidationError : Exception
{
    public string VerbName { get; } = "";
    public IReadOnlyList<string> Errors { get; } = Array.Empty<string>();

    public SchemaValidationError(string verbName, IReadOnlyList<string> errors)
        : base($"Schema validation failed for verb '{verbName}': {string.Join("; ", errors ?? Array.Empty<string>())}")
    {
        VerbName = verbName;
        Errors = errors ?? Array.Empty<string>();
    }

    public SchemaValidationError()
    {
    }

    public SchemaValidationError(string message)
        : base(message)
    {
    }

    public SchemaValidationError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thread-safe singleton that loads the SWML JSON schema from an embedded resource
/// and exposes verb definitions parsed from $defs.SWMLMethod.anyOf.
/// </summary>
public sealed class Schema
{
    private static Schema? _instance;
    private static readonly object Lock = new();

    private readonly Dictionary<string, VerbInfo> _verbs = new();

    // The compiled Draft 2020-12 validator over the bundled SWML schema (the
    // .NET analogue of Python's jsonschema-rs Draft202012Validator). Null when
    // the schema failed to compile — then ValidateVerb degrades to the
    // lightweight required-property check, matching every port's fallback.
    private readonly JsonSchema? _fullValidator;

    // The raw schema root as a JsonNode, used to introspect a verb's closed
    // top-level key-set for the shallow (handler-verb) check.
    private JsonNode? _schemaRoot;

    private Schema()
    {
        LoadSchema();
        _fullValidator = InitFullValidator();
    }

    /// <summary>Thread-safe singleton accessor.</summary>
    /// <remarks>
    /// Returns the instance OBSERVED UNDER THE LOCK, never a re-read of the field.
    /// The previous form assigned inside the lock and then <c>return _instance;</c>
    /// outside it, so a concurrent <see cref="Reset"/> — which nulls the field under
    /// the same lock — could land in that window and make this property hand back
    /// <see langword="null"/> from a non-nullable getter. Callers then died on a
    /// NullReferenceException that pointed at their own dereference rather than here.
    /// Real defect, not a test artifact: xunit.runner.json sets
    /// parallelizeTestCollections, and ~10 test classes call Reset() in their
    /// setup/teardown, so the window is genuinely reachable — it surfaced as a
    /// 1-in-2008 net10.0 failure in ConstructionReadbackTests
    /// (Schema.Instance.SchemaPath) that passed on every local run.
    /// </remarks>
    public static Schema Instance
    {
        get
        {
            var seen = _instance;
            if (seen is not null) return seen;
            lock (Lock)
            {
                return _instance ??= new Schema();
            }
        }
    }

    /// <summary>
    /// The location the SWML schema was loaded from. The reference resolves a
    /// caller-supplied <c>schema_path</c> (or its packaged default) and keeps it
    /// readable as <c>SchemaUtils.schema_path</c>; this port ships the schema as
    /// an assembly EmbeddedResource, so the resolved location is that resource's
    /// manifest name. Non-null and stable for the process.
    /// </summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance property matching the reference's per-instance SchemaUtils.schema_path attribute; the cross-port surface reads it off the instance.")]
    public string SchemaPath => SchemaResourceName;

    /// <summary>Manifest name of the bundled SWML schema resource.</summary>
    private const string SchemaResourceName = "SignalWire.SWML.schema.json";

    /// <summary>Reset the singleton (for testing).</summary>
    public static void Reset()
    {
        lock (Lock)
        {
            _instance = null;
        }
    }

    /// <summary>Check whether a verb name is valid.</summary>
    public bool IsValidVerb(string name) => _verbs.ContainsKey(name);

    /// <summary>Get a sorted list of all verb names.</summary>
    public IReadOnlyList<string> GetVerbNames()
    {
        var names = _verbs.Keys.ToList();
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>Get verb metadata, or null if not found.</summary>
    public VerbInfo? GetVerb(string name)
    {
        return _verbs.TryGetValue(name, out var info) ? info : null;
    }

    /// <summary>Number of verbs defined in the schema.</summary>
    public int VerbCount => _verbs.Count;

    /// <summary>Alias of <see cref="GetVerbNames"/>.</summary>
    public IReadOnlyList<string> GetAllVerbNames() => GetVerbNames();

    /// <summary>Public load-schema accessor. Returns the embedded SWML
    /// schema as a Dictionary&lt;string, JsonElement&gt;. Empty dict
    /// when the schema can't be loaded.
    /// </summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance accessor matching the cross-port surface (Python SchemaUtils.load_schema); kept non-static so callers reach it via Schema.Instance.")]
    public Dictionary<string, JsonElement> LoadSchemaPublic()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("SignalWire.SWML.schema.json");
        if (stream is null) return new Dictionary<string, JsonElement>();
        using var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            result[prop.Name] = prop.Value.Clone();
        }
        return result;
    }

    /// <summary>Get the parameter (property) definitions for a verb.
    /// Returns an empty dict when the verb is unknown or has no
    /// ``properties``.
    /// </summary>
    public Dictionary<string, JsonElement> GetVerbParameters(string verbName)
    {
        var verb = GetVerb(verbName);
        if (verb is null) return new Dictionary<string, JsonElement>();
        if (!verb.Definition.TryGetProperty("properties", out var props)
            || props.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, JsonElement>();
        }
        var result = new Dictionary<string, JsonElement>();
        foreach (var p in props.EnumerateObject())
        {
            result[p.Name] = p.Value.Clone();
        }
        return result;
    }

    /// <summary>Validate a SWML document against the loaded schema.
    /// Returns ``(true, [])`` on success or ``(false, [errors...])`` on
    /// failure. Lightweight verb-presence check — full JSON-Schema
    /// validation is out of scope for the bundled SDK.
    /// </summary>
    public (bool Valid, List<string> Errors) ValidateDocument(Dictionary<string, object> document)
    {
        var errors = new List<string>();
        if (document is null)
        {
            errors.Add("document must not be null");
            return (false, errors);
        }
        if (!document.TryGetValue("sections", out var sectionsObj)
            || sectionsObj is not Dictionary<string, List<Dictionary<string, object?>>> sections)
        {
            errors.Add("document missing 'sections' or wrong shape");
            return (false, errors);
        }
        foreach (var section in sections)
        {
            foreach (var verbHash in section.Value)
            {
                foreach (var verbKv in verbHash)
                {
                    if (!IsValidVerb(verbKv.Key))
                    {
                        errors.Add($"unknown verb: {verbKv.Key}");
                    }
                }
            }
        }
        return (errors.Count == 0, errors);
    }

    // ------------------------------------------------------------------
    // SchemaUtils parity: full-validation availability, property/required
    // introspection, per-verb validation, and method-source generation.
    // ------------------------------------------------------------------

    /// <summary>True when the full JSON-Schema validator is wired up (the
    /// embedded schema compiled).
    /// </summary>
    public bool FullValidationAvailable() => _fullValidator is not null;

    /// <summary>Get the ``properties`` object for a verb (its parameter
    /// definitions).</summary>
    public Dictionary<string, JsonElement> GetVerbProperties(string verbName)
        => GetVerbParameters(verbName);

    /// <summary>Get the list of required property names for a verb.</summary>
    public IReadOnlyList<string> GetVerbRequiredProperties(string verbName)
    {
        var verb = GetVerb(verbName);
        if (verb is null
            || !verb.Definition.TryGetProperty("required", out var req)
            || req.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return [.. req.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)];
    }

    /// <summary>Validate a single verb configuration against the schema.
    /// When the full validator is available (the normal case), the config is
    /// wrapped in a minimal SWML document and validated against the bundled
    /// Draft 2020-12 schema — so unknown/misspelled keys and wrong types are
    /// rejected, not silently dropped (the STRICT-RENDER contract). Falls back
    /// to a lightweight required-property check when the validator failed to
    /// compile. Returns ``(true, [])`` on success else ``(false, [errors...])``.
    /// </summary>
    public (bool Valid, List<string> Errors) ValidateVerb(
        string verbName, Dictionary<string, object?> verbConfig)
    {
        ArgumentNullException.ThrowIfNull(verbConfig);
        if (!IsValidVerb(verbName))
        {
            return (false, new List<string> { $"Unknown verb: {verbName}" });
        }
        if (_fullValidator is not null)
        {
            return ValidateVerbFull(verbName, verbConfig);
        }
        return ValidateVerbLightweight(verbName, verbConfig);
    }

    /// <summary>Full Draft 2020-12 validation: wrap the verb in a minimal SWML
    /// document (as Python's ``_validate_verb_full`` does) so the schema's
    /// closed-object (``unevaluatedProperties``), type, and required checks fire
    /// against the real document context.</summary>
    private (bool Valid, List<string> Errors) ValidateVerbFull(
        string verbName, Dictionary<string, object?> verbConfig)
    {
        JsonNode? configNode;
        try
        {
            // Serialize the config through System.Text.Json so nested
            // Dictionary/List/scalar values become a JsonNode the validator reads.
            var configJson = JsonSerializer.Serialize(verbConfig);
            configNode = JsonNode.Parse(configJson);
        }
        catch (JsonException e)
        {
            return (false, new List<string> { $"Schema validation error for '{verbName}': {e.Message}" });
        }

        var minimalDoc = new JsonObject
        {
            ["version"] = "1.0.0",
            ["sections"] = new JsonObject
            {
                ["main"] = new JsonArray(new JsonObject { [verbName] = configNode }),
            },
        };

        var result = _fullValidator!.Evaluate(
            minimalDoc, new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        if (result.IsValid)
        {
            return (true, new List<string>());
        }
        return (false, new List<string> { $"Schema validation error for '{verbName}'" });
    }

    /// <summary>Lightweight fallback: verb existence + required-property check
    /// only (Python's ``_validate_verb_lightweight``).</summary>
    private (bool Valid, List<string> Errors) ValidateVerbLightweight(
        string verbName, Dictionary<string, object?> verbConfig)
    {
        var errors = new List<string>();
        foreach (var required in GetVerbRequiredProperties(verbName))
        {
            if (!verbConfig.ContainsKey(required))
            {
                errors.Add($"Missing required property '{required}' for verb '{verbName}'");
            }
        }
        return (errors.Count == 0, errors);
    }

    /// <summary>Shallow STRICT-RENDER check for HANDLER verbs (the ai verb):
    /// reject unknown/misspelled TOP-LEVEL keys against the schema's known
    /// property set, WITHOUT running the full deep schema. Full-deep-validating
    /// the ai verb false-rejects legitimately-emitted deep shapes (an empty
    /// prompt.pom for a promptless agent, SWAIG defaults/functions[].web_hook_url
    /// / __token), so the handler owns the deep shape and only stray top-level
    /// keys (e.g. ``temperatur`` / ``zzz``) are caught here. A no-op when the
    /// verb has no enumerable closed key-set.
    /// </summary>
    public (bool Valid, List<string> Errors) ValidateVerbTopLevelKeys(
        string verbName, Dictionary<string, object?> verbConfig)
    {
        ArgumentNullException.ThrowIfNull(verbConfig);
        if (!IsValidVerb(verbName))
        {
            return (false, new List<string> { $"Unknown verb: {verbName}" });
        }
        var known = VerbTopLevelPropertyNames(verbName);
        if (known is null)
        {
            // No enumerable closed key-set — nothing shallow to enforce.
            return (true, new List<string>());
        }
        var unknown = verbConfig.Keys.Where(k => !known.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            var knownList = known.OrderBy(k => k, StringComparer.Ordinal).ToList();
            return (false, new List<string>
            {
                $"Unknown/misspelled key(s) [{string.Join(", ", unknown)}] for verb "
                + $"'{verbName}'. Known keys: [{string.Join(", ", knownList)}]",
            });
        }
        return (true, new List<string>());
    }

    /// <summary>Resolve the set of KNOWN top-level property names for a verb's
    /// config object, following a single ``$ref`` (e.g. ai -> AIObject). Returns
    /// null when the verb's config schema is not a CLOSED object-with-properties
    /// (no enumerable known-key set, so no shallow check applies).
    /// </summary>
    private HashSet<string>? VerbTopLevelPropertyNames(string verbName)
    {
        var verb = GetVerb(verbName);
        if (verb is null
            || !verb.Definition.TryGetProperty("properties", out var props)
            || props.ValueKind != JsonValueKind.Object
            || !props.TryGetProperty(verbName, out var body)
            || body.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Follow a single $ref (ai -> AIObject) to the object that declares the
        // verb config's own properties.
        if (body.TryGetProperty("$ref", out var refProp) && refProp.ValueKind == JsonValueKind.String)
        {
            var refValue = refProp.GetString()!;
            var refName = refValue[(refValue.LastIndexOf('/') + 1)..];
            if (_schemaRoot is not JsonObject rootObj
                || rootObj["$defs"] is not JsonObject defs
                || defs[refName] is not JsonObject refBody)
            {
                return null;
            }
            // Re-read the resolved def as a JsonElement for uniform handling.
            var refJson = refBody.ToJsonString();
            using var refDoc = JsonDocument.Parse(refJson);
            body = refDoc.RootElement.Clone();
        }

        if (!body.TryGetProperty("type", out var typeProp)
            || typeProp.ValueKind != JsonValueKind.String
            || typeProp.GetString() != "object"
            || !body.TryGetProperty("properties", out var propMap)
            || propMap.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Only meaningful as a closed-key check when the schema itself closes the
        // object (additionalProperties:false, unevaluatedProperties:false, or the
        // SWML idiom unevaluatedProperties:{"not":{}}).
        var closes = false;
        if (body.TryGetProperty("additionalProperties", out var ap)
            && ap.ValueKind == JsonValueKind.False)
        {
            closes = true;
        }
        if (body.TryGetProperty("unevaluatedProperties", out var up))
        {
            if (up.ValueKind == JsonValueKind.False)
            {
                closes = true;
            }
            else if (up.ValueKind == JsonValueKind.Object
                && up.TryGetProperty("not", out var notVal)
                && notVal.ValueKind == JsonValueKind.Object
                && !notVal.EnumerateObject().Any())
            {
                // unevaluatedProperties: {"not": {}} — an empty `not` nothing
                // satisfies, so any unevaluated property is rejected.
                closes = true;
            }
        }
        if (!closes)
        {
            return null;
        }

        var known = new HashSet<string>();
        foreach (var p in propMap.EnumerateObject())
        {
            known.Add(p.Name);
        }
        return known;
    }

    /// <summary>Compile the bundled SWML schema into a Draft 2020-12 validator.
    /// Returns null on any load/compile failure so ValidateVerb degrades to the
    /// lightweight check (matching Python's behaviour when jsonschema-rs is
    /// unavailable). Also caches the schema root as a JsonNode for the
    /// top-level-key introspection.</summary>
    private JsonSchema? InitFullValidator()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("SignalWire.SWML.schema.json");
            if (stream is null)
            {
                return null;
            }
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            _schemaRoot = JsonNode.Parse(text);

            // Relax every x-sdk-widen-marked field BEFORE compiling, so the
            // validator is built already-widened. Doing it here (rather than
            // per-validation) means every consumer of _fullValidator gets the
            // same relaxed contract, and it costs nothing per request.
            var widened = JsonNode.Parse(text);
            var relaxed = ApplySdkWiden(widened);
            return JsonSchema.FromText(
                (relaxed > 0 ? widened : JsonNode.Parse(text))!.ToJsonString());
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// The schema-extension key marking a field whose const-union/enum is a
    /// HINT, not a closed set. Held as a constant so the literal never has to
    /// appear in a doc comment (where it would read as internal jargon).
    /// </summary>
    private const string WidenKey = "x-sdk-widen";

    /// <summary>
    /// Relax every field the schema marks as open-valued, in place.
    ///
    /// <para>The marked field lists a few known values as a convenience, but the
    /// platform accepts ANY value of the underlying base type. Enforcing the
    /// listed set would reject documents the platform happily runs — the
    /// failure direction nobody looks for, because a too-strict validator looks
    /// like a working validator until a legitimate value shows up.</para>
    ///
    /// <para>Widening drops the value-constraining keywords
    /// (<c>anyOf</c>/<c>oneOf</c>/<c>enum</c>/<c>const</c>) and <b>sets</b> the
    /// base type recovered from the branches it removed. Recovering the type is
    /// load-bearing, not cosmetic: the one marked field today declares no
    /// <c>type</c> of its own, so simply deleting its branches would leave a
    /// schema that accepts a number or an object where only a string belongs.
    /// The <c>type</c> keyword is never stripped.</para>
    /// </summary>
    /// <returns>How many fields were relaxed.</returns>
    private static int ApplySdkWiden(JsonNode? node)
    {
        var count = 0;
        switch (node)
        {
            case JsonArray arr:
                foreach (var item in arr)
                {
                    count += ApplySdkWiden(item);
                }
                break;

            case JsonObject obj:
                if (obj.TryGetPropertyValue(WidenKey, out var marker)
                    && marker is JsonValue mv
                    && mv.TryGetValue<bool>(out var on)
                    && on)
                {
                    var baseType = RecoverBaseType(obj);
                    foreach (var key in new[] { "anyOf", "oneOf", "enum", "const" })
                    {
                        obj.Remove(key);
                    }
                    if (baseType is not null && !obj.ContainsKey("type"))
                    {
                        obj["type"] = baseType;
                    }
                    count++;
                }

                // Recurse into whatever survives (nested marked fields, and the
                // rest of the tree).
                foreach (var key in obj.Select(kvp => kvp.Key).ToList())
                {
                    count += ApplySdkWiden(obj[key]);
                }
                break;

            default:
                break;
        }
        return count;
    }

    /// <summary>
    /// Recover the base type a widened field's removed branches all shared —
    /// an own <c>type</c> if it has one, else the single <c>type</c> every
    /// <c>anyOf</c>/<c>oneOf</c> branch agreed on. Returns null when the
    /// branches disagree (nothing safe to assert) so the field is left
    /// unconstrained rather than wrongly narrowed.
    /// </summary>
    private static string? RecoverBaseType(JsonObject obj)
    {
        if (obj.TryGetPropertyValue("type", out var own)
            && own is JsonValue ov && ov.TryGetValue<string>(out var ownType))
        {
            return ownType;
        }

        string? recovered = null;
        foreach (var key in new[] { "anyOf", "oneOf" })
        {
            if (!obj.TryGetPropertyValue(key, out var branches) || branches is not JsonArray list)
            {
                continue;
            }
            foreach (var branch in list)
            {
                if (branch is not JsonObject bo
                    || !bo.TryGetPropertyValue("type", out var bt)
                    || bt is not JsonValue bv
                    || !bv.TryGetValue<string>(out var branchType))
                {
                    return null;
                }
                if (recovered is null)
                {
                    recovered = branchType;
                }
                else if (recovered != branchType)
                {
                    return null;
                }
            }
        }
        return recovered;
    }

    /// <summary>Generate a C#/pseudocode method signature for a verb (used by
    /// codegen/tooling).</summary>
    public string GenerateMethodSignature(string verbName)
    {
        var parameters = string.Join(", ",
            GetVerbParameters(verbName).Keys.Select(k => $"object? {k} = null"));
        return $"public void {verbName}({parameters})";
    }

    /// <summary>Generate a method-body stub that adds the verb to the document.</summary>
    public string GenerateMethodBody(string verbName)
    {
        var keys = GetVerbParameters(verbName).Keys.ToList();
        var assigns = string.Join("\n", keys.Select(
            k => $"    if ({k} != null) config[\"{k}\"] = {k};"));
        return $"var config = new Dictionary<string, object>();\n{assigns}\n"
             + $"AddVerb(\"{verbName}\", config);";
    }

    private void LoadSchema()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("SignalWire.SWML.schema.json")
            ?? throw new InvalidOperationException(
                "SWML schema.json not found as embedded resource. "
                + "Ensure it is included as an EmbeddedResource in the .csproj.");

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        if (!root.TryGetProperty("$defs", out var defs))
        {
            throw new InvalidOperationException("Schema missing '$defs' section");
        }

        if (!defs.TryGetProperty("SWMLMethod", out var swmlMethod))
        {
            throw new InvalidOperationException("Schema missing '$defs.SWMLMethod' section");
        }

        if (!swmlMethod.TryGetProperty("anyOf", out var anyOf))
        {
            throw new InvalidOperationException("Schema missing '$defs.SWMLMethod.anyOf' array");
        }

        foreach (var entry in anyOf.EnumerateArray())
        {
            if (!entry.TryGetProperty("$ref", out var refProp))
            {
                continue;
            }

            var refValue = refProp.GetString();
            if (refValue is null)
            {
                continue;
            }

            // e.g. "#/$defs/Answer" -> "Answer"
            var lastSlash = refValue.LastIndexOf('/');
            if (lastSlash < 0)
            {
                continue;
            }
            var defName = refValue[(lastSlash + 1)..];

            if (!defs.TryGetProperty(defName, out var defn))
            {
                continue;
            }

            if (!defn.TryGetProperty("properties", out var props))
            {
                continue;
            }

            // The first property key is the actual verb name
            string? actualVerb = null;
            foreach (var prop in props.EnumerateObject())
            {
                actualVerb = prop.Name;
                break;
            }

            if (actualVerb is null)
            {
                continue;
            }

            _verbs[actualVerb] = new VerbInfo(actualVerb, defName, defn.Clone());
        }
    }
}
