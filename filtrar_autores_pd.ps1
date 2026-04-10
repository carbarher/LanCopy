# Script para filtrar autores no deseados (blacklist) de la base PD
# Fusiona autores_pd_fusionados.txt y elimina los que estén en autores_pd_blacklist.txt
# Resultado: authors_unpurged.txt actualizado y limpio

$baseFusion = 'autores_pd_fusionados.txt'
$blacklist = 'autores_pd_blacklist.txt'
$out = 'authors_unpurged.txt'

Write-Host "Filtrando autores con blacklist..."

$excluir = @{}
if (Test-Path $blacklist) {
    Get-Content $blacklist | ForEach-Object {
        $a = $_.Trim()
        if ($a) { $excluir[$a] = $true }
    }
}

$final = @()
if (Test-Path $baseFusion) {
    Get-Content $baseFusion | ForEach-Object {
        $a = $_.Trim()
        if ($a -and -not $excluir.ContainsKey($a)) {
            $final += $a
        }
    }
}

$final | Sort-Object | Set-Content $out -Encoding UTF8

Write-Host "Autores PD filtrados y guardados en $out. Total: $($final.Count)"
