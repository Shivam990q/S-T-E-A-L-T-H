# ============================================================================
# S-T-E-A-L-T-H v8.0 build script
# Compiles the WPF GUI (S-T-E-A-L-T-H.exe) and the sandbox test runner
# (S-T-E-A-L-T-H_test.exe) using the .NET Framework 4 C# compiler.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\build.ps1          # build
#   powershell -ExecutionPolicy Bypass -File .\build.ps1 -RunTests # build + test
# ============================================================================
param(
    [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

# --- locate compiler --------------------------------------------------------
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'csc.exe not found — install .NET Framework 4.x' }
Write-Host "[1/4] Compiler: $csc"

# --- locate WPF reference assemblies (GAC) ---------------------------------
function Find-GacDll([string]$name) {
    $hits = Get-ChildItem -Path "$env:WINDIR\Microsoft.NET\assembly" -Recurse -Filter "$name.dll" -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    return $hits
}
$presFramework = Find-GacDll 'PresentationFramework'
$presCore      = Find-GacDll 'PresentationCore'
$winBase       = Find-GacDll 'WindowsBase'
$sysXaml       = Join-Path (Split-Path $csc) 'System.Xaml.dll'
foreach ($req in @($presFramework, $presCore, $winBase, $sysXaml)) {
    if (-not $req -or -not (Test-Path $req)) { throw "required WPF assembly missing: $req" }
}
Write-Host "[2/4] WPF assemblies resolved (GAC)"

$refs = @(
    "-r:$presFramework", "-r:$presCore", "-r:$winBase", "-r:$sysXaml",
    '-r:System.dll', '-r:System.Core.dll'
)

# --- compile main app -------------------------------------------------------
Write-Host "[3/4] Compiling S-T-E-A-L-T-H.exe ..."
& $csc -nologo -target:winexe '-out:S-T-E-A-L-T-H.exe' @refs 'S-T-E-A-L-T-H.cs'
if ($LASTEXITCODE -ne 0) { throw 'main compile failed' }

# --- compile test runner (shares source, its own entry point) ---------------
Write-Host "[4/4] Compiling S-T-E-A-L-T-H_test.exe ..."
& $csc -nologo -target:exe -main:STEALTH.TestSuite '-out:S-T-E-A-L-T-H_test.exe' @refs 'S-T-E-A-L-T-H.cs' 'S-T-E-A-L-T-H_test.cs'
if ($LASTEXITCODE -ne 0) { throw 'test compile failed' }

Write-Host ''
Write-Host 'BUILD OK:' (Get-Item "$root\S-T-E-A-L-T-H.exe").Length 'bytes main,' (Get-Item "$root\S-T-E-A-L-T-H_test.exe").Length 'bytes test'

if ($RunTests) {
    Write-Host ''
    Write-Host 'Running sandbox-safe test suite ...'
    & "$root\S-T-E-A-L-T-H_test.exe"
    exit $LASTEXITCODE
}
