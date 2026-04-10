# Fusiona autores_gutenberg.txt con tu base actual de autores PD (ajusta el nombre si es necesario)
# Cambia $baseActual si tu archivo de autores PD tiene otro nombre

$baseActual = 'authors_unpurged.txt'
$nuevos = 'autores_gutenberg.txt'
$fusion = 'autores_pd_fusionados.txt'

Write-Host "Fusionando $baseActual y $nuevos ..."

$all = @{}

foreach ($file in @($baseActual, $nuevos)) {
    if (Test-Path $file) {
        Get-Content $file | ForEach-Object {
            $a = $_.Trim()
            if ($a -and -not $all.ContainsKey($a)) {
                $all[$a] = $true
            }
        }
    }
}

$all.Keys | Sort-Object | Set-Content $fusion -Encoding UTF8

Write-Host "Fusión completa. Archivo generado: $fusion. Total autores: $($all.Count)"
