# Script mejorado para eliminar emojis de archivos C#
# Version 3 - Mejorado con mejor manejo de encoding y regex

param(
    [string]$Path = "c:\p2p\SlskDown"
)

Write-Host "=== Eliminador de Emojis v3 ===" -ForegroundColor Cyan
Write-Host "Procesando archivos en: $Path" -ForegroundColor Yellow
Write-Host ""

# Obtener todos los archivos .cs
$files = Get-ChildItem -Path $Path -Filter "*.cs" -Recurse -File

$totalFiles = $files.Count
$processedFiles = 0
$totalEmojisRemoved = 0

foreach ($file in $files) {
    $processedFiles++
    Write-Host "[$processedFiles/$totalFiles] Procesando: $($file.Name)" -ForegroundColor Gray
    
    try {
        # Leer contenido con UTF-8
        $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        $originalLength = $content.Length
        
        # Contar emojis antes
        $emojisBefore = ([regex]::Matches($content, '[\u{1F300}-\u{1F9FF}\u{2600}-\u{27BF}\u{2B50}\u{2705}\u{274C}\u{26A0}\u{2139}\u{23F8}-\u{23FA}\u{25B6}\u{23ED}-\u{23EF}\u{1F004}\u{1F0CF}\u{1F170}-\u{1F251}]')).Count
        
        if ($emojisBefore -eq 0) {
            Write-Host "  Sin emojis" -ForegroundColor DarkGray
            continue
        }
        
        # Eliminar emojis - Rango completo de Unicode
        # Incluye: Emoticons, Symbols, Pictographs, Transport, Flags, etc.
        $newContent = $content -replace '[\u{1F300}-\u{1F9FF}]', ''  # Emoticons & Pictographs
        $newContent = $newContent -replace '[\u{2600}-\u{27BF}]', ''  # Miscellaneous Symbols
        $newContent = $newContent -replace '[\u{2B50}]', ''           # Star
        $newContent = $newContent -replace '[\u{2705}]', ''           # Check mark
        $newContent = $newContent -replace '[\u{274C}]', ''           # Cross mark
        $newContent = $newContent -replace '[\u{26A0}]', ''           # Warning
        $newContent = $newContent -replace '[\u{2139}]', ''           # Information
        $newContent = $newContent -replace '[\u{23F8}-\u{23FA}]', ''  # Pause/Stop/Record
        $newContent = $newContent -replace '[\u{25B6}]', ''           # Play
        $newContent = $newContent -replace '[\u{23ED}-\u{23EF}]', ''  # Skip/Fast forward
        $newContent = $newContent -replace '[\u{1F004}]', ''          # Mahjong
        $newContent = $newContent -replace '[\u{1F0CF}]', ''          # Playing card
        $newContent = $newContent -replace '[\u{1F170}-\u{1F251}]', '' # Enclosed characters
        $newContent = $newContent -replace '[\u{FE0F}]', ''           # Variation Selector (emoji presentation)
        
        # Contar emojis después
        $emojisAfter = ([regex]::Matches($newContent, '[\u{1F300}-\u{1F9FF}\u{2600}-\u{27BF}\u{2B50}\u{2705}\u{274C}\u{26A0}\u{2139}\u{23F8}-\u{23FA}\u{25B6}\u{23ED}-\u{23EF}\u{1F004}\u{1F0CF}\u{1F170}-\u{1F251}]')).Count
        $emojisRemoved = $emojisBefore - $emojisAfter
        
        if ($emojisRemoved -gt 0) {
            # Guardar con UTF-8 sin BOM
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($file.FullName, $newContent, $utf8NoBom)
            
            $totalEmojisRemoved += $emojisRemoved
            Write-Host "  Eliminados: $emojisRemoved emojis" -ForegroundColor Green
        } else {
            Write-Host "  Sin cambios" -ForegroundColor DarkGray
        }
    }
    catch {
        Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== RESUMEN ===" -ForegroundColor Cyan
Write-Host "Archivos procesados: $processedFiles" -ForegroundColor White
Write-Host "Total emojis eliminados: $totalEmojisRemoved" -ForegroundColor Green
Write-Host ""
Write-Host "Completado!" -ForegroundColor Green
