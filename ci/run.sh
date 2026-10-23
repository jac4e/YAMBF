#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
: "${1:?Usage: bash ci/run.sh /absolute/path/to/dependency-cache}"
dependency_dir=$(realpath -m "$1")
python3 ci/fetch_dependencies.py "$dependency_dir"

bash build.sh "$dependency_dir/shvdne" "$dependency_dir/framework"
bash tests/run.sh
python3 -m unittest discover -s tests -p 'test_*.py'

# Mono's implementation assemblies expose overloads that GTA's Windows .NET
# Framework cannot load. Audit the built DLL against Microsoft reference DLLs.
cecil_path=${CECIL_PATH:-/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll}
if [ ! -f "$cecil_path" ]; then
    echo 'Mono.Cecil 0.11 not found; install libmono-cecil-private-cil or set CECIL_PATH.' >&2
    exit 1
fi
test_dir=$(mktemp -d /tmp/mission-blip-ci.XXXXXX)
trap 'rm -rf "$test_dir"' EXIT
mcs -warnaserror -r:"$cecil_path" -out:"$test_dir/FrameworkTests.exe" tests/FrameworkTests.cs
mono "$test_dir/FrameworkTests.exe" package/scripts/MissionBlipFix.dll "$dependency_dir/framework"

# Prove the audit rejects the exact unsupported API that broke version 1.0.0.
mono "$test_dir/FrameworkTests.exe" --write-incompatible "$test_dir/Incompatible.dll"
if mono "$test_dir/FrameworkTests.exe" "$test_dir/Incompatible.dll" "$dependency_dir/framework"; then
    echo 'Framework audit failed to reject the incompatible String.Split overload.' >&2
    exit 1
fi

python3 ci/verify_package.py
