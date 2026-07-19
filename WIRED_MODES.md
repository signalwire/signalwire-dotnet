# WIRED_MODES — load-bearing run-ci modes (plan 1.6 / D7)

The strict-mocks × Part-5 merge race silently DROPPED load-bearing env/mode lines from
individual ports' `scripts/run-ci.sh` (a strict export un-set, a gate then green-and-
vacuous). This manifest is the merge-coherence guard: each line below is a regex the
WIRED-MODES gate (`check_wired_modes.py`) requires to be present in `scripts/run-ci.sh`.
If a future merge drops one, the gate reds instead of shipping a vacuous strict lane.

Format (one required pattern per line): `` - `<python-regex>` — <why it is load-bearing> ``.
Prose/headers/comments are ignored, so this file doubles as human documentation.

- `MOCK_RELAY_STRICT=1` — RELAY strict mode: the nightly EXAMPLES-RUN/SNIPPET-RUN lanes carry the shared mock's 400-on-violation mode so the moment dotnet gains a run target, a wrong-wire example fails loud instead of being tolerantly journaled (STRICT-MOCKS parity).
- `export MOCK_SIGNALWIRE_STRICT` — REST 400 strict default (D3): the REST mock returns 400 on an unknown key / wrong type instead of tolerantly journaling it; exported before the gate-owned mocks spawn so REST-COVERAGE + TEST catch a regression the tolerant mock would swallow.
