param(
    [string]$Project = "DockBar.csproj",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputFile = "DockBar.msix",
    [switch]$SkipPublish,
    [switch]$SkipSigning
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

function Ensure-PackagingTools {
    $toolsDir = Join-Path $repoRoot ".tools"
    $makeappx = Join-Path $toolsDir "makeappx.exe"
    $signtool = Join-Path $toolsDir "signtool.exe"

    if ((Test-Path $makeappx) -and (Test-Path $signtool)) {
        return @{ MakeAppx = $makeappx; SignTool = $signtool }
    }

    # 1. Chequear PATH y Windows Kits
    $cmdMake = Get-Command makeappx -ErrorAction SilentlyContinue
    $cmdSign = Get-Command signtool -ErrorAction SilentlyContinue
    if ($cmdMake -and $cmdSign) {
        return @{ MakeAppx = $cmdMake.Source; SignTool = $cmdSign.Source }
    }

    $kitMake = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    $kitSign = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if ($kitMake -and $kitSign) {
        return @{ MakeAppx = $kitMake.FullName; SignTool = $kitSign.FullName }
    }

    # 2. Descargar herramientas oficiales de NuGet Microsoft.Windows.SDK.BuildTools
    Write-Host "Herramientas de empaquetado no encontradas. Descargando Microsoft.Windows.SDK.BuildTools..." -ForegroundColor Cyan
    if (-not (Test-Path $toolsDir)) {
        New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
    }

    $pkgUrl = "https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools/10.0.22621.756"
    $zipPath = Join-Path $toolsDir "sdk_buildtools.zip"

    Invoke-WebRequest -Uri $pkgUrl -OutFile $zipPath -UseBasicParsing
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $x64Entries = $zip.Entries | Where-Object { $_.FullName -like "bin/*/x64/*" -and -not $_.FullName.EndsWith("/") }
        foreach ($entry in $x64Entries) {
            $destFileName = Split-Path $entry.FullName -Leaf
            $destFile = Join-Path $toolsDir $destFileName
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destFile, $true)
        }
    }
    finally {
        $zip.Dispose()
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    }

    if ((Test-Path $makeappx) -and (Test-Path $signtool)) {
        Write-Host "Herramientas oficiales listas en .tools\" -ForegroundColor Green
        return @{ MakeAppx = $makeappx; SignTool = $signtool }
    }

    throw "No se pudieron preparar las herramientas de empaquetado."
}

# 1. Compilacion y publicacion
if (-not $SkipPublish) {
    Write-Host "Publicando aplicacion en modo $Configuration ($Runtime)..." -ForegroundColor Cyan
    dotnet publish $Project -c $Configuration -r $Runtime --self-contained false -o publish
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

# 2. Generacion de Assets si no existen
$assetsDir = Join-Path $repoRoot "Package\Assets"
if (-not (Test-Path (Join-Path $assetsDir "Square150x150Logo.png"))) {
    Write-Host "Generando assets visuales para el paquete MSIX..." -ForegroundColor Cyan
    & (Join-Path $repoRoot "Package\generate-assets.ps1")
}

# 3. Preparar directorio de staging para MSIX
$stagingDir = Join-Path $repoRoot "obj\msix_layout"
if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

Write-Host "Preparando estructura del paquete MSIX en $stagingDir..." -ForegroundColor Cyan
Copy-Item (Join-Path $repoRoot "publish\*") -Destination $stagingDir -Recurse -Force
Copy-Item (Join-Path $repoRoot "Package\AppxManifest.xml") -Destination (Join-Path $stagingDir "AppxManifest.xml") -Force

$stagingAssets = Join-Path $stagingDir "Assets"
New-Item -ItemType Directory -Path $stagingAssets -Force | Out-Null
Copy-Item (Join-Path $assetsDir "*") -Destination $stagingAssets -Force

# 4. Empaquetar con makeappx.exe
$tools = Ensure-PackagingTools
Write-Host "Usando makeappx en: $($tools.MakeAppx)" -ForegroundColor Gray

$outputFullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputFile))
if (Test-Path $outputFullPath) {
    Remove-Item $outputFullPath -Force
}

Write-Host "Creando paquete $OutputFile..." -ForegroundColor Cyan
& $tools.MakeAppx pack /d $stagingDir /p $outputFullPath /o /nv
$packExit = $LASTEXITCODE

if ($packExit -ne 0) {
    throw "Fallo la creacion del paquete MSIX con codigo de salida $packExit"
}

# 5. Firma de codigo
if (-not $SkipSigning) {
    [xml]$manifestXml = Get-Content (Join-Path $repoRoot "Package\AppxManifest.xml")
    $expectedPublisher = $manifestXml.Package.Identity.Publisher

    $cert = Get-ChildItem -Path cert:\CurrentUser\My, cert:\LocalMachine\My -ErrorAction SilentlyContinue |
            Where-Object { $_.Subject -eq $expectedPublisher -or $_.Subject -like "*$expectedPublisher*" } |
            Select-Object -First 1

    if ($cert) {
        Write-Host "Certificado coincidente encontrado ($($cert.Thumbprint) - $($cert.Subject)). Firmando $OutputFile con signtool..." -ForegroundColor Cyan
        & $tools.SignTool sign /fd SHA256 /sha1 $cert.Thumbprint /tr "http://timestamp.digicert.com" /td SHA256 $outputFullPath
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Paquete firmado exitosamente." -ForegroundColor Green
        } else {
            Write-Host "Advertencia: signtool retorno codigo de salida $LASTEXITCODE" -ForegroundColor Yellow
        }
    } else {
        Write-Host "NOTA: No se encontro certificado local para '$expectedPublisher'." -ForegroundColor Yellow
        Write-Host "Para la Microsoft Store esto es NORMAL (Microsoft firma el paquete automaticamente al publicarlo)." -ForegroundColor DarkGray
        Write-Host "Para instalarlo localmente en tu equipo, genera un cert para este Publisher o ejecuta .\install-cert.ps1." -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "   Paquete MSIX creado exitosamente: $OutputFile" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host "Para instalarlo localmente en tu maquina (doble clic):"
Write-Host "  1. Asegurate de tener instalado el certificado (.\install-cert.ps1)"
Write-Host "  2. Haz doble clic en $OutputFile"
Write-Host ""
Write-Host "Para enviarlo a la Microsoft Store:"
Write-Host "  - Reemplaza los datos en Package\AppxManifest.xml con los de Partner Center"
Write-Host "  - Ejecuta .\build-msix.ps1"
Write-Host "  - Sube $OutputFile en tu submission de Partner Center"
Write-Host "========================================================" -ForegroundColor Green

exit $packExit
