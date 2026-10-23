param(
    [Parameter(Mandatory = $true)]
    [string]$RuntimeDirectory,
    [Parameter(Mandatory = $true)]
    [string]$FrameworkDirectory
)

$ErrorActionPreference = 'Stop'
$runtime = (Resolve-Path -LiteralPath $RuntimeDirectory).Path
$framework = (Resolve-Path -LiteralPath $FrameworkDirectory).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Install .NET Framework 4.8 to provide the Windows C# compiler.'
}
foreach ($reference in @(
    (Join-Path $runtime 'ScriptHookVDotNet3.dll'),
    (Join-Path $framework 'mscorlib.dll'),
    (Join-Path $framework 'System.dll'),
    (Join-Path $framework 'System.Core.dll')
)) {
    if (-not (Test-Path -LiteralPath $reference)) {
        throw "Missing build reference: $reference"
    }
}

Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Force -Path 'package/scripts' | Out-Null
    & $compiler -nologo -noconfig -nostdlib -target:library -platform:x64 -optimize+ -warnaserror `
        "-r:$framework/mscorlib.dll" "-r:$framework/System.dll" "-r:$framework/System.Core.dll" `
        "-r:$runtime/ScriptHookVDotNet3.dll" `
        '-out:package/scripts/MissionBlipFix.dll' BlipGuard.cs MissionBlipFix.cs
    if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
    python ci/package.py
    if ($LASTEXITCODE -ne 0) { throw 'Release packaging failed.' }
} finally {
    Pop-Location
}
