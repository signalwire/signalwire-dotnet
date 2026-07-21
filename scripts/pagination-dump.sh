#!/usr/bin/env bash
# pagination-dump.sh — the PAGINATION-CORPUS (PSDK-3) Layer-D dump wrapper.
#
# Thin delegate to dump-corpus.sh: the DumpCorpus console app carries a
# `pagination` surface that arms the corpus page sequences on a live
# mock_signalwire, drives the real PaginatedIterator, and emits the per-fixture
# {continued_past_empty/items_seen | loop_guarded/hung | terminated/total_items}
# classification as ONE JSON object on stdout.
# porting-sdk/scripts/diff_port_pagination.py drives this via
#   --dump-cmd 'bash scripts/pagination-dump.sh'
# and byte-compares the result against the python oracle.
set -u
PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
exec bash "$PORT_ROOT/scripts/dump-corpus.sh" pagination
