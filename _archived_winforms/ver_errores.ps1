# Script para ver errores específicos
$output = dotnet build SlskDown.csproj -c Release 2>&1
$errors = $output | Where-Object { $_ -match "error CS" }

Write-Host "Total de errores: $($errors.Count)" -ForegroundColor Red
Write-Host ""
Write-Host "Primeros 10 errores:" -ForegroundColor Yellow
$errors | Select-Object -First 10 | ForEach-Object { Write-Host $_ -ForegroundColor Cyan }

# Ver qué archivos causan más errores
$files = @()
$errors | ForEach-Object {
    if ($_ -match "([a-zA-Z0-9_]+\.cs)\(") {
        $files += $matches[1]
    }
}

$fileCounts = $files | Group-Object | Sort-Object Count -Descending
Write-Host ""
Write-Host "Archivos con más errores:" -ForegroundColor Yellow
$fileCounts | Select-Object -First 5 | ForEach-Object { 
    Write-Host "$($_.Name): $($_.Count) errores" -ForegroundColor Magenta 
}
