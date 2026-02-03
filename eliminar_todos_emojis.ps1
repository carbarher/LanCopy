# Script definitivo para eliminar TODOS los emojis
$projectPath = "c:\p2p\SlskDown"
$filesProcessed = 0
$totalCharsRemoved = 0

Write-Host "Iniciando eliminacion de emojis..." -ForegroundColor Cyan
Write-Host "Ruta: $projectPath" -ForegroundColor Cyan
Write-Host ""

$files = Get-ChildItem -Path $projectPath -Filter "*.cs" -Recurse -File -ErrorAction SilentlyContinue

Write-Host "Total archivos .cs encontrados: $($files.Count)" -ForegroundColor Green
Write-Host ""

foreach ($file in $files) {
    try {
        $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        $originalLength = $content.Length
        
        # Eliminar TODOS los emojis usando regex Unicode
        $content = $content -replace '[\u{1F300}-\u{1F9FF}]', ''  # Emojis misceláneos
        $content = $content -replace '[\u{2600}-\u{26FF}]', ''    # Símbolos misceláneos
        $content = $content -replace '[\u{2700}-\u{27BF}]', ''    # Dingbats
        $content = $content -replace '[\u{1F600}-\u{1F64F}]', ''  # Emoticones
        $content = $content -replace '[\u{1F680}-\u{1F6FF}]', ''  # Transporte
        $content = $content -replace '[\u{1F1E0}-\u{1F1FF}]', ''  # Banderas
        $content = $content -replace '[\u{FE0F}]', ''             # Selector variación
        $content = $content -replace '[\u{200D}]', ''             # Zero Width Joiner
        
        $charsRemoved = $originalLength - $content.Length
        
        if ($charsRemoved -gt 0) {
            [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]::new($false))
            Write-Host "$($file.Name): $charsRemoved caracteres eliminados" -ForegroundColor Green
            $filesProcessed++
            $totalCharsRemoved += $charsRemoved
        }
    }
    catch {
        Write-Host "ERROR en $($file.Name): $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=" * 60 -ForegroundColor Yellow
Write-Host "RESUMEN FINAL" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Yellow
Write-Host "Archivos modificados: $filesProcessed" -ForegroundColor Green
Write-Host "Total caracteres eliminados: $totalCharsRemoved" -ForegroundColor Green
Write-Host "=" * 60 -ForegroundColor Yellow

# Guardar resultado en archivo
$resultado = @"
Eliminacion de emojis completada
Fecha: $(Get-Date)
Archivos procesados: $filesProcessed
Caracteres eliminados: $totalCharsRemoved
"@

$resultado | Out-File "c:\p2p\resultado_emojis.txt" -Encoding UTF8
Write-Host ""
Write-Host "Resultado guardado en: c:\p2p\resultado_emojis.txt" -ForegroundColor Cyan
