"""Regression test: the surface enumerator must ABORT on an unreadable/unparsable
source file, never silently drop it.

Before this test, ``_wired_base_surface`` caught every exception from
``scan_class_bases`` / ``parse_cs_file`` and ``continue``d. A file it could not
read or parse was therefore dropped from the base-member map, so the generated
class's INHERITED surface members vanished from ``port_surface_native.json`` and
SURFACE-DIFF reported them as OMITTED BY THE PORT — a false omission produced
with no diagnostic at all.

Run: python3 tests/scripts/test_enumerate_surface_fail_loud.py
"""

import pathlib
import sys

PORT_ROOT = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(PORT_ROOT / "scripts"))

import enumerate_surface as es  # noqa: E402

GEN = PORT_ROOT / "src" / "SignalWire" / "REST" / "Namespaces" / "Generated"


def main() -> int:
    real = sorted(GEN.rglob("*.cs"))
    if not real:
        print(f"FAIL: no generated .cs under {GEN}")
        return 1

    manifest = es.load_rest_manifest()

    # A clean input must still work.
    es._wired_base_surface(real, manifest)

    # An unreadable file in the list must abort, not be skipped.
    missing = GEN / "__does_not_exist__.cs"
    try:
        es._wired_base_surface([*real, missing], manifest)
    except SystemExit as exc:
        if "cannot read" not in str(exc):
            print(f"FAIL: aborted, but not with the expected message: {exc}")
            return 1
        print("PASS: an unreadable source file aborts the enumerator")
        return 0

    print(
        "FAIL: an unreadable source file was silently skipped "
        "(this manufactures a false SURFACE-DIFF omission)"
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
