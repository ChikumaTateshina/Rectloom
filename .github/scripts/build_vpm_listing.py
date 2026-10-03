#!/usr/bin/env python3
"""Build the VPM repository listing and the release zips for Rectloom.

The VRChat Creator Companion subscribes to a single ``index.json`` that lists every
version of every package and where to download it. This script produces that file
from the packages in ``Packages/``, merging in whatever the currently published
listing already contains so that older versions stay installable.

Run from the repository root::

    python .github/scripts/build_vpm_listing.py --out Website --version 0.1.0

Options:

``--repo``     ``owner/name`` on GitHub. Defaults to ``$GITHUB_REPOSITORY``.
``--version``  Version to publish. Defaults to each package's own version.
``--existing`` A previously published ``index.json`` to merge, or a URL.
``--out``      Folder to write ``index.json`` and the zips into.
``--no-zip``   Write only the listing, which is all a dry run needs.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
PACKAGES_DIR = REPO_ROOT / "Packages"

LISTING_NAME = "Rectloom"
LISTING_ID = "com.chikumatateshina.rectloom"
LISTING_AUTHOR = "ChikumaTateshina"
LISTING_DESCRIPTION = "HTML/CSS to Unity UI compiler for VRChat worlds."

# Only the VRChat-facing packages belong in a VPM listing. The core and the uGUI
# backend arrive as dependencies of the VRChat package, which is what lets a user
# subscribe to one entry and get everything.
VPM_PACKAGES = (
    "com.chikumatateshina.rectloom.core",
    "com.chikumatateshina.rectloom.ugui",
    "com.chikumatateshina.rectloom.vrchat",
)

# Never shipped inside a package zip: Unity's own leftovers and our scratch output.
EXCLUDED_NAMES = {"Library", "Temp", "obj", "Logs", ".git", ".vs", ".idea"}


def read_manifest(package_dir: Path) -> dict:
    return json.loads((package_dir / "package.json").read_text(encoding="utf-8"))


def zip_package(package_dir: Path, destination: Path) -> tuple[int, str]:
    """Zip a package so that package.json sits at the archive root, as VPM requires."""
    destination.parent.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(package_dir.rglob("*")):
            if any(part in EXCLUDED_NAMES for part in path.relative_to(package_dir).parts):
                continue
            if path.is_dir():
                continue
            archive.write(path, path.relative_to(package_dir).as_posix())

    data = destination.read_bytes()
    return len(data), hashlib.sha256(data).hexdigest()


def load_existing(source: str | None) -> dict:
    if not source:
        return {}

    try:
        if source.startswith("http://") or source.startswith("https://"):
            with urllib.request.urlopen(source, timeout=30) as response:
                return json.loads(response.read().decode("utf-8"))

        path = Path(source)
        if path.is_file():
            return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, urllib.error.URLError, json.JSONDecodeError) as error:
        # A listing that cannot be read must not silently become an empty one: that
        # would unpublish every older version for everyone subscribed.
        print(f"error: could not read the existing listing from {source}: {error}", file=sys.stderr)
        raise SystemExit(1)

    return {}


def build(args: argparse.Namespace) -> int:
    if not PACKAGES_DIR.is_dir():
        print("error: Packages/ not found; run from the repository root", file=sys.stderr)
        return 1

    repo = args.repo or os.environ.get("GITHUB_REPOSITORY", "")
    if not repo:
        print("error: --repo or GITHUB_REPOSITORY is required", file=sys.stderr)
        return 1

    out_dir = REPO_ROOT / args.out
    out_dir.mkdir(parents=True, exist_ok=True)

    listing = load_existing(args.existing)
    listing.setdefault("name", LISTING_NAME)
    listing.setdefault("id", LISTING_ID)
    listing.setdefault("author", LISTING_AUTHOR)
    listing.setdefault("description", LISTING_DESCRIPTION)
    listing["url"] = f"https://{repo.split('/')[0]}.github.io/{repo.split('/')[1]}/index.json"
    packages = listing.setdefault("packages", {})

    published: list[str] = []

    for name in VPM_PACKAGES:
        package_dir = PACKAGES_DIR / name

        if not package_dir.is_dir():
            print(f"error: {name} is missing", file=sys.stderr)
            return 1

        manifest = read_manifest(package_dir)
        version = args.version or manifest["version"]
        manifest["version"] = version

        zip_name = f"{name}-{version}.zip"
        manifest["url"] = f"https://github.com/{repo}/releases/download/v{version}/{zip_name}"

        if not args.no_zip:
            size, digest = zip_package(package_dir, out_dir / zip_name)
            manifest["zipSHA256"] = digest
            print(f"  {zip_name}  {size / 1024:.0f} KiB  sha256:{digest[:12]}")

        versions = packages.setdefault(name, {}).setdefault("versions", {})

        if version in versions and not args.overwrite:
            print(
                f"error: {name} {version} is already published; bump the version or pass --overwrite",
                file=sys.stderr,
            )
            return 1

        versions[version] = manifest
        published.append(f"{name} {version}")

    (out_dir / "index.json").write_text(
        json.dumps(listing, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )

    index_page = REPO_ROOT / "Website" / "index.html"
    if index_page.is_file() and out_dir != index_page.parent:
        shutil.copy2(index_page, out_dir / "index.html")

    print(f"Wrote {out_dir / 'index.json'}")
    for entry in published:
        print(f"  published {entry}")

    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", help="owner/name on GitHub")
    parser.add_argument("--version", help="version to publish")
    parser.add_argument("--existing", help="previously published index.json, as a path or URL")
    parser.add_argument("--out", default="Website", help="output folder")
    parser.add_argument("--no-zip", action="store_true", help="write only the listing")
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="replace a version that is already listed",
    )

    return build(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
