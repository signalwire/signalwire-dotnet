#!/usr/bin/env bash
# secure-default-dump.sh — the SECURE-DEFAULT (A1) Layer-D dump wrapper.
#
# Thin delegate to dump-corpus.sh: the DumpCorpus console app carries a
# `secure-default` surface that defines a default (secure) + a secure=False tool,
# renders the SWML with the fixed corpus call_id, and emits the per-fixture
# {secure_default_true, wire_reflects_secure} classification as ONE JSON object on
# stdout. porting-sdk/scripts/diff_port_secure_default.py drives this via
#   --dump-cmd 'bash scripts/secure-default-dump.sh'
# and byte-compares the result against the python oracle.
set -u
PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
exec bash "$PORT_ROOT/scripts/dump-corpus.sh" secure-default
