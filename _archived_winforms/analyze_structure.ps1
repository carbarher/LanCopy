# Analizar la estructura del código alrededor del error
$filePath = "MainForm.cs"
$lines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)

Write-Host "==================================="
Write-Host "ANALISIS DE ESTRUCTURA"
Write-Host "==================================="
Write-Host ""

# Mostrar contexto amplio
Write-Host "Contexto amplio (20295-20315):"
for ($i = 20294; $i -le 20314; $i++) {
    if ($i -lt $lines.Count) {
        $line = $lines[$i]
        $spaces = ($line -replace '^(\s*).*','$1').Length
        Write-Host "  $($i+1) [espacios:$spaces]: $line"
    }
}
Write-Host ""
Write-Host "==================================="
