# Dividir archivo de autores en chunks de 500 líneas

$inputFile = "autores_sf_2500.txt"
$chunkSize = 500

Write-Host "Leyendo archivo $inputFile..."
$lines = Get-Content $inputFile -Encoding UTF8

$totalLines = $lines.Count
$numChunks = [Math]::Ceiling($totalLines / $chunkSize)

Write-Host "Total líneas: $totalLines"
Write-Host "Dividiendo en $numChunks archivos de $chunkSize líneas cada uno..."
Write-Host ""

for ($i = 0; $i -lt $numChunks; $i++) {
    $start = $i * $chunkSize
    $end = [Math]::Min($start + $chunkSize, $totalLines)
    $chunkLines = $lines[$start..($end-1)]
    
    $outputFile = "autores_sf_2500_$($i+1).txt"
    $chunkLines | Out-File -FilePath $outputFile -Encoding UTF8
    
    $lineCount = $chunkLines.Count
    Write-Host "✅ $outputFile : $lineCount líneas (líneas $($start+1)-$end)"
}

Write-Host ""
Write-Host "✅ División completa: $numChunks archivos creados"
