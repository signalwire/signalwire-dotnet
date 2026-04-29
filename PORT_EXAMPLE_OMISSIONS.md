# PORT_EXAMPLE_OMISSIONS.md (signalwire-dotnet)

Python examples deliberately not ported to this .NET SDK. Format:

```
- `<python_example_stem>` — <one-line rationale>
```

`scripts/audit_example_parity.py` reads this file (it parses list items
under the standard ``- `name`/<name>`` markdown convention) to know
which Python examples to ignore when checking parity.

---

## Search-related (skip list)

- `local_search_agent` — Python-only example showing the
  `native_vector_search` skill, which depends on the search subsystem
  (sentence-transformers / pgvector / faiss). Per the porting-sdk skip
  list, search is Python-only and the .NET port doesn't ship a local
  search agent. The cross-port skip regex catches `*_search_*` only when
  it starts at column zero or after an underscore boundary, so this
  prefix-`local_` form slips through; recording explicitly here.
