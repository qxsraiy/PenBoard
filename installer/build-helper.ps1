param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'PenBoard.SetupHelper.cpp'
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw 'vswhere.exe not found. Install Visual Studio Build Tools with C++ tools.'
}

$installation = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $installation) {
    throw 'Visual Studio C++ build tools not found.'
}

function Build-Helper([string]$vcvars, [string]$output) {
    $command = "call `"$vcvars`" >nul && cd /d `"$root`" && cl /nologo /O2 /std:c++17 /EHsc /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /Fe:`"$output`" `"$source`" /link /SUBSYSTEM:WINDOWS /ENTRY:wWinMainCRTStartup shell32.lib ole32.lib advapi32.lib"
    cmd.exe /c $command
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build $output"
    }
}

$build = Join-Path $installation 'VC\Auxiliary\Build'
Build-Helper (Join-Path $build 'vcvarsamd64_arm64.bat') (Join-Path $root 'PenBoard.SetupHelper.arm64.exe')
Build-Helper (Join-Path $build 'vcvars64.bat') (Join-Path $root 'PenBoard.SetupHelper.x64.exe')
Write-Host 'SetupHelper built.'
