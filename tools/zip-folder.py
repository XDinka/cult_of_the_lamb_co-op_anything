"""Zips a folder's files with paths relative to it (forward slashes), for the installer payload.

Usage: python -I tools/zip-folder.py <folder> <out.zip>
"""
import os
import sys
import zipfile


def main(folder, out):
    count = 0
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for root, _, files in os.walk(folder):
            for name in sorted(files):
                path = os.path.join(root, name)
                archive.write(path, os.path.relpath(path, folder).replace(os.sep, "/"))
                count += 1
    print(f"{count} files into {out}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
