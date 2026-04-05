#Requires -Version 5.1
<#
.SYNOPSIS
  Prueba rápida de conectividad a BDH (BNE), Manybooks y Feedbooks (fuentes PD mencionadas).
.DESCRIPTION
  No forma parte del build de la app. Uso:
    pwsh -NoProfile -File scripts/probe-pd-sources.ps1
    pwsh -NoProfile -File scripts/probe-pd-sources.ps1 -OpenBrowser
  Muchas webs usan Cloudflare: desde PowerShell puede devolver 403 aunque el navegador funcione.
  BDH: la búsqueda nueva es una SPA; la respuesta HTML suele ser el caparazón; el legado Search.do a veces responde 503.
#>
param(
    [switch] $OpenBrowser,
    [int] $TimeoutSec = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ChromeUa = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

function Invoke-UrlProbe {
    param(
        [string] $Name,
        [string] $Uri,
        [hashtable] $ExtraHeaders = @{}
    )
    $h = @{
        'User-Agent'                = $ChromeUa
        'Accept'                    = 'text/html,application/xhtml+xml;q=0.9,*/*;q=0.8'
        'Accept-Language'           = 'es-ES,es;q=0.9,en;q=0.8'
        'Cache-Control'             = 'no-cache'
        'Upgrade-Insecure-Requests' = '1'
    }
    foreach ($k in $ExtraHeaders.Keys) { $h[$k] = $ExtraHeaders[$k] }

    try {
        $resp = Invoke-WebRequest -Uri $Uri -Method Get -UseBasicParsing -TimeoutSec $TimeoutSec -MaximumRedirection 5 -Headers $h
        $finalUri = $Uri
        try {
            $br = $resp.BaseResponse
            if ($null -ne $br) {
                if ($br.PSObject.Properties['ResponseUri'] -and $null -ne $br.ResponseUri) {
                    $finalUri = $br.ResponseUri.AbsoluteUri
                }
                elseif ($br.PSObject.Properties['RequestMessage'] -and $null -ne $br.RequestMessage -and $br.RequestMessage.RequestUri) {
                    $finalUri = $br.RequestMessage.RequestUri.AbsoluteUri
                }
            }
        }
        catch { }
        [pscustomobject]@{
            Fuente     = $Name
            Estado     = [int]$resp.StatusCode
            Bytes      = if ($null -ne $resp.Content) { $resp.Content.Length } else { 0 }
            FinalUri   = $finalUri
            Error      = ''
            Sugerencia = ''
        }
    }
    catch {
        $code = $null
        $ex = $_.Exception
        try {
            if ($ex -is [System.Net.WebException] -and $null -ne $ex.Response) {
                $code = [int]$ex.Response.StatusCode
            }
            elseif ($ex.PSObject.Properties['StatusCode'] -and $null -ne $ex.StatusCode) {
                $code = [int]$ex.StatusCode
            }
        }
        catch { }
        $msg = $ex.Message
        $hint = ''
        if ($msg -match '403|Forbidden') {
            $hint = 'Posible bloqueo Cloudflare/bot; prueba en navegador o otra red.'
        }
        elseif ($msg -match '503') {
            $hint = 'Servidor temporalmente no disponible; reintenta más tarde.'
        }
        [pscustomobject]@{
            Fuente     = $Name
            Estado     = $code
            Bytes      = 0
            FinalUri   = $Uri
            Error      = $msg
            Sugerencia = $hint
        }
    }
}

$probes = @(
    @{ Name = 'BDH BNE busqueda (SPA)'; Uri = 'https://bdh.bne.es/bd/es/search?search=don%20quijote' }
    @{ Name = 'BDH BNE avanzada';       Uri = 'https://bdh.bne.es/bd/es/advanced' }
    @{ Name = 'BDH BNE legado Search.do'; Uri = 'https://bdh.bne.es/bnesearch/Search.do?text=quijote&pageNumber=1&pageSize=5' }
    @{ Name = 'Manybooks inicio';      Uri = 'https://manybooks.net/' }
    @{ Name = 'Manybooks categorias';  Uri = 'https://manybooks.net/categories' }
    @{ Name = 'Feedbooks catalogo';    Uri = 'https://www.feedbooks.com/catalog' }
    @{ Name = 'Feedbooks PD bookshelf'; Uri = 'https://www.feedbooks.com/bookshelf/category/public-domain' }
)

Write-Host "=== Prueba de fuentes PD (BDH / Manybooks / Feedbooks) ===" -ForegroundColor Cyan
Write-Host ""

$rows = foreach ($p in $probes) {
    Invoke-UrlProbe -Name $p.Name -Uri $p.Uri
    Start-Sleep -Milliseconds 350
}

$rows | Format-Table -AutoSize Fuente, Estado, Bytes, FinalUri

$errRows = $rows | Where-Object { $_.Error -ne '' }
if ($errRows) {
    Write-Host "--- Detalle errores ---" -ForegroundColor Yellow
    $errRows | Format-List Fuente, Estado, Error, Sugerencia
}

Write-Host ""
Write-Host "Notas:" -ForegroundColor DarkGray
Write-Host "  - BDH: resultados reales suelen cargarse por JS/API en el cliente; 200 con HTML corto es normal en la SPA." -ForegroundColor DarkGray
Write-Host "  - Manybooks/Feedbooks: 403 desde scripts es frecuente; validar en Chrome/Firefox." -ForegroundColor DarkGray

if ($OpenBrowser) {
    $urls = @(
        'https://bdh.bne.es/bd/es/search?search=don%20quijote',
        'https://manybooks.net/',
        'https://www.feedbooks.com/bookshelf/category/public-domain'
    )
    Write-Host ""
    Write-Host "Abriendo URLs en el navegador predeterminado..." -ForegroundColor Cyan
    foreach ($u in $urls) {
        Start-Process $u
        Start-Sleep -Milliseconds 400
    }
}
