$ErrorActionPreference = 'Stop'

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent (Split-Path -Parent $projectDir)
$outDir = Join-Path $projectDir 'bin'
$source = Join-Path $projectDir 'Program.cs'
$exe = Join-Path $outDir 'HotkeyClipboardPrototype.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

if (-not (Test-Path $compiler)) {
    throw 'C# compiler not found. Install .NET Framework developer tools or .NET SDK.'
}

New-Item -ItemType Directory -Path $outDir -Force | Out-Null

& $compiler `
    /nologo `
    /target:winexe `
    /optimize+ `
    /out:$exe `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built $exe"
