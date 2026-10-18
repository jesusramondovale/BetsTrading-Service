<#
.SYNOPSIS
  Para BetsTrading.API, deja la web Flutter dentro del publish y vuelve a arrancar API + web.

.PARAMETER Web
  Recompila Flutter (scripts/publish_web.ps1 del cliente) antes de publicar.

.PARAMETER Publicar
  Regenera el exe autocontenido en bin\Release\net8.0\publish.
  Sin este switch solo copia la web ya compilada junto al exe y lo relanza.

.PARAMETER SinArrancar
  No inicia el proceso ni abre el navegador.

.EXAMPLE
  .\Relanzar.bat
  .\Relanzar.bat web
  .\Relanzar.bat publicar
#>
$accion = @($args | ForEach-Object { $_.Trim().TrimStart("-").ToLowerInvariant() })
$Web = $accion -contains "web"
$Publicar = $accion -contains "publicar"
$SinArrancar = $accion -contains "sinarrancar"

$ErrorActionPreference = "Stop"

$ServiceRoot = Split-Path -Parent $PSScriptRoot
$ClientRoot = "C:\dev\BetsTrading"
$PublishDir = Join-Path $ServiceRoot "BetsTrading.API\bin\Release\net8.0\publish"
$Exe = Join-Path $PublishDir "BetsTrading.API.exe"
$WebSrc = Join-Path $ServiceRoot "BetsTrading.API\wwwroot\betrader"
$WebDest = Join-Path $PublishDir "wwwroot\betrader"

function Stop-Api {
    $procs = Get-Process -Name "BetsTrading.API" -ErrorAction SilentlyContinue
    if (-not $procs) {
        Write-Host "API no estaba en marcha."
        return
    }
    Write-Host "Deteniendo BetsTrading.API..."
    Stop-Process -Name "BetsTrading.API" -Force
    Start-Sleep -Seconds 1
}

function Copy-WebIntoPublish {
    if (-not (Test-Path (Join-Path $WebSrc "index.html"))) {
        throw "No hay web en $WebSrc. Ejecuta: Relanzar.bat web"
    }
    Write-Host "Copiando web a $WebDest"
    if (Test-Path $WebDest) {
        Remove-Item -Recurse -Force $WebDest
    }
    New-Item -ItemType Directory -Path $WebDest -Force | Out-Null
    Copy-Item -Path (Join-Path $WebSrc "*") -Destination $WebDest -Recurse -Force
}

Stop-Api

if ($Web) {
    $publishWeb = Join-Path $ClientRoot "scripts\publish_web.ps1"
    if (-not (Test-Path $publishWeb)) {
        throw "No se encontro $publishWeb"
    }
    Write-Host "Compilando Flutter web..."
    & $publishWeb -ServiceRoot $ServiceRoot
    if ($LASTEXITCODE -ne 0) {
        throw "publish_web.ps1 fallo con codigo $LASTEXITCODE"
    }
}

if ($Publicar -or $Web) {
    Write-Host "Publicando API (win-x64, autocontenida) en $PublishDir"
    dotnet publish (Join-Path $ServiceRoot "BetsTrading.API\BetsTrading.API.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $PublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish fallo con codigo $LASTEXITCODE"
    }
}

if (-not (Test-Path $Exe)) {
    throw "No existe $Exe. Ejecuta: Relanzar.bat publicar"
}

if (-not (Test-Path (Join-Path $WebDest "index.html"))) {
    Copy-WebIntoPublish
}

if ($SinArrancar) {
    Write-Host "Listo. Exe: $Exe"
    Write-Host "Web:  $WebDest"
    exit 0
}

if (-not $env:ASPNETCORE_URLS) {
    $env:ASPNETCORE_URLS = "http://localhost:5000"
}

$baseUrl = ($env:ASPNETCORE_URLS -split ";")[0].Trim().TrimEnd("/")
$webUrl = "$baseUrl/betrader/"

Write-Host "Arrancando $Exe"
Write-Host "API: $baseUrl"
Write-Host "Web: $webUrl"
Start-Process -FilePath $Exe -WorkingDirectory $PublishDir

$ok = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $resp = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing -TimeoutSec 2
        if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 500) {
            $ok = $true
            break
        }
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}

Start-Process $webUrl
if ($ok) {
    Write-Host "En marcha. Navegador abierto en $webUrl"
}
else {
    Write-Host "Proceso lanzado. El health check aun no respondio; abre $webUrl"
}
