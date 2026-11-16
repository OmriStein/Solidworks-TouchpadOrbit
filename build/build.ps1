# Builds TouchpadOrbit.dll from src\ and writes the installers to dist\.
# Needs: Visual Studio or Build Tools (for csc.exe). SOLIDWORKS is not needed to build.
param([string]$Version = '1.4.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$csc = & $vswhere -products * -find 'MSBuild\Current\Bin\Roslyn\csc.exe' | Select-Object -First 1
if (-not $csc) { throw 'csc.exe not found - install Visual Studio or Build Tools.' }

$obj = Join-Path $root 'obj'; $dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $obj, $dist | Out-Null
& $csc /nologo /target:library /platform:x64 /optimize+ /deterministic /out:"$obj\TouchpadOrbit.dll" `
    /r:System.Windows.Forms.dll "$root\src\TouchpadOrbit.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("$obj\TouchpadOrbit.dll"))
$lines = ($b64 -split '(.{1000})' | ? { $_ }) -join "`r`n"
$ascii = New-Object Text.ASCIIEncoding
$inst = [IO.File]::ReadAllText("$PSScriptRoot\Install.template.cmd").Replace('{{DLL_BASE64}}', $lines) -replace "`r?`n", "`r`n"
[IO.File]::WriteAllText("$dist\Install Touchpad Orbit.cmd", $inst, $ascii)
$un = [IO.File]::ReadAllText("$PSScriptRoot\Uninstall Touchpad Orbit.cmd") -replace "`r?`n", "`r`n"
[IO.File]::WriteAllText("$dist\Uninstall Touchpad Orbit.cmd", $un, $ascii)

# release zip (not committed)
$zip = Join-Path $obj "TouchpadOrbit-v$Version.zip"
Compress-Archive -Force -DestinationPath $zip -LiteralPath "$dist\Install Touchpad Orbit.cmd", "$dist\Uninstall Touchpad Orbit.cmd", "$root\README.md", "$root\LICENSE"
Write-Host "Built dist\ and $zip" -ForegroundColor Green
