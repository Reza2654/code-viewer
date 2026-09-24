# ==============================================================================
# Code Viewer - Windows 11 Modern Context Menu Unregistration Script
# Removes Sparse Package + Classic Fallback Registrations
# ==============================================================================

[CmdletBinding()]
param(
    [switch]$RestartExplorer
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Code Viewer - Context Menu Removal" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Remove Windows 11 Sparse Package
Write-Host "[1/2] Removing Windows 11 Modern Context Menu Package..." -ForegroundColor Yellow
$pkg = Get-AppxPackage "CodeViewer.ModernShell" -ErrorAction SilentlyContinue
if ($pkg) {
    Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue
    Write-Host "      Removed package: $($pkg.PackageFullName)" -ForegroundColor Green
} else {
    Write-Host "      No modern package registration found." -ForegroundColor Gray
}

# 2. Remove Classic Registry Keys
Write-Host "[2/2] Removing classic context menu registry keys..." -ForegroundColor Yellow

$regClasses = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Software\Classes", $true)
if ($regClasses) {
    try {
        $subStar = $regClasses.OpenSubKey("*", $true)
        if ($subStar) {
            $subShell = $subStar.OpenSubKey("shell", $true)
            if ($subShell) { $subShell.DeleteSubKeyTree("CodeViewer", $false); $subShell.Close() }
            $subStar.Close()
        }
    } catch { }

    try {
        $subDir = $regClasses.OpenSubKey("Directory", $true)
        if ($subDir) {
            $subShell = $subDir.OpenSubKey("shell", $true)
            if ($subShell) { $subShell.DeleteSubKeyTree("CodeViewer", $false); $subShell.Close() }
            $subDir.Close()
        }
    } catch { }

    try {
        $subBg = $regClasses.OpenSubKey("Directory\Background", $true)
        if ($subBg) {
            $subShell = $subBg.OpenSubKey("shell", $true)
            if ($subShell) { $subShell.DeleteSubKeyTree("CodeViewer", $false); $subShell.Close() }
            $subBg.Close()
        }
    } catch { }

    try { $regClasses.DeleteSubKeyTree("Applications\CodeViewer.exe", $false) } catch { }
    $regClasses.Close()
}
Write-Host "      Registry entries removed." -ForegroundColor Green

# 3. Optional Explorer restart
if ($RestartExplorer) {
    Write-Host "Restarting File Explorer to refresh shell cache..." -ForegroundColor Cyan
    Stop-Process -Name explorer -Force
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Code Viewer context menu uninstalled successfully." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
