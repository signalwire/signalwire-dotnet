#!/usr/bin/env bash
# check-nupkg-xmldoc.sh — NUPKG-XMLDOC gate (6.3 doc-surface floor, dotnet).
#
# Packs the SDK and asserts the nupkg ships the compiler XML documentation
# file (lib/<tfm>/SignalWire.xml) for EVERY target framework. This is the
# second half of the GenerateDocumentationFile pair: the csproj enables the
# doc file; this gate proves it actually lands in the shipped package (a
# silent <GenerateDocumentationFile> regression otherwise ships doc-less
# IntelliSense and nobody notices).
#
# Self-test (GATE-SELFTEST doctrine): --selftest packs a known-bad package
# (doc file forced OFF) and requires the check to go RED on it — proving the
# assertion is not vacuous.
#
# CWD-independent; scratch lives under the repo-local .sw-tmp (never /tmp).

set -euo pipefail

# shellcheck source=scripts/_env.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

DN="$(dotnet_cmd)"
CSPROJ="$REPO/src/SignalWire/SignalWire.csproj"
SCRATCH="$REPO/.sw-tmp/nupkg-xmldoc"
MODE="${1:-check}"

rm -rf "$SCRATCH"
mkdir -p "$SCRATCH"

PACK_ARGS=(pack "$CSPROJ" -c Release -o "$SCRATCH" -v quiet)
if [ "$MODE" = "--selftest" ]; then
    # Known-bad fixture: force the doc file OFF; the assertion below MUST fail.
    PACK_ARGS+=("-p:GenerateDocumentationFile=false")
fi

echo "==> dotnet ${PACK_ARGS[*]}"
"$DN" "${PACK_ARGS[@]}" 1>&2

NUPKG="$(ls "$SCRATCH"/*.nupkg 2>/dev/null | head -1)"
if [ -z "$NUPKG" ]; then
    echo "NUPKG-XMLDOC: no nupkg produced by pack" >&2
    exit 1
fi

# Assert lib/<tfm>/SignalWire.xml for every TFM the csproj targets.
check_output="$(python3 - "$NUPKG" "$CSPROJ" <<'PYEOF'
import re, sys, zipfile
nupkg, csproj = sys.argv[1], sys.argv[2]
tfms_match = re.search(r"<TargetFrameworks>([^<]+)</TargetFrameworks>", open(csproj).read())
if not tfms_match:
    print("NUPKG-XMLDOC: cannot read TargetFrameworks from csproj")
    sys.exit(1)
tfms = [t.strip() for t in tfms_match.group(1).split(";") if t.strip()]
names = set(zipfile.ZipFile(nupkg).namelist())
missing = [f"lib/{t}/SignalWire.xml" for t in tfms if f"lib/{t}/SignalWire.xml" not in names]
if missing:
    print("NUPKG-XMLDOC: XML doc file MISSING from nupkg: " + ", ".join(missing))
    sys.exit(1)
print(f"NUPKG-XMLDOC: OK — SignalWire.xml present for all {len(tfms)} TFMs ({', '.join(tfms)})")
PYEOF
)" && check_rc=0 || check_rc=$?

echo "$check_output"

if [ "$MODE" = "--selftest" ]; then
    if [ "$check_rc" -eq 0 ]; then
        echo "NUPKG-XMLDOC SELFTEST: FAIL — known-bad (doc file off) package PASSED the check (vacuous gate)" >&2
        exit 1
    fi
    echo "NUPKG-XMLDOC SELFTEST: OK — known-bad package went RED as required"
    exit 0
fi

exit "$check_rc"
