"""Require exactly the built mod DLL and README in the release package."""
from pathlib import Path
from zipfile import ZipFile

root = Path(__file__).resolve().parents[1]
version = (root / "VERSION").read_text().strip()
archive = root / "package" / f"YAMBF-{version}.zip"
expected = ["README.md", "scripts/YAMBF.dll"]
with ZipFile(archive) as package:
    if sorted(package.namelist()) != expected:
        raise ValueError(f"Unexpected release contents: {package.namelist()}")
    for name in expected:
        if package.read(name) != (root / "package" / name).read_bytes():
            raise ValueError(f"Stale release content: {name}")
print(f"Verified package: {archive.name}")
