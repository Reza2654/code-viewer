# ==============================================================================
# Code Viewer - Windows 11 Modern Context Menu Registration Script
# Registers IExplorerCommand Sparse Package + Classic Fallback (Zero hardcoded paths)
# ==============================================================================

[CmdletBinding()]
param(
    [string]$TargetDir = "",
    [switch]$SkipBuild,
    [switch]$RestartExplorer
)

$ErrorActionPreference = "Stop"

# 1. Determine base directories dynamically
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RootDir = Split-Path -Parent $ScriptDir

if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = Join-Path $RootDir "dist"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Code Viewer - Windows 11 Context Menu Registration" -ForegroundColor Cyan
Write-Host " Target Directory: $TargetDir" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. Build and publish if requested/needed
if (-not $SkipBuild) {
    Write-Host "[1/5] Building Code Viewer and Shell Extension..." -ForegroundColor Yellow
    
    $slnPath = Join-Path $RootDir "CodeViewer.slnx"
    dotnet publish (Join-Path $RootDir "src\CodeViewer\CodeViewer.csproj") -c Release -o $TargetDir --nologo | Out-Null
    dotnet publish (Join-Path $RootDir "src\CodeViewer.ShellExtension\CodeViewer.ShellExtension.csproj") -c Release -o $TargetDir --nologo | Out-Null
    Write-Host "      Build succeeded." -ForegroundColor Green
}

# 3. Verify required binaries exist in target directory
$exePath = Join-Path $TargetDir "CodeViewer.exe"
$comHostPath = Join-Path $TargetDir "CodeViewer.ShellExtension.comhost.dll"

if (-not (Test-Path $exePath)) {
    throw "CodeViewer.exe not found in $TargetDir. Run build first."
}
if (-not (Test-Path $comHostPath)) {
    throw "CodeViewer.ShellExtension.comhost.dll not found in $TargetDir."
}

# 4. Copy packaging files into target directory
Write-Host "[2/5] Preparing packaging manifest and assets..." -ForegroundColor Yellow
$manifestSource = Join-Path $RootDir "packaging\AppxManifest.xml"
$manifestDest = Join-Path $TargetDir "AppxManifest.xml"
Copy-Item $manifestSource $manifestDest -Force

$assetsSource = Join-Path $RootDir "packaging\Assets"
$assetsDest = Join-Path $TargetDir "Assets"
if (Test-Path $assetsSource) {
    Copy-Item $assetsSource $assetsDest -Recurse -Force
}

# 5. Remove any previously registered package version
Write-Host "[3/5] Checking previous package registrations..." -ForegroundColor Yellow
$existingPkg = Get-AppxPackage "CodeViewer.ModernShell" -ErrorAction SilentlyContinue
if ($existingPkg) {
    Write-Host "      Unregistering previous version: $($existingPkg.PackageFullName)" -ForegroundColor Gray
    Remove-AppxPackage -Package $existingPkg.PackageFullName -ErrorAction SilentlyContinue
}

# 6. Register Windows 11 Sparse Package with IExplorerCommand
Write-Host "[4/5] Registering Windows 11 Modern Context Menu (Sparse Package)..." -ForegroundColor Yellow
try {
    Add-AppxPackage -Register $manifestDest -ExternalLocation $TargetDir
    Write-Host "      Windows 11 Modern Context Menu registered successfully!" -ForegroundColor Green
}
catch {
    Write-Warning "Sparse package registration returned: $($_.Exception.Message)"
}

# 7. Register Classic context menu and Open With entries (maximum compatibility)
Write-Host "[5/5] Registering Classic Shell Fallback & Windows 'Open with'..." -ForegroundColor Yellow

# All files
$kFile = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\*\shell\CodeViewer")
$kFile.SetValue("", "Open with Code Viewer")
$kFile.SetValue("Icon", "`"$exePath`",0")
$kFile.Close()
$kFileCmd = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\*\shell\CodeViewer\command")
$kFileCmd.SetValue("", "`"$exePath`" `"%1`"")
$kFileCmd.Close()

# Folders
$kDir = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\Directory\shell\CodeViewer")
$kDir.SetValue("", "Open with Code Viewer")
$kDir.SetValue("Icon", "`"$exePath`",0")
$kDir.Close()
$kDirCmd = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\Directory\shell\CodeViewer\command")
$kDirCmd.SetValue("", "`"$exePath`" `"%1`"")
$kDirCmd.Close()

# Windows Open With Application
$kApp = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\Applications\CodeViewer.exe\shell\open\command")
$kApp.SetValue("", "`"$exePath`" `"%1`"")
$kApp.Close()
$kTypes = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\Applications\CodeViewer.exe\SupportedTypes")
$extensions = @(".txt", ".cs", ".js", ".ts", ".html", ".css", ".json", ".xml", ".py", ".dart", ".cpp", ".c", ".h", ".rs", ".go", ".sql", ".sh", ".ps1", ".yaml", ".yml", ".md")
foreach ($ext in $extensions) {
    $kTypes.SetValue($ext, "")
}
$kTypes.Close()

Write-Host "      Registry registration complete." -ForegroundColor Green

# 8. Optional Explorer restart
if ($RestartExplorer) {
    Write-Host "Restarting File Explorer to refresh shell extensions..." -ForegroundColor Cyan
    Stop-Process -Name explorer -Force
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " REGISTRATION COMPLETE!" -ForegroundColor Green
Write-Host " 'Open with Code Viewer' is now active in Windows 11." -ForegroundColor Green
Write-Host " Test by right-clicking any file in File Explorer." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
