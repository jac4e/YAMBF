# Mission Blip Fix 1.0.1 — GTA V Enhanced

Repairs a specific cause of Story Mode mission icons failing to return after character switches or death: the vanilla `blip_controller` waits in a loop while 150 or more blips exist. Mods can keep the count above that threshold indefinitely. Hiding icons does not release their handles or lower that count.

This mod changes only that wait loop's comparison in the loaded script. Rockstar's controller continues to decide which missions, characters, locations, and icons are available. It does not restart mission scripts, write save data, create replacement markers, clear mission flags, or delete other mods' icons. It does not increase the engine's actual blip allocation limit; a completely full engine pool remains a separate problem.

## Installation

Requirements:

- GTA V **Enhanced**, in Story Mode. Legacy compatibility is not guaranteed.
- [Script Hook V](https://www.dev-c.com/gtav/scripthookv/) and an ASI loader compatible with your game version.
- [ScriptHookVDotNet Enhanced (SHVDNE)](https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced), version **1.1.0.6** or compatible.

Instructions:

1. Download `MissionBlipFix-<version>.zip` from this repository's **Releases** page. The GitHub source archives do not contain the compiled mod.
2. Locate your GTA V Enhanced game folder (the folder containing the game executable).
3. Extract the release ZIP into that folder, preserving the `scripts` subfolder. Create `scripts` if it does not exist, or merge it with your existing folder. The resulting path must be `<game folder>/scripts/MissionBlipFix.dll`.
4. Start Story Mode. Check `scripts/MissionBlipFix.log` for `Patched blip_controller 150-icon wait gate` once the controller is running.

The release ZIP contains only this mod and its README; install the requirements separately. If you run the game through a compatibility layer, configure Script Hook V and SHVDNE according to their instructions before installing this mod.

To update, close the game and replace `scripts/MissionBlipFix.dll`. To uninstall, close the game and remove that DLL; you can also remove `scripts/MissionBlipFix.log`.

## Troubleshooting and limitations

`scripts/MissionBlipFix.log` records successful application, active blip counts, character model, mission/switch state, and errors. If no log is created, check that SHVDNE is installed and loading scripts, and confirm the DLL's location.

A changed or unsupported script signature disables the mod and logs the reason instead of guessing a memory location. If icons remain missing despite the success message, inspect the log's counts; mission suppression by another mod, unavailable missions, or a full engine pool require separate diagnosis.

## Build and test

Both builds produce an x64 .NET Framework DLL and `package/MissionBlipFix-<version>.zip`. A game installation is not required to build. Run the commands below from the repository root.

The dependency downloader fetches the SHVDNE 1.1.0.6 API DLL and Microsoft's .NET Framework 4.8 reference assemblies, checks their SHA-256 hashes against `ci/dependencies.json`, and extracts them into `.ci-deps/`. Python 3 and `curl` must be on your PATH; on Windows, use `curl.exe` supplied with Windows or install curl separately.

### Windows (PowerShell)

Install **.NET Framework 4.8** (which provides `csc.exe`) and **Python 3** with the `python` command on PATH. Windows PowerShell 5.1 or later is supported.

```powershell
python ci/fetch_dependencies.py .ci-deps
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 .ci-deps\shvdne .ci-deps\framework
python ci/verify_package.py
```

The build uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. The execution-policy option applies only to this PowerShell process.

### Linux (Bash)

Install **Mono** (`mcs` and `mono`), **Python 3**, and **curl**. For example, on Debian/Ubuntu:

```bash
sudo apt-get update
sudo apt-get install mono-devel python3 curl
python3 ci/fetch_dependencies.py .ci-deps
bash build.sh .ci-deps/shvdne .ci-deps/framework
python3 ci/verify_package.py
```

### Using existing build references

Both build scripts accept two directories: the first contains `ScriptHookVDotNet3.dll` from SHVDNE, and the second contains `mscorlib.dll`, `System.dll`, and `System.Core.dll` from .NET Framework 4.8. Pass those directories in place of `.ci-deps/shvdne` and `.ci-deps/framework` to build without downloading dependencies. Paths containing spaces must be quoted.

Explicit framework references prevent Mono-only API overloads from entering the DLL. Do not substitute Mono's framework assemblies. Build references and runtime dependencies are excluded from the release ZIP and must not be copied into the mod's `scripts` folder.

### Tests and CI

On Linux, run the guard tests and release-tag validation tests:

```bash
bash tests/run.sh
python3 -m unittest discover -s tests -p 'test_*.py'
# Optional: verify against an extracted, decompressed blip_controller.ysc resource:
bash tests/run.sh /path/to/blip_controller.ysc
```

For the complete CI checks, also install `libmono-cecil-private-cil` and run:

```bash
bash ci/run.sh .ci-deps
```

This downloads the pinned references, builds the mod, runs the tests, audits .NET Framework API compatibility (including rejection of a known incompatible overload), and verifies ZIP contents. Set `CECIL_PATH` to your Mono.Cecil 0.11 DLL if it is installed somewhere other than the default Debian/Ubuntu path.

GitHub Actions runs the full checks on Linux and builds and verifies the package on Windows for pushes and pull requests. Building on Linux produces the same type of Windows .NET Framework mod; it does not provide a native Linux game runtime.

## Technical

The 21-byte signature identifies a zero-argument native call returning one value, comparison, conditional branch, `WAIT(2000)`, and a jump back to the count query. Native table indices are wildcarded; the remaining instructions and branch displacements are fixed. Only `PUSH_U8 150; ILT` becomes `PUSH_U8 0; IGE`. Both consume two integers and return one boolean, preserving code length, stack balance, native calls, and jump targets. A nonnegative blip count therefore exits the wait.

- Controller source reference: <https://github.com/calamity-inc/GTA-V-Decompiled-Scripts/blob/senpai/decompiled_scripts/blip_controller.c>
- Opcode definitions: <https://github.com/Parik27/V.Rainbomizer/blob/master/lib/scrThread.hh>
- SHVDNE runtime API: `SHVDN.NativeMemory.FindPatternInScript`, resolved at runtime; builds use the pinned SHVDNE release in `ci/dependencies.json`.
