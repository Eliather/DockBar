<#
.SYNOPSIS
    Sube y publica automaticamente DockBar a la Microsoft Store mediante la Submission API oficial.
.DESCRIPTION
    Automatiza el flujo completo de publicacion en Microsoft Partner Center sin necesidad de abrir un navegador web:
    1. Autenticacion con Azure AD (OAuth 2.0).
    2. Creacion de nueva submission para la aplicacion.
    3. Carga directa del archivo MSIX al almacenamiento Azure Blob Storage.
    4. Actualizacion de metadatos del paquete.
    5. Envio a certificacion oficial (Commit).
.EXAMPLE
    .\publish-store.ps1
.EXAMPLE
    .\publish-store.ps1 -PackagePath "DockBar.msix" -SkipCommit
#>

param(
    [string]$AppId = "9NKG6MK32732",
    [string]$PackagePath = "DockBar.msix",
    [string]$CredentialsPath = ".store-credentials.json",
    [string]$TenantId,
    [string]$ClientId,
    [string]$ClientSecret,
    [switch]$SkipCommit
)

[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   DockBar - Publicacion Directa a Microsoft Store (API)   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ---------------------------------------------------------
# 1. Validar el paquete local
# ---------------------------------------------------------
if (-not (Test-Path $PackagePath)) {
    Write-Host "ERROR: No se encontro el archivo de paquete: $PackagePath" -ForegroundColor Red
    Write-Host "Por favor ejecuta primero .\build-msix.ps1 para generarlo." -ForegroundColor Yellow
    exit 1
}

$fullPackagePath = Resolve-Path $PackagePath
$packageSizeMb = [math]::Round(((Get-Item $fullPackagePath).Length / 1MB), 2)
$fileName = Split-Path $fullPackagePath -Leaf

# Leer version del paquete MSIX
$packageVersion = "Desconocida"
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($fullPackagePath)
    $entry = $zip.GetEntry('AppxManifest.xml')
    if ($entry) {
        $stream = $entry.Open()
        $reader = New-Object System.IO.StreamReader($stream)
        $manifestXml = [xml]$reader.ReadToEnd()
        $reader.Close()
        $stream.Close()
        $packageVersion = $manifestXml.Package.Identity.Version
    }
    $zip.Dispose()
} catch {
}

Write-Host "Paquete detectado: $fileName ($packageSizeMb MB)" -ForegroundColor Green
Write-Host "Version interna:   $packageVersion" -ForegroundColor Green
Write-Host ""

# ---------------------------------------------------------
# 2. Cargar Credenciales de Azure AD
# ---------------------------------------------------------
if (-not $TenantId -or -not $ClientId -or -not $ClientSecret) {
    if ($env:STORE_TENANT_ID -and $env:STORE_CLIENT_ID -and $env:STORE_CLIENT_SECRET) {
        $TenantId = $env:STORE_TENANT_ID
        $ClientId = $env:STORE_CLIENT_ID
        $ClientSecret = $env:STORE_CLIENT_SECRET
    } elseif (Test-Path $CredentialsPath) {
        try {
            $jsonCreds = Get-Content $CredentialsPath -Raw | ConvertFrom-Json
            $TenantId = $jsonCreds.TenantId
            $ClientId = $jsonCreds.ClientId
            $ClientSecret = $jsonCreds.ClientSecret
        } catch {
            Write-Host "Advertencia: Error al leer $CredentialsPath" -ForegroundColor Yellow
        }
    }
}

if (-not $TenantId -or -not $ClientId -or -not $ClientSecret -or $TenantId -like "*00000000*") {
    Write-Host "==========================================================" -ForegroundColor Yellow
    Write-Host "  CONFIGURACION DE CREDENCIALES REQUERIDA (Unica vez)    " -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Yellow
    Write-Host "Para utilizar la API de Microsoft Store necesitas asociar una aplicacion de Azure AD:"
    Write-Host ""
    Write-Host "1. Abre Microsoft Partner Center -> Configuracion (icono de engranaje) -> Configuracion de la cuenta." -ForegroundColor Cyan
    Write-Host "   Enlace directo: https://partner.microsoft.com/dashboard/account/usermanagement"
    Write-Host "2. Ve a 'Aplicaciones de Azure AD' -> 'Crear aplicacion de Azure AD'." -ForegroundColor Cyan
    Write-Host "3. Copia el 'Id. de inquilino (Tenant ID)' y el 'Id. de cliente (Client ID)'." -ForegroundColor Cyan
    Write-Host "4. Crea una clave secreta (Client Secret) y copiala." -ForegroundColor Cyan
    Write-Host "5. Crea el archivo .store-credentials.json en esta carpeta con tus datos:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host (@"
{
  "TenantId": "tu-tenant-id",
  "ClientId": "tu-client-id",
  "ClientSecret": "tu-client-secret"
}
"@) -ForegroundColor Gray
    Write-Host ""
    Write-Host "(Se ha creado una plantilla en .store-credentials.example.json)" -ForegroundColor Gray
    exit 1
}

# ---------------------------------------------------------
# 3. Obtener Token de Acceso de Azure AD
# ---------------------------------------------------------
Write-Host "1. Autenticando con Azure AD..." -ForegroundColor Cyan
$tokenEndpoint = "https://login.microsoftonline.com/$TenantId/oauth2/token"
$tokenBody = @{
    grant_type    = "client_credentials"
    client_id     = $ClientId
    client_secret = $ClientSecret
    resource      = "https://api.partner.microsoft.com"
}

$tokenResponse = $null
try {
    $tokenResponse = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $tokenBody -ContentType "application/x-www-form-urlencoded"
} catch {
    Write-Host "Error autenticando con Azure AD: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
    exit 1
}

