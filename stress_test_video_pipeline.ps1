# Stress test para video rendering pipeline

param(
    [int]$FileCount = 100,
    [string]$OutputDir = "C:\p2p\stress_test_pdfs",
    [switch]$CleanupOnExit
)

if (Test-Path $OutputDir -PathType Container) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null | Out-Null

Write-Host "Generando $FileCount PDFs fake para stress test..."

for ($i = 1; $i -le $FileCount; $i++) {
    $pdfPath = Join-Path $OutputDir "test_score_$($i.ToString('D3')).pdf"
    
    $pdfContent = "%PDF-1.4`n"
    $pdfContent += "1 0 obj`n"
    $pdfContent += "<< /Type /Catalog /Pages 2 0 R >>`n"
    $pdfContent += "endobj`n"
    $pdfContent += "2 0 obj`n"
    $pdfContent += "<< /Type /Pages /Kids [3 0 R] /Count 1 >>`n"
    $pdfContent += "endobj`n"
    $pdfContent += "3 0 obj`n"
    $pdfContent += "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>`n"
    $pdfContent += "endobj`n"
    $pdfContent += "4 0 obj`n"
    $pdfContent += "<< >>`n"
    $pdfContent += "stream`n"
    $pdfContent += "BT /F1 12 Tf 100 700 Td (Test Score $i) Tj ET`n"
    $pdfContent += "endstream`n"
    $pdfContent += "endobj`n"
    $pdfContent += "xref`n0 5`n"
    $pdfContent += "0000000000 65535 f`n"
    $pdfContent += "0000000009 00000 n`n"
    $pdfContent += "0000000058 00000 n`n"
    $pdfContent += "0000000115 00000 n`n"
    $pdfContent += "0000000244 00000 n`n"
    $pdfContent += "trailer`n"
    $pdfContent += "<< /Size 5 /Root 1 0 R >>`n"
    $pdfContent += "startxref`n296`n%%EOF"
    
    Set-Content -Path $pdfPath -Value $pdfContent -Encoding ASCII -NoNewline
    
    if ($i % 10 -eq 0) {
        Write-Host "  Creados $i/$FileCount PDFs"
    }
}

Write-Host "OK: Generados $FileCount PDFs en $OutputDir"

$scoreDownExe = "C:\p2p\ScoreDown\bin\Debug\net9.0-windows\ScoreDown.exe"
if (-not (Test-Path $scoreDownExe)) {
    Write-Host "ERROR: No encontrado $scoreDownExe"
    exit 1
}

Write-Host ""
Write-Host "Iniciando stress test..."
Write-Host "  Esperado: LRU cap <= 5000 trazas"
Write-Host "  Esperado: Retry 2x en timeouts"
Write-Host "  Esperado: Backoff dinamico si mediana > 30s"
Write-Host ""

$startTime = Get-Date

$OutputDir | Set-Clipboard
Write-Host "Ruta PDFs copiada a clipboard: $OutputDir"
Write-Host "1. En ScoreDown: Seleccionar Carpeta"
Write-Host "2. Pega (Ctrl+V): $OutputDir"
Write-Host "3. Espera procesamiento..."
Write-Host ""

try {
    $proc = Start-Process -FilePath $scoreDownExe -PassThru
    Write-Host "ScoreDown iniciado (PID: $($proc.Id))"
    
    $processTimeout = 3600000
    if ($proc.WaitForExit($processTimeout)) {
        Write-Host "Test completado"
    } else {
        Write-Host "Timeout. Terminando..."
        Stop-Process -Id $proc.Id -Force
    }
}
catch {
    Write-Host "Error: $_"
    exit 1
}

$duration = ((Get-Date) - $startTime).TotalSeconds
Write-Host ""
Write-Host "Duracion: $([Math]::Round($duration, 1)) segundos"
Write-Host ""

$traceCandidates = @(
    "$env:LOCALAPPDATA\ScoreDown\video-render-trace-metrics.json",
    "$env:APPDATA\ScoreDown\video_render_trace.json"
)
$traceFile = $traceCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($traceFile) {
    Write-Host "Archivo trazas: $traceFile"

    try {
        $items = Get-Content $traceFile -Raw | ConvertFrom-Json
        $count = if ($items -is [System.Array]) { $items.Count } elseif ($items) { 1 } else { 0 }
        Write-Host "Total trazas persistidas: $count"

        $values = @()
        if ($items -is [System.Array]) {
            foreach ($it in $items) {
                if ($null -ne $it.Item2) { $values += [double]$it.Item2 }
                elseif ($null -ne $it.Ms) { $values += [double]$it.Ms }
                elseif ($null -ne $it.ElapsedMs) { $values += [double]$it.ElapsedMs }
            }
        }

        if ($values.Count -gt 0) {
            $sorted = $values | Sort-Object
            $avg = ($sorted | Measure-Object -Average).Average
            $median = $sorted[[int]($sorted.Count / 2)]
            $p95Index = [Math]::Min($sorted.Count - 1, [int]($sorted.Count * 0.95))
            $p95 = $sorted[$p95Index]

            Write-Host "Stats (ms):"
            Write-Host "  avg=$([Math]::Round($avg, 1))"
            Write-Host "  median=$([Math]::Round($median, 1))"
            Write-Host "  p95=$([Math]::Round($p95, 1))"
        }
    }
    catch {
        Write-Host "No se pudo parsear archivo de trazas: $_"
    }
}
else {
    Write-Host "No se encontro archivo de trazas persistidas en rutas esperadas."
    Write-Host "Asegura ejecutar generacion real de video antes de cerrar ScoreDown."
}

if ($CleanupOnExit -and (Test-Path $OutputDir -PathType Container)) {
    Remove-Item $OutputDir -Recurse -Force
    Write-Host "Limpieza: carpeta de PDFs de prueba eliminada."
}

Write-Host "Stress test completado"
