# Script para eliminar TODOS los emojis de archivos .cs
# Usa regex para detectar rangos Unicode de emojis

$projectPath = "c:\p2p\SlskDown"
$filesProcessed = 0
$totalCharsRemoved = 0

# Función para eliminar emojis de un texto
function Remove-Emojis {
    param([string]$text)
    
    # Eliminar emojis usando rangos Unicode
    # Esto cubre la mayoría de emojis comunes
    $cleaned = $text -replace '[\u{1F300}-\u{1F9FF}]', ''  # Símbolos y pictogramas
    $cleaned = $cleaned -replace '[\u{2600}-\u{26FF}]', ''  # Símbolos misceláneos
    $cleaned = $cleaned -replace '[\u{2700}-\u{27BF}]', ''  # Dingbats
    $cleaned = $cleaned -replace '[\u{1F600}-\u{1F64F}]', '' # Emoticones
    $cleaned = $cleaned -replace '[\u{1F680}-\u{1F6FF}]', '' # Transporte y símbolos de mapa
    $cleaned = $cleaned -replace '[\u{1F1E0}-\u{1F1FF}]', '' # Banderas
    $cleaned = $cleaned -replace '[\u{FE0F}]', ''            # Selector de variación
    $cleaned = $cleaned -replace '[\u{200D}]', ''            # Zero Width Joiner
    
    return $cleaned
}

Write-Host "Procesando archivos en: $projectPath"
Write-Host ""

Get-ChildItem -Path $projectPath -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    
    try {
        # Leer contenido con UTF-8
        $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        $originalLength = $content.Length
        
        # Eliminar emojis
        $newContent = Remove-Emojis -text $content
        
        $charsRemoved = $originalLength - $newContent.Length
        
        if ($charsRemoved -gt 0) {
            # Guardar archivo modificado
            [System.IO.File]::WriteAllText($file.FullName, $newContent, [System.Text.UTF8Encoding]::new($false))
            
            Write-Host "✓ $($file.Name) - $charsRemoved caracteres eliminados" -ForegroundColor Green
            $filesProcessed++
            $totalCharsRemoved += $charsRemoved
        }
    }
    catch {
        Write-Host "✗ Error procesando $($file.Name): $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=========================================="
Write-Host "RESUMEN FINAL"
Write-Host "=========================================="
Write-Host "Archivos modificados: $filesProcessed"
Write-Host "Total caracteres eliminados: $totalCharsRemoved"
Write-Host "=========================================="
