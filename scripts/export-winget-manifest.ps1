# Code Viewer - WinGet Manifest Exporter Script
# Generates valid Microsoft Windows Package Manager (winget) YAML manifests

param (
    [string]$Version = "",
    [string]$InstallerSha256 = ""
)

$ErrorActionPreference = "Stop"
$rootDir = (Resolve-Path "$PSScriptRoot\..").Path
$distDir = "$rootDir\dist"

# 1. Determine Version
if ([string]::IsNullOrWhiteSpace($Version)) {
    $csprojContent = Get-Content "$rootDir\src\CodeViewer\CodeViewer.csproj" -Raw
    if ($csprojContent -match '<Version>(.*?)</Version>') {
        $Version = $matches[1]
    } else {
        $Version = "1.0.1-beta.5"
    }
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "   Code Viewer - WinGet Manifest Exporter v$Version" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 2. Determine Installer SHA-256
$installerName = "CodeViewer-v$Version-Setup.exe"
$installerPath = "$distDir\$installerName"

if ([string]::IsNullOrWhiteSpace($InstallerSha256)) {
    if (Test-Path "$distDir\checksums.sha256") {
        $lines = Get-Content "$distDir\checksums.sha256"
        foreach ($line in $lines) {
            if ($line -match "([a-fA-F0-9]{64})\s+.*$installerName") {
                $InstallerSha256 = $matches[1].ToUpperInvariant()
                break
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($InstallerSha256) -and (Test-Path $installerPath)) {
        Write-Host "Calculating SHA-256 for $installerPath..." -ForegroundColor Yellow
        $InstallerSha256 = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash.ToUpperInvariant()
    }
}

if ([string]::IsNullOrWhiteSpace($InstallerSha256)) {
    $InstallerSha256 = "0000000000000000000000000000000000000000000000000000000000000000"
    Write-Warning "Installer SHA-256 could not be determined. Placeholder hash used. Re-run after building release package."
} else {
    Write-Host "Installer SHA-256: $InstallerSha256" -ForegroundColor Green
}

# 3. Target Directories
$packageId = "Reza2654.CodeViewer"
$manifestTypeVersion = "1.6.0"
$packagingDir = "$rootDir\packaging\winget"
$distWingetDir = "$distDir\winget\manifests\r\Reza2654\CodeViewer\$Version"

if (-not (Test-Path $packagingDir)) { New-Item -ItemType Directory -Path $packagingDir -Force | Out-Null }
if (-not (Test-Path $distWingetDir)) { New-Item -ItemType Directory -Path $distWingetDir -Force | Out-Null }

$installerUrl = "https://github.com/Reza2654/code-viewer/releases/download/v$Version/$installerName"

# 4. Generate Manifest 1: Version Manifest
$versionYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$manifestTypeVersion.schema.json

PackageIdentifier: $packageId
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $manifestTypeVersion
"@

# 5. Generate Manifest 2: Installer Manifest
$installerYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$manifestTypeVersion.schema.json

PackageIdentifier: $packageId
PackageVersion: $Version
MinimumOSVersion: 10.0.17763.0
InstallerType: inno
Scope: user
InstallModes:
- interactive
- silent
- silentWithProgress
Installers:
- Architecture: x64
  InstallerUrl: $installerUrl
  InstallerSha256: $InstallerSha256
  ProductCode: '{D37F2C0B-9799-4A9B-B1D2-8A73DC88235A}'
  UpgradeBehavior: install
ManifestType: installer
ManifestVersion: $manifestTypeVersion
"@

# 6. Generate Manifest 3: Default Locale Manifest
$localeYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$manifestTypeVersion.schema.json

PackageIdentifier: $packageId
PackageVersion: $Version
PackageLocale: en-US
Publisher: Reza2654
PublisherUrl: https://github.com/Reza2654
PublisherSupportUrl: https://github.com/Reza2654/code-viewer/issues
Author: Reza2654
PackageName: Code Viewer
PackageUrl: https://github.com/Reza2654/code-viewer
License: MIT
LicenseUrl: https://github.com/Reza2654/code-viewer/blob/main/LICENSE
Copyright: Copyright (c) Reza2654
ShortDescription: Fast, lightweight code viewer and editor with themes, plugins, and Windows 11 context menu integration.
Description: Code Viewer is an open-source, fast code viewer and editor built with C# and Avalonia. Features include customizable syntax themes, font customization, plugin support, and deep Windows 11 right-click context menu integration.
Moniker: codeviewer
Tags:
- code
- developer-tools
- editor
- text-editor
- viewer
- syntax-highlighting
- windows11
- avalonia
ReleaseNotesUrl: https://github.com/Reza2654/code-viewer/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: $manifestTypeVersion
"@

# 7. Write to packaging/winget and dist/winget
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

foreach ($targetFolder in @($packagingDir, $distWingetDir)) {
    [System.IO.File]::WriteAllText("$targetFolder\$packageId.yaml", $versionYaml.Trim() + "`n", $utf8NoBom)
    [System.IO.File]::WriteAllText("$targetFolder\$packageId.installer.yaml", $installerYaml.Trim() + "`n", $utf8NoBom)
    [System.IO.File]::WriteAllText("$targetFolder\$packageId.locale.en-US.yaml", $localeYaml.Trim() + "`n", $utf8NoBom)
}

Write-Host "  -> WinGet manifests written to: $packagingDir" -ForegroundColor Green
Write-Host "  -> WinGet package structure written to: $distWingetDir" -ForegroundColor Green

# 8. Create ZIP archive of manifests for distribution
$wingetZip = "$distDir\CodeViewer-v$Version-winget-manifests.zip"
if (Test-Path $wingetZip) { Remove-Item $wingetZip -Force }
Compress-Archive -Path "$rootDir\packaging\winget\*" -DestinationPath $wingetZip -CompressionLevel Optimal
Write-Host "  -> WinGet manifest archive created: $wingetZip" -ForegroundColor Green

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host " WinGet Submission Instructions:" -ForegroundColor Yellow
Write-Host " 1. Test locally:" -ForegroundColor Gray
Write-Host "    winget validate $packagingDir" -ForegroundColor White
Write-Host "    winget install --manifest $packagingDir" -ForegroundColor White
Write-Host " 2. Submit to microsoft/winget-pkgs via wingetcreate CLI:" -ForegroundColor Gray
Write-Host "    wingetcreate submit $installerUrl" -ForegroundColor White
Write-Host "=================================================" -ForegroundColor Cyan
