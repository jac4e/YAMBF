"""Require a stable vMAJOR.MINOR.PATCH tag matching the packaged VERSION."""
from pathlib import Path
import re
import sys


def validate(tag, version):
    number = r"(?:0|[1-9][0-9]*)"
    if not re.fullmatch(rf"v{number}\.{number}\.{number}", tag):
        raise ValueError("Release tag must use vMAJOR.MINOR.PATCH, for example v1.0.1")
    if tag[1:] != version:
        raise ValueError(f"Tag {tag} does not match VERSION ({version})")
    return version


if __name__ == "__main__":
    version_file = Path(__file__).resolve().parents[1] / "VERSION"
    try:
        print(validate(sys.argv[1], version_file.read_text().strip()))
    except (ValueError, IndexError) as error:
        sys.exit(str(error))
