# WIRE_VIOLATIONS_ALLOW.md — signed exceptions to the STRICT-MOCKS wire-truth gate

The STRICT-MOCKS consumer (`porting-sdk/scripts/assert_no_wire_violations.py`, wired
into REST-COVERAGE) reads the mock journal after a run and fails on ANY
`wire_violation` — a request/frame that put a shape on the wire the OpenAPI/RELAY
spec does not declare (an undeclared query param, an unknown body key, an unknown
frame field). A wire violation is a spec bug or a real defect; the fix is to make
the wire match the spec, NOT to allowlist it.

This file exists for the rare, genuinely-justified exception, and each entry needs a
human-signed reason. Format (one per line):

    - <kind>:<name> — reason (approver, date)

where `<kind>` is the violation kind (`unknown_query_param`, `unknown_body_key`,
`unknown_frame_field`, `duplicate_command_id`) and `<name>` is the offending
key/param name. A bare `kind:name` with no ` — reason` is NOT matched, so it cannot
silently widen the allowlist.

## Currently empty

No entries. The wired REST-COVERAGE gate (`rest_coverage_gate` in `scripts/run-ci.sh`)
runs ONLY the `[Trait("Category", "RestCoverage")]` generated coverage suite
(`tests/RestMock/Generated/*.cs`) against its dedicated mock — that suite is
spec-generated and runs wire-clean against the reference.

Two spec gaps surfaced during STRICT-MOCKS bring-up but do NOT need a park here,
because the tests that probe them carry `[Trait("Category", "RestMock")]` (the
hand-authored fixture suite), a category the coverage gate's `--filter
"Category=RestCoverage"` never selects, so they never reach this journal:

  * `page_size` on `relay-rest.list_recordings` — the spec's `list_recordings` op
    has `parameters: []` while every sibling `list_*` op declares `page_size`
    (`tests/RestMock/SmallNamespacesMockTest.cs`, `RecordingsList`).
  * `page_token` on `fabric.list_fabric_addresses` — same class: `parameters: []`,
    but the server returns a `links.next` cursor URL the SDK's `PaginatedIterator`
    replays as `?page_token=` (`tests/RestMock/PaginationMockTest.cs`,
    `Next_PagesThroughAllItems`; `tests/RestMock/FabricMockTest.cs`,
    `Addresses_Paginate_FollowsCursorAcrossTwoPages`).

Both are owner-approved to FIX THE SPEC (declare the missing param); fixed on
porting-sdk branch `fix/recordings-pagination-spec` (not yet merged to `main` as of
this writing — re-drift once it lands, at which point these two also stop firing
under the plain `Category=RestMock` run).
