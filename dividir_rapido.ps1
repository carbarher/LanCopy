$lines = Get-Content "autores_sf_2500.txt"
$total = $lines.Count

Write-Host "Total lineas: $total"

$lines[0..499] | Set-Content "autores_sf_2500_1.txt"
Write-Host "Creado: autores_sf_2500_1.txt (500 lineas)"

$lines[500..999] | Set-Content "autores_sf_2500_2.txt"
Write-Host "Creado: autores_sf_2500_2.txt (500 lineas)"

$lines[1000..1499] | Set-Content "autores_sf_2500_3.txt"
Write-Host "Creado: autores_sf_2500_3.txt (500 lineas)"

$lines[1500..($total-1)] | Set-Content "autores_sf_2500_4.txt"
Write-Host "Creado: autores_sf_2500_4.txt ($($total-1500) lineas)"

Write-Host ""
Get-ChildItem "autores_sf_2500_*.txt" | ForEach-Object {
    $count = (Get-Content $_.FullName).Count
    Write-Host "$($_.Name): $count lineas"
}
