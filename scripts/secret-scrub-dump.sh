#!/usr/bin/env bash
# secret-scrub-dump.sh — the SECRET-SCRUB-LIVE (PSDK-5) Layer-D dump wrapper.
#
# Thin delegate to dump-corpus.sh, mirroring scripts/secure-default-dump.sh: the
# DumpCorpus console app carries a `secret-scrub` surface that drives the REAL
# RELAY Client through a real WebSocket connect + an inbound
# signalwire.authorization.state re-auth frame with the fixture sentinels
# (project=PJ-TESTLEAK, token=PT-TESTLEAK, authorization_state=AENC-TESTLEAK),
# captures its own log output, and emits the per-sentinel {leaked} classification
# as ONE JSON object on stdout.
#
# SIGNALWIRE_LOG_LEVEL=debug is exported HERE, not inside the dump: SignalWire's
# Logger snapshots the level from the environment when its singleton is first
# constructed, so setting it after the process starts would be too late and the
# capture would come back empty (the dump asserts non-emptiness and fails loud
# rather than reporting a vacuous leaked=false).
#
# porting-sdk/scripts/diff_port_secret_scrub.py drives this via
#   --dump-cmd 'bash scripts/secret-scrub-dump.sh'
# and structurally compares the result against the python oracle (every sentinel
# must be {leaked: false}).
set -u
PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export SIGNALWIRE_LOG_LEVEL=debug
unset SIGNALWIRE_LOG_MODE
exec bash "$PORT_ROOT/scripts/dump-corpus.sh" secret-scrub
