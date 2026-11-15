<# : batch part - runs the PowerShell below
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -Command "$f='%~f0'; iex ([IO.File]::ReadAllText($f))"
exit /b
#>
# ===== Touchpad Orbit for SOLIDWORKS - uninstaller =====
# https://github.com/OmriStein/Solidworks-TouchpadOrbit  (MIT License)
$guid = '{8C3E1F2A-6B4D-4E7F-9A15-2D3C4B5A6E71}'
$dest = 'C:\ProgramData\TouchpadOrbit'
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (Get-Process SLDWORKS -ErrorAction SilentlyContinue) {
    Write-Host 'Please close SOLIDWORKS first, then run this again.' -ForegroundColor Yellow
    Read-Host 'Press Enter to close'; exit
}

# per-user settings (for the user who runs this)
Remove-Item "HKCU:\Software\SolidWorks\AddInsStartup\$guid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\TouchpadOrbit' -Recurse -Force -ErrorAction SilentlyContinue
Get-Process TouchpadOrbit -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item "$env:LOCALAPPDATA\TouchpadOrbit" -Recurse -Force -ErrorAction SilentlyContinue

if (-not $isAdmin) {
    Write-Host 'Asking for administrator rights...'
    Start-Process powershell -Verb RunAs -Wait -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"`$f='$f'; iex ([IO.File]::ReadAllText(`$f))`""
    exit
}

try {
    $regasm = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
    if (Test-Path "$dest\TouchpadOrbit.dll") { & $regasm /u "$dest\TouchpadOrbit.dll" /silent | Out-Null }
    Remove-Item "HKLM:\SOFTWARE\SolidWorks\AddIns\$guid" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "Registry::HKEY_CLASSES_ROOT\CLSID\$guid" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item 'Registry::HKEY_CLASSES_ROOT\TouchpadOrbit.Addin' -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $dest -Recurse -Force -ErrorAction Stop
    Write-Host "`nTouchpad Orbit removed." -ForegroundColor Green
} catch { Write-Host "`nUninstall problem: $_" -ForegroundColor Red }
Read-Host 'Press Enter to close'
