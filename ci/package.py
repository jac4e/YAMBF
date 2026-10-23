"""Package only the mod DLL and README, using portable ZIP member paths."""
from pathlib import Path
from shutil import copyfile
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parents[1]
base = root / "package"
copyfile(root / "README.md", base / "README.md")
version = (root / "VERSION").read_text().strip()
archive_path = base / f"MissionBlipFix-{version}.zip"
with ZipFile(archive_path, "w", ZIP_DEFLATED) as archive:
    for name in ["scripts/MissionBlipFix.dll", "README.md"]:
        archive.write(base / name, name)
print(f"Created {archive_path}")
