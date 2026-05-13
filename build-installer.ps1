param(
    [string]$Project = "DockBar.csproj",
    [string]$Runtime = "win-x64",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

function Get-MakeNsisPath {
    $cmd = Get-Command makensis -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) {
        return $cmd.Source
    }

    $candidates = @(
        "C:\Program Files (x86)\NSIS\makensis.exe",
        "C:\Program Files\NSIS\makensis.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "NSIS no esta instalado o makensis.exe no se pudo localizar. Instala NSIS o agregalo al PATH."
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

if (-not $SkipPublish) {
    dotnet publish $Project -c Release -r $Runtime --self-contained false -o publish
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$makensis = Get-MakeNsisPath
Write-Host "Usando NSIS en: $makensis"

& $makensis "DockBar.nsi"
exit $LASTEXITCODE