$accessToken = $tokenResponse.access_token
Write-Host "   Autenticacion exitosa. Token obtenido." -ForegroundColor Green

$headers = @{
    "Authorization" = "Bearer $accessToken"
    "Accept"        = "application/json"
}

$apiBase = "https://manage.devcenter.microsoft.com/v1.0/my/applications"

# ---------------------------------------------------------
# 4. Obtener o Crear la Submission
# ---------------------------------------------------------
Write-Host "2. Consultando aplicacion en Microsoft Partner Center ($AppId)..." -ForegroundColor Cyan
$appUrl = "$apiBase/$AppId"

try {
    $appData = Invoke-RestMethod -Uri $appUrl -Method Get -Headers $headers
} catch {
    Write-Host "Error al consultar la aplicacion $($AppId): $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
    exit 1
}

Write-Host "   Aplicacion encontrada: $($appData.primaryName)" -ForegroundColor Green

$submission = $null
if ($appData.pendingApplicationSubmission) {
    $pendingId = $appData.pendingApplicationSubmission.id
    Write-Host "   Submission en borrador encontrada (ID: $pendingId). Usando borrador existente..." -ForegroundColor Yellow
    $subUrl = "$apiBase/$AppId/submissions/$pendingId"
    $submission = Invoke-RestMethod -Uri $subUrl -Method Get -Headers $headers
} else {
    Write-Host "   Creando nuevo envio (submission)..." -ForegroundColor Cyan
    $createSubUrl = "$apiBase/$AppId/submissions"
    try {
        $submission = Invoke-RestMethod -Uri $createSubUrl -Method Post -Headers $headers -ContentType "application/json"
    } catch {
        Write-Host "Error creando la submission: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
        exit 1
    }
}

$submissionId = $submission.id
$fileUploadUrl = $submission.fileUploadUrl

if (-not $fileUploadUrl) {
    Write-Host "ERROR: No se recibio la URL de carga de archivos (fileUploadUrl)." -ForegroundColor Red
    exit 1
}

Write-Host "   Submission ID: $submissionId" -ForegroundColor Green

# ---------------------------------------------------------
# 5. Subir Paquete a Azure Blob Storage
# ---------------------------------------------------------
Write-Host "3. Subiendo paquete MSIX ($packageSizeMb MB) a Azure Blob Storage..." -ForegroundColor Cyan
$uploadHeaders = @{
    "x-ms-blob-type" = "BlockBlob"
}

try {
    $uploadStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $uploadResponse = Invoke-RestMethod -Uri $fileUploadUrl -Method Put -InFile $fullPackagePath -Headers $uploadHeaders -ContentType "application/octet-stream"
    $uploadStopwatch.Stop()
    $uploadSeconds = [math]::Round($uploadStopwatch.Elapsed.TotalSeconds, 1)
    Write-Host "   Carga completada exitosamente en $uploadSeconds segundos." -ForegroundColor Green
} catch {
    Write-Host "Error subiendo el paquete a Azure: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
    exit 1
}

# ---------------------------------------------------------
# 6. Actualizar Datos de la Submission con el Paquete
# ---------------------------------------------------------
Write-Host "4. Actualizando metadatos del paquete en la submission..." -ForegroundColor Cyan

# Preparar la definicion del paquete en la submission
$newPackage = @{
    fileName = $fileName
    fileStatus = "PendingUpload"
    minimumDirectXVersion = "None"
    minimumSystemRam = "None"
}

# Reemplazar la lista de paquetes con el nuevo paquete
$submission.applicationPackages = @($newPackage)

$updateSubUrl = "$apiBase/$AppId/submissions/$submissionId"
$jsonPayload = $submission | ConvertTo-Json -Depth 10

try {
    $updatedSubmission = Invoke-RestMethod -Uri $updateSubUrl -Method Put -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload)) -ContentType "application/json; charset=utf-8"
    Write-Host "   Metadatos actualizados correctamente." -ForegroundColor Green
} catch {
    Write-Host "Error actualizando la submission: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
    exit 1
}

# ---------------------------------------------------------
# 7. Enviar a Certificacion (Commit)
# ---------------------------------------------------------
if ($SkipCommit) {
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Yellow
    Write-Host "  El paquete fue cargado exitosamente en modo BORRADOR.   " -ForegroundColor Yellow
    Write-Host "  No se envio a certificacion (-SkipCommit especificado). " -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Yellow
    exit 0
}

Write-Host "5. Enviando a certificacion oficial (Commit)..." -ForegroundColor Cyan
$commitUrl = "$apiBase/$AppId/submissions/$submissionId/commit"

try {
    $commitResponse = Invoke-RestMethod -Uri $commitUrl -Method Post -Headers $headers -ContentType "application/json"
    Write-Host "   Solicitud de certificacion aceptada!" -ForegroundColor Green
} catch {
    Write-Host "Error al solicitar certificacion: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
    exit 1
}

# ---------------------------------------------------------
# 8. Comprobar Estado Inicial
# ---------------------------------------------------------
Start-Sleep -Seconds 3
try {
    $statusUrl = "$apiBase/$AppId/submissions/$submissionId/status"
    $statusResponse = Invoke-RestMethod -Uri $statusUrl -Method Get -Headers $headers
    $currentStatus = $statusResponse.status
} catch {
    $currentStatus = "CommitStarted"
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  ¡ENVIO v$packageVersion COMPLETADO EXITOSAMENTE!         " -ForegroundColor Green
Write-Host "  Submission ID: $submissionId                            " -ForegroundColor Green
Write-Host "  Estado actual: $currentStatus                           " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "El paquete ahora esta siendo procesado y certificado automaticamente por Microsoft."
Write-Host "Panel web: https://partner.microsoft.com/dashboard/products/$AppId/overview" -ForegroundColor Cyan
