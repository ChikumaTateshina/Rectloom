#!/usr/bin/env python3
"""Build the VPM repository listing and the release zip for Rectloom.

The VRChat Creator Companion subscribes to a single ``index.json`` that lists every
version of every package and where to download it. This script produces that file,
merging in whatever the currently published listing already contains so that older
versions stay installable.

Rectloom is three packages in this repository -- core, the uGUI backend and the
VRChat adapter -- because the core must stay usable without VRChat and the
assemblies depend in one direction only. VCC, though, shows every package a
listing names, and three entries where two are internal is a list a user has to
decode. So VPM gets one bundle package containing all three assemblies, while the
repository keeps the split for UPM users who want the core without the adapter.

The bundle names the three split packages in ``legacyPackages``, so a project that
installed them separately has them removed when the bundle arrives.

The split packages are dropped from the listing only once the bundle has a version in
it. Dropping them earlier would leave a listing with nothing installable: the three
published versions gone and no bundle version yet to replace them. Since the listing is
rebuilt on every push to the default branch, that window would be every push between
the bundle being written and its first release.

Run from the repository root::

    python .github/scripts/build_vpm_listing.py --out Website --version 0.1.1

Options:

``--repo``     ``owner/name`` on GitHub. Defaults to ``$GITHUB_REPOSITORY``.
``--version``  Version to publish. Defaults to the core package's own version.
``--existing`` A previously published ``index.json`` to merge, or a URL.
``--out``      Folder to write ``index.json`` and the zip into.
``--no-zip``   Write only the listing, which is all a dry run needs.
``--empty``    Write a listing with no package versions at all.
``--verify``   Only list a version whose zip can actually be downloaded.

``--empty`` exists because the URL has to be a valid listing from the moment the
page goes live. A VPM client that fetches a 404 reports the repository as invalid,
which reads as a broken link rather than "nothing released yet".

``--verify`` is what lets the listing be built from the default branch rather than
from a tag. A version whose release has not been published yet is simply left out,
so the listing can never advertise a download that answers 404. A check that
cannot be made at all is an error, because quietly dropping a version would
unpublish it for everyone subscribed.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
import zipfile
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
PACKAGES_DIR = REPO_ROOT / "Packages"

LISTING_NAME = "Rectloom"
LISTING_ID = "com.chikumatateshina.rectloom"
LISTING_AUTHOR = "ChikumaTateshina"
LISTING_DESCRIPTION = "HTML/CSS to Unity UI compiler for VRChat worlds."

BUNDLE_ID = "com.chikumatateshina.rectloom"
BUNDLE_DISPLAY_NAME = "Rectloom"
BUNDLE_DESCRIPTION = (
    "Compiles HTML and CSS into Unity uGUI objects in the Editor. Includes the "
    "compiler, the uGUI backend and the VRChat adapter. Editor only: nothing is "
    "added to a player or world build."
)

# Folder each package's contents takes inside the bundle. The split packages keep
# their own identity in the repository; these are just readable names in a project.
BUNDLE_LAYOUT = (
    ("com.chikumatateshina.rectloom.core", "Core"),
    ("com.chikumatateshina.rectloom.ugui", "uGUI"),
    ("com.chikumatateshina.rectloom.vrchat", "VRChat"),
)

# Published separately in 0.1.0, before the bundle existed. Dropped from the listing
# so VCC shows one entry, and named in the bundle's legacyPackages so a project that
# already has them gets them replaced rather than duplicated.
RETIRED_PACKAGES = tuple(name for name, _ in BUNDLE_LAYOUT)

# The VRChat adapter compiles with or without the SDK, through assembly version defines, so the
# bundle does not require it. Carrying the requirement over from the split package would stop the
# bundle installing in an avatar project, which would be a worse list to decode than three entries.
OPTIONAL_VPM_DEPENDENCIES = frozenset({"com.vrchat.worlds", "com.vrchat.base", "com.vrchat.avatars"})

# Never shipped inside the bundle: Unity's own leftovers, our scratch output, the
# split packages' manifests (a package has one, at its root) and the tests, which a
# consumer cannot run and would only pay to download.
EXCLUDED_NAMES = {"Library", "Temp", "obj", "Logs", ".git", ".vs", ".idea", "Tests"}
EXCLUDED_FILES = {"package.json", "package.json.meta"}

# The earliest timestamp a zip entry can carry, used only when the commit time is unknown.
ZIP_EPOCH = (1980, 1, 1, 0, 0, 0)


def zip_timestamp() -> tuple[int, int, int, int, int, int]:
    """The time every entry of the archive is stamped with: that of the commit being built.

    It has to differ between releases. Unity decides whether a script changed by its
    modification time, and VCC restores the times stored in the zip, so an archive whose
    entries all say 1980 installs over the previous version without Unity recompiling
    anything: the new source sits on disk while the old assemblies keep running. The commit
    time changes with every release and is still the same for the same commit, so the
    archive stays reproducible.
    """
    try:
        seconds = int(
            subprocess.run(
                ["git", "log", "-1", "--format=%ct"],
                cwd=REPO_ROOT,
                check=True,
                capture_output=True,
                text=True,
            ).stdout.strip()
        )
    except (OSError, ValueError, subprocess.CalledProcessError):
        return ZIP_EPOCH

    moment = datetime.fromtimestamp(seconds, tz=timezone.utc)
    stamp = (moment.year, moment.month, moment.day, moment.hour, moment.minute, moment.second)
    return max(stamp, ZIP_EPOCH)


def read_manifest(package_dir: Path) -> dict:
    return json.loads((package_dir / "package.json").read_text(encoding="utf-8"))


def build_bundle_manifest(version: str) -> dict:
    """Derive the bundle's manifest from the three split packages.

    Generated rather than kept as a fourth file, so there is nothing to drift out of
    step with the packages it is built from.
    """
    manifests = {}

    for name, _ in BUNDLE_LAYOUT:
        package_dir = PACKAGES_DIR / name

        if not package_dir.is_dir():
            print(f"error: {name} is missing", file=sys.stderr)
            raise SystemExit(1)

        manifests[name] = read_manifest(package_dir)

    core = manifests["com.chikumatateshina.rectloom.core"]
    dependencies: dict[str, str] = {}
    vpm_dependencies: dict[str, str] = {}
    samples: list[dict] = []
    keywords: list[str] = []

    for name, folder in BUNDLE_LAYOUT:
        manifest = manifests[name]

        # Dependencies between our own packages disappear: inside the bundle they are
        # all present already.
        for key, value in (manifest.get("dependencies") or {}).items():
            if not key.startswith(BUNDLE_ID):
                dependencies[key] = value

        for key, value in (manifest.get("vpmDependencies") or {}).items():
            if key.startswith(BUNDLE_ID) or key in OPTIONAL_VPM_DEPENDENCIES:
                continue

            vpm_dependencies[key] = value

        for sample in manifest.get("samples") or []:
            moved = dict(sample)
            moved["path"] = f"{folder}/{sample['path']}"
            samples.append(moved)

        for keyword in manifest.get("keywords") or []:
            if keyword not in keywords:
                keywords.append(keyword)

    bundle = {
        "name": BUNDLE_ID,
        "displayName": BUNDLE_DISPLAY_NAME,
        "version": version,
        "unity": core["unity"],
        "description": BUNDLE_DESCRIPTION,
        "keywords": keywords,
        "author": core["author"],
        "license": core.get("license", "MIT"),
        "documentationUrl": core.get("documentationUrl", ""),
        "changelogUrl": core.get("changelogUrl", ""),
        "licensesUrl": core.get("licensesUrl", ""),
        "hideInEditor": False,
        "legacyPackages": list(RETIRED_PACKAGES),
    }

    if dependencies:
        bundle["dependencies"] = dependencies

    if vpm_dependencies:
        bundle["vpmDependencies"] = vpm_dependencies

    if samples:
        bundle["samples"] = samples

    return bundle


def zip_bundle(destination: Path, manifest: dict) -> tuple[int, str]:
    """Zip the three packages into one archive with the bundle manifest at its root.

    Entries are sorted and stamped with the commit's time, so the same commit always
    produces the same bytes. Without that, re-running a release would change the
    archive's hash while its contents stayed identical.
    """
    destination.parent.mkdir(parents=True, exist_ok=True)
    entries: list[tuple[str, bytes]] = [
        ("package.json", (json.dumps(manifest, indent=2) + "\n").encode("utf-8")),
        ("README.md", (REPO_ROOT / "README.md").read_bytes()),
        ("LICENSE", (REPO_ROOT / "LICENSE").read_bytes()),
    ]

    for name, folder in BUNDLE_LAYOUT:
        package_dir = PACKAGES_DIR / name

        for path in sorted(package_dir.rglob("*")):
            relative = path.relative_to(package_dir)

            if any(part in EXCLUDED_NAMES for part in relative.parts):
                continue

            if path.is_dir() or relative.name in EXCLUDED_FILES:
                continue

            entries.append((f"{folder}/{relative.as_posix()}", path.read_bytes()))

    timestamp = zip_timestamp()

    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as archive:
        for name, data in sorted(entries):
            info = zipfile.ZipInfo(name, timestamp)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            archive.writestr(info, data)

    data = destination.read_bytes()
    return len(data), hashlib.sha256(data).hexdigest()


def fetch_release_digest(url: str) -> str | None:
    """Hash a published release asset, or answer None when it is not published.

    The hash has to come from the bytes users will actually download, not from a
    rebuilt copy, so that a corrupted or substituted asset is detected rather than
    matching a hash computed from something else.
    """
    try:
        with urllib.request.urlopen(url, timeout=120) as response:
            digest = hashlib.sha256()

            while True:
                chunk = response.read(1 << 16)

                if not chunk:
                    break

                digest.update(chunk)

            return digest.hexdigest()
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None

        print(f"error: {url} answered HTTP {error.code}", file=sys.stderr)
        raise SystemExit(1) from error
    except (urllib.error.URLError, OSError) as error:
        print(f"error: could not fetch {url} ({error})", file=sys.stderr)
        raise SystemExit(1) from error


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


def write_listing(out_dir: Path, listing: dict) -> None:
    (out_dir / "index.json").write_text(
        json.dumps(listing, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )

    index_page = REPO_ROOT / "Website" / "index.html"
    if index_page.is_file() and out_dir != index_page.parent:
        shutil.copy2(index_page, out_dir / "index.html")


def drop_retired(packages: dict) -> None:
    """Remove the split packages the bundle supersedes.

    Only called once the bundle actually has a listed version. Dropping them before
    that would leave a listing with nothing installable in it: the three published
    versions gone and no bundle version yet to replace them. A subscriber would see
    the repository as having no packages, which is worse than seeing the old three.
    """
    for name in RETIRED_PACKAGES:
        if packages.pop(name, None) is not None:
            print(f"  dropped {name}: superseded by the {BUNDLE_ID} bundle")


def has_bundle_version(packages: dict) -> bool:
    return bool((packages.get(BUNDLE_ID) or {}).get("versions"))


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

    # GitHub Pages serves the owner part in lower case, and VCC stores this URL
    # verbatim, so it has to match what the documentation tells people to paste.
    owner, _, name = repo.partition("/")
    listing["url"] = f"https://{owner.lower()}.github.io/{name}/index.json"
    packages = listing.setdefault("packages", {})

    if args.empty:
        # An empty listing is for the moment before anything has been released at all, so
        # there is nothing published to supersede and nothing to keep installable.
        packages.clear()
        packages.setdefault(BUNDLE_ID, {}).setdefault("versions", {})
        write_listing(out_dir, listing)
        print(f"Wrote an empty listing to {out_dir / 'index.json'}")
        return 0

    version = args.version or read_manifest(PACKAGES_DIR / BUNDLE_LAYOUT[0][0])["version"]
    manifest = build_bundle_manifest(version)

    zip_name = f"{BUNDLE_ID}-{version}.zip"
    manifest["url"] = f"https://github.com/{repo}/releases/download/v{version}/{zip_name}"

    if not args.no_zip:
        size, digest = zip_bundle(out_dir / zip_name, manifest)
        manifest["zipSHA256"] = digest
        print(f"  {zip_name}  {size / 1024:.0f} KiB  sha256:{digest[:12]}")

    versions = packages.setdefault(BUNDLE_ID, {}).setdefault("versions", {})

    if args.verify:
        digest = fetch_release_digest(manifest["url"])

        if digest is None:
            # Not released yet. Whatever is already listed stays listed, including the split
            # packages: until a bundle version exists they are the only way to install Rectloom.
            print(f"  skipped {BUNDLE_ID} {version}: {zip_name} is not published yet")

            if has_bundle_version(packages):
                drop_retired(packages)
            else:
                print("  kept the split packages: no bundle version is published yet")

            write_listing(out_dir, listing)
            print(f"Wrote {out_dir / 'index.json'}")
            return 0

        manifest["zipSHA256"] = digest

    if version in versions and not args.overwrite:
        print(
            f"error: {BUNDLE_ID} {version} is already published; bump the version or pass --overwrite",
            file=sys.stderr,
        )
        return 1

    versions[version] = manifest

    # The bundle now has a version, so the packages it supersedes can go.
    drop_retired(packages)

    write_listing(out_dir, listing)
    print(f"Wrote {out_dir / 'index.json'}")
    print(f"  published {BUNDLE_ID} {version}")

    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", help="owner/name on GitHub")
    parser.add_argument("--version", help="version to publish")
    parser.add_argument("--existing", help="previously published index.json, as a path or URL")
    parser.add_argument("--out", default="Website", help="output folder")
    parser.add_argument("--no-zip", action="store_true", help="write only the listing")
    parser.add_argument(
        "--empty",
        action="store_true",
        help="write a listing with no package versions",
    )
    parser.add_argument(
        "--verify",
        action="store_true",
        help="only list a version whose zip can actually be downloaded",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="replace a version that is already listed",
    )

    return build(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
