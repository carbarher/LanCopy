# Script para corregir bloques de código comentados incorrectamente
# Patrón: // ERROR: funcion( seguido de líneas sin comentar

$ErrorActionPreference = "Stop"

$files = @(
    "Core\SearchManager.cs",
    "MainForm.cs",
    "Database\SlskDatabase.cs"
)

foreach ($file in $files) {
    $path = "c:\p2p\SlskDown\$file"
    if (-not (Test-Path $path)) {
        Write-Host "⚠️ Archivo no encontrado: $path" -ForegroundColor Yellow
        continue
    }
    
    Write-Host "📝 Procesando: $file" -ForegroundColor Cyan
    
    $content = Get-Content $path -Raw
    $lines = Get-Content $path
    $newLines = @()
    $inCommentedBlock = $false
    $blockDepth = 0
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Detectar inicio de bloque comentado incorrectamente
        if ($line -match '^\s*//\s*ERROR:\s*\w+\(') {
            $inCommentedBlock = $true
            $blockDepth = 0
            $newLines += $line
            continue
        }
        
        # Si estamos en un bloque comentado, comentar todas las líneas
        if ($inCommentedBlock) {
            # Contar llaves para saber cuándo termina el bloque
            $openBraces = ($line -split '\{').Count - 1
            $closeBraces = ($line -split '\}').Count - 1
            $blockDepth += $openBraces - $closeBraces
            
            # Si la línea no está comentada, comentarla
            if ($line -notmatch '^\s*//' -and $line.Trim() -ne '') {
                $indent = $line -replace '^(\s*).*', '$1'
                $newLines += "$indent// $($line.TrimStart())"
            } else {
                $newLines += $line
            }
            
            # Si cerramos todas las llaves, salir del bloque
            if ($blockDepth -le 0 -and $line -match '\}') {
                $inCommentedBlock = $false
            }
        } else {
            $newLines += $line
        }
    }
    
    # Guardar archivo
    $backup = "$path.backup_fix"
    Copy-Item $path $backup -Force
    $newLines | Set-Content $path -Encoding UTF8
    
    Write-Host "✅ Corregido: $file (backup: $backup)" -ForegroundColor Green
}

Write-Host "`n🎉 Proceso completado" -ForegroundColor Green
