#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
: "${1:?Usage: bash build.sh /path/to/SHVDNE-references /path/to/framework-references}"
: "${2:?Supply a .NET Framework reference directory containing mscorlib.dll, System.dll, and System.Core.dll}"
mkdir -p package/scripts
mcs -noconfig -nostdlib -target:library -platform:x64 -optimize+ -warnaserror \
  -r:"$2/mscorlib.dll" -r:"$2/System.dll" -r:"$2/System.Core.dll" \
  -r:"$1/ScriptHookVDotNet3.dll" \
  -out:package/scripts/MissionBlipFix.dll BlipGuard.cs MissionBlipFix.cs
python3 ci/package.py
