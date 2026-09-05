[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13
$ErrorActionPreference = "Stop"

Write-Host "Obteniendo credenciales de Git..."
$gcmPath = "C:\Program Files\Git\mingw64\bin\git-credential-manager.exe"
if (-not (Test-Path $gcmPath)) {
    $gcmCmd = Get-Command git-credential-manager -ErrorAction SilentlyContinue
    if ($gcmCmd) { $gcmPath = $gcmCmd.Source }
}

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $gcmPath
$psi.Arguments = "get"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.CreateNoWindow = $true
$proc = [System.Diagnostics.Process]::Start($psi)
$proc.StandardInput.Write("protocol=https`nhost=github.com`n`n")
$proc.StandardInput.Flush()
$proc.StandardInput.Close()
$rawCreds = $proc.StandardOutput.ReadToEnd()
$proc.WaitForExit()

$token = $null
foreach ($line in ($rawCreds -split "`r?`n")) {
    if ($line.StartsWith("password=")) {
        $token = $line.Substring(9)
        break
    }
}

if (-not $token) {
    Write-Host "ERROR: No se encontro token en Git Credential Manager."
    exit 1
}

Write-Host "Token obtenido correctamente."

$owner = "Eliather"
$repo = "DockBar"
$tag = "v1.8.2"
$releaseName = "DockBar v1.8.2"
$releaseNotes = [System.IO.File]::ReadAllText((Resolve-Path "release_notes.md"), [System.Text.Encoding]::UTF8)

Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$client.DefaultRequestHeaders.UserAgent.ParseAdd("DockBar-Release-Script")
$client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $token)
$client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json")

# 1. Comprobar si el release ya existe
$existingRelease = $null
try {
    $response = $client.GetAsync("https://api.github.com/repos/$owner/$repo/releases/tags/$tag").GetAwaiter().GetResult()
    if ($response.IsSuccessStatusCode) {
        $jsonStr = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $existingRelease = $jsonStr | ConvertFrom-Json
    }
} catch {
}

$release = $null
if ($existingRelease -and $existingRelease.id) {
    Write-Host "Release $tag existente encontrado (ID: $($existingRelease.id)). Actualizando..."
    $payload = @{
        name = $releaseName
        body = $releaseNotes
        draft = $false
        prerelease = $false
    } | ConvertTo-Json

    $content = New-Object System.Net.Http.StringContent($payload, [System.Text.Encoding]::UTF8, "application/json")
    $req = New-Object System.Net.Http.HttpRequestMessage
    $req.Method = New-Object System.Net.Http.HttpMethod "PATCH"
    $req.RequestUri = New-Object System.Uri "https://api.github.com/repos/$owner/$repo/releases/$($existingRelease.id)"
    $req.Content = $content
    $res = $client.SendAsync($req).GetAwaiter().GetResult()
    $resContent = $res.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $res.IsSuccessStatusCode) {
        Write-Host "Error actualizando release: $resContent"
        exit 1
    }
    $release = $resContent | ConvertFrom-Json
} else {
    Write-Host "Creando nuevo release $tag en GitHub..."
    $payload = @{
        tag_name = $tag
        name = $releaseName
        body = $releaseNotes
        draft = $false
        prerelease = $false
    } | ConvertTo-Json

    $content = New-Object System.Net.Http.StringContent($payload, [System.Text.Encoding]::UTF8, "application/json")
    $res = $client.PostAsync("https://api.github.com/repos/$owner/$repo/releases", $content).GetAwaiter().GetResult()
    $resContent = $res.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $res.IsSuccessStatusCode) {
        Write-Host "Error creando release: $resContent"
        exit 1
    }
    $release = $resContent | ConvertFrom-Json
}

Write-Host "Release creado/actualizado exitosamente!"
Write-Host "Release ID: $($release.id)"
Write-Host "Release URL: $($release.html_url)"

# 2. Subir assets
$assetsToUpload = @(
    "DockBarSetup.exe",
    "DockBar-win-x64-v1.8.2.zip",
    "DockBar.msix"
)

$uploadUrlTemplate = $release.upload_url -replace '\{\?name,label\}', ''

# Obtener lista actual de assets
$relCheck = $client.GetAsync("https://api.github.com/repos/$owner/$repo/releases/$($release.id)").GetAwaiter().GetResult()
$relObj = ($relCheck.Content.ReadAsStringAsync().GetAwaiter().GetResult()) | ConvertFrom-Json

foreach ($fileName in $assetsToUpload) {
    if (-not (Test-Path $fileName)) {
        Write-Host "Archivo $fileName no encontrado. Saltando..."
        continue
    }

    # Eliminar asset anterior si existe
    if ($relObj.assets) {
        $existingAsset = $relObj.assets | Where-Object { $_.name -eq $fileName }
        if ($existingAsset) {
            Write-Host "Eliminando asset existente $($existingAsset.name)..."
            $delRes = $client.DeleteAsync("https://api.github.com/repos/$owner/$repo/releases/assets/$($existingAsset.id)").GetAwaiter().GetResult()
        }
    }

    Write-Host "Subiendo $fileName..."
    $fileBytes = [System.IO.File]::ReadAllBytes((Resolve-Path $fileName))
    $byteContent = New-Object System.Net.Http.ByteArrayContent($fileBytes, 0, $fileBytes.Length)
    $byteContent.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream")

    $uploadUri = "$uploadUrlTemplate`?name=$fileName"
    $upRes = $client.PostAsync($uploadUri, $byteContent).GetAwaiter().GetResult()
    $upContent = $upRes.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    if ($upRes.IsSuccessStatusCode) {
        $upObj = $upContent | ConvertFrom-Json
        Write-Host "Asset $fileName subido con exito ($($upObj.size) bytes)."
    } else {
        Write-Host "Error subiendo asset ${fileName}: $upContent"
    }
}

Write-Host ""
Write-Host "=========================================================="
Write-Host "  Release v1.8.2 publicado COMPLETAMENTE en GitHub!"
Write-Host "  URL: $($release.html_url)"
Write-Host "=========================================================="
