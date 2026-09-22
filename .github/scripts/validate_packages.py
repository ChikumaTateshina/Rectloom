#!/usr/bin/env python3
"""Structural checks for the Rectloom package layout.

Guards the constraints that must never regress silently:

* every package.json and .asmdef is valid JSON;
* a package's ``name`` matches its folder name;
* declared versions agree across packages;
* compiler assemblies stay Editor-only;
* the core package never depends on VRChat, VRCUISharp or an external UI library;
* the test project's ``file:`` package references resolve.

Run from the repository root::

    python .github/scripts/validate_packages.py
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
PACKAGES_DIR = REPO_ROOT / "Packages"
TEST_PROJECT_MANIFEST = REPO_ROOT / "TestProject" / "Packages" / "manifest.json"

REQUIRED_PACKAGE_FIELDS = ("name", "displayName", "version", "unity", "description", "license")
SEMVER = re.compile(r"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$")

# Substrings that must never appear in the core package's dependencies or assembly references.
FORBIDDEN_IN_CORE = ("vrchat", "vrc.", "vrcuisharp", "udon")

errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def load_json(path: Path) -> dict | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        fail(f"{path.relative_to(REPO_ROOT)}: invalid JSON ({exc})")
    except OSError as exc:
        fail(f"{path.relative_to(REPO_ROOT)}: cannot read ({exc})")
    return None


def check_package_manifest(package_dir: Path) -> tuple[str, str] | None:
    manifest_path = package_dir / "package.json"
    if not manifest_path.is_file():
        fail(f"{package_dir.relative_to(REPO_ROOT)}: package.json is missing")
        return None

    manifest = load_json(manifest_path)
    if manifest is None:
        return None

    rel = manifest_path.relative_to(REPO_ROOT)

    for field in REQUIRED_PACKAGE_FIELDS:
        if not manifest.get(field):
            fail(f"{rel}: required field '{field}' is missing or empty")

    name = manifest.get("name", "")
    if name != package_dir.name:
        fail(f"{rel}: name '{name}' does not match folder '{package_dir.name}'")

    version = manifest.get("version", "")
    if version and not SEMVER.match(version):
        fail(f"{rel}: version '{version}' is not semver")

    if not str(manifest.get("unity", "")).startswith("2022.3"):
        fail(f"{rel}: unity must target 2022.3, found '{manifest.get('unity')}'")

    return name, version


def check_dependency_versions(versions: dict[str, str]) -> None:
    for package_dir in sorted(PACKAGES_DIR.iterdir()):
        manifest_path = package_dir / "package.json"
        if not manifest_path.is_file():
            continue

        manifest = load_json(manifest_path)
        if manifest is None:
            continue

        rel = manifest_path.relative_to(REPO_ROOT)
        for dependency, required in (manifest.get("dependencies") or {}).items():
            if dependency in versions and versions[dependency] != required:
                fail(
                    f"{rel}: depends on {dependency} {required}, "
                    f"but that package is version {versions[dependency]}"
                )


def check_core_independence(core_dir: Path) -> None:
    manifest = load_json(core_dir / "package.json")
    if manifest is not None:
        dependencies = list((manifest.get("dependencies") or {}).keys())
        dependencies += list((manifest.get("vpmDependencies") or {}).keys())
        for dependency in dependencies:
            lowered = dependency.lower()
            if any(token in lowered for token in FORBIDDEN_IN_CORE):
                fail(f"core package must not depend on '{dependency}'")

    for asmdef_path in sorted(core_dir.rglob("*.asmdef")):
        if "Tests" in asmdef_path.parts:
            continue

        asmdef = load_json(asmdef_path)
        if asmdef is None:
            continue

        for reference in asmdef.get("references") or []:
            lowered = reference.lower()
            if any(token in lowered for token in FORBIDDEN_IN_CORE):
                fail(
                    f"{asmdef_path.relative_to(REPO_ROOT)}: "
                    f"core assembly must not reference '{reference}'"
                )


def check_asmdefs() -> None:
    asmdef_paths = sorted(PACKAGES_DIR.rglob("*.asmdef"))
    if not asmdef_paths:
        fail("no .asmdef files found under Packages/")

    seen: dict[str, Path] = {}

    for asmdef_path in asmdef_paths:
        asmdef = load_json(asmdef_path)
        if asmdef is None:
            continue

        rel = asmdef_path.relative_to(REPO_ROOT)
        name = asmdef.get("name")

        if not name:
            fail(f"{rel}: assembly definition has no name")
            continue

        if name != asmdef_path.stem:
            fail(f"{rel}: assembly name '{name}' does not match file name")

        if name in seen:
            fail(f"{rel}: assembly name '{name}' already used by {seen[name]}")
        seen[name] = rel

        if asmdef.get("includePlatforms") != ["Editor"]:
            fail(f"{rel}: compiler assemblies must set includePlatforms to [\"Editor\"]")


def check_test_project(versions: dict[str, str]) -> None:
    manifest = load_json(TEST_PROJECT_MANIFEST)
    if manifest is None:
        return

    rel = TEST_PROJECT_MANIFEST.relative_to(REPO_ROOT)
    dependencies = manifest.get("dependencies") or {}

    for package_name in versions:
        reference = dependencies.get(package_name)
        if reference is None:
            fail(f"{rel}: does not reference {package_name}")
            continue

        if not reference.startswith("file:"):
            fail(f"{rel}: {package_name} must use a local 'file:' reference, found '{reference}'")
            continue

        target = (TEST_PROJECT_MANIFEST.parent / reference[len("file:"):]).resolve()
        if not target.is_dir():
            fail(f"{rel}: {package_name} points at missing folder '{reference}'")


def main() -> int:
    if not PACKAGES_DIR.is_dir():
        print("Packages/ not found", file=sys.stderr)
        return 1

    versions: dict[str, str] = {}

    for package_dir in sorted(PACKAGES_DIR.iterdir()):
        if not package_dir.is_dir():
            continue

        result = check_package_manifest(package_dir)
        if result is not None:
            name, version = result
            versions[name] = version

    check_dependency_versions(versions)
    check_asmdefs()

    core_dir = PACKAGES_DIR / "com.chikumatateshina.rectloom.core"
    if core_dir.is_dir():
        check_core_independence(core_dir)
    else:
        fail("core package folder is missing")

    check_test_project(versions)

    if errors:
        print(f"Package validation failed with {len(errors)} problem(s):\n", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print(f"Package validation passed: {len(versions)} package(s) checked.")
    for name in sorted(versions):
        print(f"  {name} {versions[name]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
