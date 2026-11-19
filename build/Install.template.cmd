<# : batch part - runs the PowerShell below
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -Command "$f='%~f0'; iex ([IO.File]::ReadAllText($f))"
pause
exit /b
#>
# ===== Touchpad Orbit for SOLIDWORKS - single-file installer =====
# https://github.com/OmriStein/Solidworks-TouchpadOrbit  (MIT License)
$ErrorActionPreference = 'Stop'
$DLL = @"
{{DLL_BASE64}}
"@
$guid = '{8C3E1F2A-6B4D-4E7F-9A15-2D3C4B5A6E71}'
$dest = 'C:\ProgramData\TouchpadOrbit'
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

# every SOLIDWORKS instance must be closed, otherwise the add-in DLL is locked
while ($swp = @(Get-Process SLDWORKS -ErrorAction SilentlyContinue)) {
    Write-Host "$($swp.Count) SOLIDWORKS window(s) still open. Save your work and close all of them." -ForegroundColor Yellow
    Read-Host 'Then press Enter to continue (or close this window to cancel)'
}
# load at SOLIDWORKS start-up (for the user who runs this)
New-Item "HKCU:\Software\SolidWorks\AddInsStartup\$guid" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\SolidWorks\AddInsStartup\$guid" '(default)' 1 -Type DWord

if (-not $isAdmin) {
    Write-Host 'Asking for administrator rights...'
    $p = Start-Process powershell -Verb RunAs -Wait -PassThru -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"`$f='$f'; iex ([IO.File]::ReadAllText(`$f))`""
    exit
}

try {
    Get-Process TouchpadOrbit -ErrorAction SilentlyContinue | Stop-Process -Force   # old tray version
    $sw = Get-ChildItem 'HKLM:\SOFTWARE\SolidWorks' | ? PSChildName -match '^SOLIDWORKS \d{4}$' | Sort-Object PSChildName -Descending |
          % { (Get-ItemProperty "$($_.PSPath)\Setup" -ErrorAction SilentlyContinue).'SolidWorks Folder' } |
          ? { $_ -and (Test-Path (Join-Path $_ 'sldworks.exe')) } | Select-Object -First 1
    if (-not $sw) { throw 'SOLIDWORKS installation not found.' }
    Write-Host "SOLIDWORKS: $sw"

    $regasm = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
    New-Item -ItemType Directory -Force $dest | Out-Null
    if (Test-Path "$dest\TouchpadOrbit.dll") { & $regasm /u "$dest\TouchpadOrbit.dll" /silent | Out-Null }
    Remove-Item "$dest\SolidWorks.Interop.*.dll" -Force -ErrorAction SilentlyContinue   # left over from versions before 1.4
    [IO.File]::WriteAllBytes("$dest\TouchpadOrbit.dll", [Convert]::FromBase64String(($DLL -replace '\s','')))
    & $regasm /codebase "$dest\TouchpadOrbit.dll" /silent
    if ($LASTEXITCODE -ne 0) { throw 'Registration failed.' }
    Write-Host "`nTouchpad Orbit installed. Restart SOLIDWORKS." -ForegroundColor Green
    Write-Host 'Two-finger drag: rotate.  Two-finger tap and hold, then drag (or Ctrl + two-finger drag): pan.  Pinch: zoom.  Twist two fingers: roll.'
} catch { Write-Host "`nInstall failed: $_" -ForegroundColor Red }
Read-Host 'Press Enter to close'
