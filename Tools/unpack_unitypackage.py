#!/usr/bin/env python3
"""Unpacks .unitypackage files straight into a Unity project's Assets folder.

A .unitypackage is a gzipped tar of <guid>/{asset,asset.meta,pathname} folders. Unpacking it here, instead of
through the editor's async importer, is deterministic in batch mode (the importer's completion callback never
fires headless once the External Dependency Manager starts its own update loop).

    unpack_unitypackage.py <project-dir> <package.unitypackage>...
"""
import os
import shutil
import sys
import tarfile
import tempfile


def unpack(project: str, package: str) -> int:
    count = 0
    with tempfile.TemporaryDirectory() as work:
        with tarfile.open(package, "r:gz") as tar:
            members = [m for m in tar.getmembers() if (m.isfile() or m.isdir()) and ".." not in m.name.split("/")
                       and not os.path.isabs(m.name)]
            tar.extractall(work, members=members)
        for guid in sorted(os.listdir(work)):
            folder = os.path.join(work, guid)
            pathname_file = os.path.join(folder, "pathname")
            if not os.path.isfile(pathname_file):
                continue
            with open(pathname_file, encoding="utf-8") as handle:
                relative = handle.readline().strip()
            if not relative.startswith("Assets/") or ".." in relative.split("/"):
                raise ValueError(f"Refusing path outside Assets: {relative!r}")
            target = os.path.join(project, relative)
            asset = os.path.join(folder, "asset")
            if os.path.isfile(asset):
                os.makedirs(os.path.dirname(target), exist_ok=True)
                shutil.copyfile(asset, target)
                count += 1
            else:
                os.makedirs(target, exist_ok=True)
            meta = os.path.join(folder, "asset.meta")
            if os.path.isfile(meta):
                os.makedirs(os.path.dirname(target), exist_ok=True)
                shutil.copyfile(meta, target + ".meta")
    return count


def main() -> None:
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    project = os.path.abspath(sys.argv[1])
    for package in sys.argv[2:]:
        print(f"{os.path.basename(package)}: {unpack(project, package)} files")


if __name__ == "__main__":
    main()
