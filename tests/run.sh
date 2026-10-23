#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
test_dir=$(mktemp -d /tmp/yambf-tests.XXXXXX)
trap 'rm -rf "$test_dir"' EXIT
mcs -warnaserror -out:"$test_dir/GuardTests.exe" BlipGuard.cs tests/GuardTests.cs
mono "$test_dir/GuardTests.exe" "$@"
