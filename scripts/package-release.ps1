# Code Viewer - Automated Release Packaging Script
# Generates Portable ZIP and Inno Setup Windows Installer

$ErrorActionPreference = "Stop"
$rootDir = (Resolve-Path "$PSScriptRoot\..").Path
$distDir = "$rootDir\dist"
$installerScript = "$rootDir\installer\CodeViewer.iss"

# Detect version from CodeViewer.csproj
$csprojContent = Get-Content "$rootDir\src\CodeViewer\CodeViewer.csproj" -Raw
if ($csprojContent -match '<Version>(.*?)</Version>') {
    $version = $matches[1]
} else {
    $version = "1.0.1-beta.4"
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "   Code Viewer - Build & Package Release v$version" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# Ensure any running CodeViewer process is stopped before touching files
Get-Process -Name "CodeViewer" -ErrorAction SilentlyContinue | Stop-Process -Force

# 1. Clean dist directory (preserve any old setup while building)
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
} else {
    Get-ChildItem -Path $distDir -Exclude "*Setup*.exe","*.zip" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# 2. Publish Main CodeViewer Application (Self-Contained win-x64)
Write-Host "[1/5] Publishing CodeViewer (Self-Contained Release win-x64)..." -ForegroundColor Yellow
dotnet publish "$rootDir\src\CodeViewer\CodeViewer.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $distDir

if (-not (Test-Path "$distDir\CodeViewer.exe")) {
    throw "Build verification failed: $distDir\CodeViewer.exe was not produced by dotnet publish!"
}
$exeSize = (Get-Item "$distDir\CodeViewer.exe").Length
Write-Host "  -> Verified CodeViewer.exe ($([math]::Round($exeSize / 1KB, 1)) KB)" -ForegroundColor Green

# 3. Publish Native Shell Extension COM Host
Write-Host "[2/5] Publishing CodeViewer.ShellExtension..." -ForegroundColor Yellow
dotnet publish "$rootDir\src\CodeViewer.ShellExtension\CodeViewer.ShellExtension.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o "$distDir\shell_ext_temp"

# Move shell extension files into dist root
Move-Item "$distDir\shell_ext_temp\CodeViewer.ShellExtension.*" $distDir -Force
Remove-Item "$distDir\shell_ext_temp" -Recurse -Force -ErrorAction SilentlyContinue

# 4. Copy Packaging & Explorer Integration Assets
Write-Host "[3/5] Staging Sparse Package & Assets..." -ForegroundColor Yellow
Copy-Item "$rootDir\packaging\AppxManifest.xml" $distDir -Force
if (Test-Path "$rootDir\packaging\Assets") {
    Copy-Item "$rootDir\packaging\Assets" "$distDir\Assets" -Recurse -Force
}

# Copy setup scripts to dist
$distScripts = "$distDir\scripts"
if (-not (Test-Path $distScripts)) { New-Item -ItemType Directory -Path $distScripts -Force | Out-Null }
Copy-Item "$rootDir\scripts\register-windows11-context-menu.ps1" $distScripts -Force
Copy-Item "$rootDir\scripts\unregister-windows11-context-menu.ps1" $distScripts -Force
Copy-Item "$rootDir\scripts\add-context-menu.reg" $distScripts -Force
Copy-Item "$rootDir\scripts\remove-context-menu.reg" $distScripts -Force

# 5. Create Portable ZIP
Write-Host "[4/5] Creating Portable ZIP archive..." -ForegroundColor Yellow
$zipPath = "$distDir\CodeViewer-v$version-win-x64-portable.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

# Gather files for ZIP (include CodeViewer.exe, exclude installer executables and archives)
$zipFiles = Get-ChildItem -Path $distDir -Exclude "*Setup*.exe","*.zip","*.iss","checksums.sha256" | Select-Object -ExpandProperty FullName
Compress-Archive -Path $zipFiles -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "  -> Portable ZIP created: $zipPath" -ForegroundColor Green

# Verify CodeViewer.exe is inside the ZIP
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipArchive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$hasExe = $zipArchive.Entries | Where-Object { $_.FullName -eq "CodeViewer.exe" }
$zipArchive.Dispose()
if (-not $hasExe) {
    throw "Verification failed: CodeViewer.exe is missing from portable ZIP archive!"
}
Write-Host "  -> Verified CodeViewer.exe is packaged in portable ZIP" -ForegroundColor Green

# 6. Compile Inno Setup Installer if ISCC is available
Write-Host "[5/5] Checking for Inno Setup compiler (ISCC)..." -ForegroundColor Yellow
$isccPath = $null
$candidatePaths = @(
    "ISCC",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

foreach ($cand in $candidatePaths) {
    if (Get-Command $cand -ErrorAction SilentlyContinue) {
        $isccPath = $cand
        break
    }
    if (Test-Path $cand) {
        $isccPath = $cand
        break
    }
}

$expectedInstaller = "$distDir\CodeViewer-v$version-Setup.exe"
if ($isccPath) {
    Write-Host "  Found Inno Setup at: $isccPath" -ForegroundColor Cyan
    Write-Host "  Compiling installer..." -ForegroundColor Yellow
    & $isccPath "$installerScript"
    if (Test-Path $expectedInstaller) {
        Write-Host "  -> Installer created: $expectedInstaller" -ForegroundColor Green
    } else {
        Write-Warning "Installer compilation finished, but expected file $expectedInstaller was not found."
    }
} else {
    Write-Warning "Inno Setup compiler (ISCC.exe) not found on PATH or Program Files. Portable ZIP was built successfully."
}

# 7. Generate SHA-256 checksums
Write-Host "Generating SHA-256 Checksums..." -ForegroundColor Yellow
$checksumFile = "$distDir\checksums.sha256"
$hashEntries = @()
Get-ChildItem -Path "$distDir\*" -Include "*Setup*.exe","*.zip" -File | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    $hashEntries += "$hash  $($_.Name)"
}
$hashEntries | Out-File -FilePath $checksumFile -Encoding utf8
Write-Host "  -> Checksums saved to: $checksumFile" -ForegroundColor Green

# 8. Export WinGet Manifests
Write-Host "Exporting WinGet Manifests..." -ForegroundColor Yellow
$wingetScript = "$rootDir\scripts\export-winget-manifest.ps1"
if (Test-Path $wingetScript) {
    & powershell -ExecutionPolicy Bypass -File $wingetScript -Version $version
}

Write-Host "=================================================" -ForegroundColor Green
Write-Host "   Package Release Completed Successfully!       " -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
