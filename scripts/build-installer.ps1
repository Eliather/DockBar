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

$repoRoot = if (Test-Path (Join-Path $PSScriptRoot "..\DockBar.csproj")) {
    (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
} else {
    $PSScriptRoot
}
Set-Location $repoRoot

if (-not (Test-Path "dist")) {
    New-Item -ItemType Directory -Path "dist" -Force | Out-Null
}

if (-not $SkipPublish) {
    dotnet publish $Project -c Release -r $Runtime --self-contained false -o publish
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$makensis = Get-MakeNsisPath
Write-Host "Usando NSIS en: $makensis"

& $makensis "DockBar.nsi"
$nsisExit = $LASTEXITCODE

# ---------------------------------------------------------
# Firma de codigo (Code Signing)
# ---------------------------------------------------------
$cert = Get-ChildItem -Path cert:\CurrentUser\My | Where-Object { $_.Subject -match "CN=Eliather$" } | Select-Object -First 1

if ($cert) {
    Write-Host "Certificado encontrado: $($cert.Thumbprint). Firmando ejecutables..."
    
    # Firmar el ejecutable principal
    if (Test-Path "publish\DockBar.exe") {
        Set-AuthenticodeSignature -Certificate $cert -FilePath "publish\DockBar.exe" -TimestampServer "http://timestamp.digicert.com" | Out-Null
        Write-Host "publish\DockBar.exe firmado."
    }

    # Firmar el instalador
    $setupExe = if (Test-Path "dist\DockBarSetup.exe") { "dist\DockBarSetup.exe" } else { "DockBarSetup.exe" }
    if (Test-Path $setupExe) {
        Set-AuthenticodeSignature -Certificate $cert -FilePath $setupExe -TimestampServer "http://timestamp.digicert.com" | Out-Null
        Write-Host "$setupExe firmado."
    }
} else {
    Write-Host "No se encontro un certificado para 'Eliather'. Omitiendo la firma de codigo."
}

exit $nsisExit
