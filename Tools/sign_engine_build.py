#!/usr/bin/env python3
"""
Signs packaged engine builds and updates a launcher engine manifest.

The launcher fork downloads engine builds listed in this manifest and verifies each zip's Ed25519 signature against
the public key it ships (signing_key_custom). Format (same as the official robust-builds manifest):

    { "<version>": { "insecure": false, "redirect": null,
                     "platforms": { "<rid>": { "url": ..., "sha256": ..., "sig": ... } } } }

Usage: sign_engine_build.py <version> <release dir> <url prefix> <manifest.json>
The private key (PEM, PKCS#8 Ed25519) is read from the ENGINE_SIGNING_KEY environment variable.
"""

import glob
import hashlib
import json
import os
import re
import sys

from cryptography.hazmat.primitives.serialization import load_pem_private_key
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey


def main() -> None:
    if len(sys.argv) != 5:
        print(__doc__)
        sys.exit(1)

    version, release_dir, url_prefix, manifest_path = sys.argv[1:]

    key_pem = os.environ.get("ENGINE_SIGNING_KEY")
    if not key_pem:
        print("ENGINE_SIGNING_KEY is not set.")
        sys.exit(1)

    key = load_pem_private_key(key_pem.encode(), password=None)
    if not isinstance(key, Ed25519PrivateKey):
        print("ENGINE_SIGNING_KEY must be an Ed25519 private key.")
        sys.exit(1)

    platforms = {}
    for path in sorted(glob.glob(os.path.join(release_dir, "Robust.Client_*.zip"))):
        name = os.path.basename(path)
        rid = re.match(r"Robust\.Client_(.+)\.zip", name).group(1)
        with open(path, "rb") as f:
            data = f.read()

        platforms[rid] = {
            "url": f"{url_prefix.rstrip('/')}/{name}",
            "sha256": hashlib.sha256(data).hexdigest().upper(),
            "sig": key.sign(data).hex().upper(),
        }
        print(f"Signed {name}")

    if not platforms:
        print(f"No Robust.Client_*.zip found in {release_dir}")
        sys.exit(1)

    manifest = {}
    if os.path.exists(manifest_path):
        with open(manifest_path) as f:
            manifest = json.load(f)

    manifest[version] = {"insecure": False, "redirect": None, "platforms": platforms}

    with open(manifest_path, "w") as f:
        json.dump(manifest, f, indent=2, sort_keys=True)
        f.write("\n")

    print(f"Manifest updated with {version} ({len(platforms)} platforms).")


if __name__ == "__main__":
    main()
