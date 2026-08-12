#!/usr/bin/env python3
"""Golden-test runner for the .NET signature adapter.

Builds the SignatureDumpFixtures project, runs SignatureDump against
that assembly (instead of SignalWire.dll), feeds the result through
enumerate_signatures.py, and byte-compares the per-class output to the
committed golden file.

Usage:
    python3 tests/dotnet_adapter_goldens/run_goldens.py            # verify
    python3 tests/dotnet_adapter_goldens/run_goldens.py --update   # regenerate
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PORT_ROOT = HERE.parent.parent
SCRIPTS = PORT_ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS))
from enumerate_signatures import collect, load_aliases  # type: ignore

GOLDEN = HERE / "golden"
DUMP_PROJECT = SCRIPTS / "SignatureDump" / "SignatureDump.csproj"
FIXTURES_PROJECT = SCRIPTS / "SignatureDumpFixtures" / "SignatureDumpFixtures.csproj"

# Resolve `dotnet` from PATH — no hardcoded machine path. Fail loud if absent.
DOTNET = shutil.which("dotnet")
if not DOTNET:
    raise SystemExit("run_goldens.py: `dotnet` not found on PATH")


def build_fixtures() -> Path:
    cp = subprocess.run(
        [DOTNET, "build", str(FIXTURES_PROJECT)],
        capture_output=True,
        text=True,
        timeout=300,
    )
    if cp.returncode != 0:
        raise SystemExit(f"fixtures build failed:\n{cp.stderr}\n{cp.stdout}")
    # Find the built assembly
    bin_dir = FIXTURES_PROJECT.parent / "bin" / "SignatureDumpFixtures"
    dlls = list(bin_dir.rglob("SignatureDumpFixtures.dll"))
    if not dlls:
        raise SystemExit(f"no SignatureDumpFixtures.dll under {bin_dir}")
    return dlls[0]


def run_dump_against(dll: Path) -> dict:
    """Run a tweaked SignatureDump that loads the given DLL via reflection."""
    # The default SignatureDump is wired to the SignalWire assembly. For
    # the goldens we use a tiny inline C# program that loads our fixture
    # DLL by path and dumps types under the GoldenFixtures namespace.
    helper = HERE / "DumpFixtures" / "DumpFixtures.csproj"
    cp = subprocess.run(
        [DOTNET, "run", "--project", str(helper), "--", str(dll)],
        capture_output=True,
        text=True,
        timeout=300,
    )
    if cp.returncode != 0:
        raise SystemExit(f"DumpFixtures failed:\n{cp.stderr}\n{cp.stdout}")
    brace = cp.stdout.find("{")
    if brace < 0:
        raise SystemExit(f"DumpFixtures produced no JSON; stdout was:\n{cp.stdout}")
    return json.loads(cp.stdout[brace:])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--update", action="store_true")
    args = parser.parse_args()

    dll = build_fixtures()
    raw = run_dump_against(dll)
    aliases = load_aliases()
    canonical, failures = collect(raw, aliases)
    if failures:
        print("translation failures during golden run:", file=sys.stderr)
        for f in failures[:20]:
            print(f"  - {f}", file=sys.stderr)
        return 1

    GOLDEN.mkdir(exist_ok=True)
    golden = GOLDEN / "fixtures.json"
    emitted_text = json.dumps(canonical, indent=2, sort_keys=False) + "\n"
    if args.update:
        golden.write_text(emitted_text, encoding="utf-8")
        print(f"updated {golden}")
        return 0

    if not golden.exists():
        print(f"no golden at {golden} — run with --update", file=sys.stderr)
        return 1
    expected = golden.read_text(encoding="utf-8")
    if emitted_text != expected:
        print("FAIL: emitted differs from golden", file=sys.stderr)
        import difflib

        for line in difflib.unified_diff(
            expected.splitlines(keepends=True),
            emitted_text.splitlines(keepends=True),
            fromfile="golden/fixtures.json",
            tofile="emitted/fixtures.json",
            n=3,
        ):
            sys.stderr.write(line)
        return 1
    print("OK fixtures.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())
