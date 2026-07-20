# TYPED_RETURNS_PLAN — dotnet mechanism for the 6.1 typed-returns wave

Machine/planning doc (not user-facing API prose). Plan 6.1
(GATE_ENFORCEMENT_PLAN_2026-07-18) schedules the `Dictionary<string,object?>`
→ typed-record returns flip as part of the coordinated **4.0 breaking wave**
(with D9-ruby/rust/java) — explicitly NOT to be half-flipped early. This doc
records the dotnet mechanism so the wave executes mechanically.

## Current state

- REST verbs return `Dictionary<string, object?>` (CrudResource.cs + the
  generated resource tree) — the #1 grade cap named by reviewers ("a
  generation behind stripe").
- The typed WIRE layers already exist and are generated:
  `REST/Namespaces/Generated/GenTypes/{SwmlVerbs,RelayProtocol,SwaigRequest,
  SwaigActions,PostPrompt}` — emitted by `scripts/generate_rest.py` and
  siblings from the canonical specs. What is missing is the REST **response**
  record layer and the return-type flip.

## Mechanism (locked strategy 2026-06-26: python-first oracle)

1. **Oracle first**: python reference lands per-resource TypedDict response
   schemas (generated from the REST specs' response models). The oracle
   regenerates; `rest_signatures.json` sidecar carries the typed response
   shape per operationId.
2. **Generator**: `scripts/generate_rest.py` gains a response-record emitter —
   per-resource `sealed record` types under
   `REST/Namespaces/Generated/GenTypes/RestResponses/`, one per GET/LIST
   response model:
   - properties are PascalCase with `[JsonPropertyName("<wire_key>")]` — wire
     keys stay snake_case, byte-identical to today's dictionaries;
   - open portions of the schema (`additionalProperties: true`) map to an
     `Extras` property (`IReadOnlyDictionary<string, object?>`), mirroring the
     closed-params + `extras` design;
   - records deserialize via `System.Text.Json` from the same response body —
     no transport change.
3. **Additive proof-of-pattern first** (the java `ConnectConfig`-overload
   shape): each resource gains typed overloads/variants returning the record
   (e.g. `Task<AddressPage> ListAsync(...)` alongside the dictionary form)
   BEFORE the flip, so consumers migrate in-place.
4. **The 4.0 flip**: the dictionary returns are replaced by the record
   returns in the breaking window; the dictionary form is deleted (no
   long-lived dual surface). CHANGELOG marks it as the 4.0 breaking set.
5. **Adapter reconciliation (DRIFT 0)**: `scripts/enumerate_signatures.py`
   already reclassifies typed params via the `rest_signatures.json` sidecar;
   the response records reconcile the same way (record → the reference's
   TypedDict response type name via the sidecar's response map). Generated
   record types are ledgered like the other generated layers — never
   hand-listed omissions.
6. **Ratchet**: GEN-TYPE-DEGENERACY's threshold drops to zero for the REST
   response surface as each namespace lands.

## Sequencing

Blocked on the python oracle's response-schema emission (cross-port, owned by
the wave) — not on anything dotnet-local. When the wave opens, steps 2-6 are
dotnet-local and mechanical.
