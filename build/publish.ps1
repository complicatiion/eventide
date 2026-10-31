<#
.SYNOPSIS
    Builds the Eventide release: eventide_v<version>_portable.zip (+ .sha256) and, if Inno Setup 6 is installed,
    eventide_v<version>_setup.exe (+ .sha256).

.DESCRIPTION
    1. Runs the tests (skip with -SkipTests)
    2. Publishes the app self-contained (no .NET runtime needed on the target system)
    3. Checks that the license files are part of the output
    4. Packs the portable ZIP (with the Eventide.portable marker) and writes the SHA-256
    5. Compiles build\installer\eventide.iss with ISCC from the publish folder (skip with -SkipInstaller)

    Note: a normal Release build in Visual Studio already creates the installer (target EventideInstaller in
    Eventide.App.csproj). This script is for complete release packages (portable ZIP + setup + checksums).

.EXAMPLE
    pwsh -File build\publish.ps1
    pwsh -File build\publish.ps1 -SkipTests -SkipInstaller
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Runtime = 'win-x64',
    [switch] $SkipTests,
    [switch] $SkipInstaller
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
[xml] $props = Get-Content -Raw -Path (Join-Path $root 'Directory.Build.props')
$versionNode = $props.SelectSingleNode('//Version')
if ($null -eq $versionNode) { throw 'Version not found in Directory.Build.props.' }
$version = $versionNode.InnerText.Trim()

$name = "eventide_v$version"
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts 'publish\Eventide'
$portableRoot = Join-Path $artifacts "$($name)_portable"
$zip = Join-Path $artifacts "$($name)_portable.zip"

Write-Host "Eventide $version · $Configuration · $Runtime" -ForegroundColor Green

if (-not $SkipTests) {
    dotnet test (Join-Path $root 'Eventide.slnx') -c $Configuration -p:EventideSkipInstaller=true
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

foreach ($dir in $publishDir, $portableRoot) {
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet publish (Join-Path $root 'src\Eventide.App\Eventide.App.csproj') -c $Configuration -r $Runtime --self-contained true `
    -p:PublishReadyToRun=false -p:DebugType=none -p:EventideSkipInstaller=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Publishing Eventide failed.' }

foreach ($required in 'Eventide.exe', 'README.md', 'licenses\LICENSE.md', 'licenses\README.md', 'licenses\third-party\AVALONIA.md') {
    if (-not (Test-Path (Join-Path $publishDir $required))) { throw "Missing in output: $required" }
}

# Portable ZIP: same files plus the marker that keeps the settings next to the EXE (data\).
Copy-Item -Recurse $publishDir $portableRoot
Set-Content -Path (Join-Path $portableRoot 'Eventide.portable') -Value 'Settings are stored in the data folder next to Eventide.exe.' -Encoding utf8
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $portableRoot '*') -DestinationPath $zip
(Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant() + "  $(Split-Path -Leaf $zip)" | Set-Content -Encoding ascii "$zip.sha256"
Remove-Item -Recurse -Force $portableRoot
Write-Host "Portable: $zip" -ForegroundColor Green

if (-not $SkipInstaller) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($null -eq $iscc) {
        Write-Warning 'Inno Setup 6 not found – installer skipped.'
    }
    else {
        & $iscc "/DAppVersion=$version" "/DSourceDir=$publishDir" "/DOutputDir=$artifacts" (Join-Path $root 'build\installer\eventide.iss')
        if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }
        $setup = Join-Path $artifacts "$($name)_setup.exe"
        (Get-FileHash -Algorithm SHA256 $setup).Hash.ToLowerInvariant() + "  $(Split-Path -Leaf $setup)" | Set-Content -Encoding ascii "$setup.sha256"
        Write-Host "Setup: $setup" -ForegroundColor Green
    }
}
