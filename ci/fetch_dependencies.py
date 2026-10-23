"""Fetch hash-pinned build references; never package the runtime or GTA assets."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from zipfile import ZipFile


def main():
    destination = Path(sys.argv[1]).resolve()
    destination.mkdir(parents=True, exist_ok=True)
    manifest = json.loads(Path(__file__).with_name("dependencies.json").read_text())
    for name, dependency in manifest.items():
        archive = destination / (name + ".zip")
        if not archive.exists():
            subprocess.run([
                "curl", "--fail", "--location", "--retry", "3",
                "--connect-timeout", "20", "--max-time", "120",
                dependency["url"], "--output", str(archive),
            ], check=True)
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        if digest != dependency["sha256"]:
            raise ValueError(f"SHA-256 mismatch for {name}: {digest}")
        with ZipFile(archive) as package:
            for member, relative_path in dependency["files"].items():
                output = destination / relative_path
                output.parent.mkdir(parents=True, exist_ok=True)
                output.write_bytes(package.read(member))
        print(f"Verified and extracted {name}", flush=True)


if __name__ == "__main__":
    main()
