# Script mejorado para arreglar TODOS los bloques comentados incorrectamente
# Maneja multiples patrones de error

param(
    [string]$FilePath = "c:\p2p\SlskDown\MainForm.cs",
    [switch]$DryRun = $false
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "ARREGLO AUTOMATICO DE BLOQUES COMENTADOS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if (-not (Test-Path $FilePath)) {
    Write-Host "ERROR: No se encuentra $FilePath" -ForegroundColor Red
    exit 1
}

# Crear backup
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupPath = "$FilePath.backup_$timestamp"
Copy-Item $FilePath $backupPath -Force
Write-Host "[OK] Backup creado: $backupPath`n" -ForegroundColor Green

# Leer contenido
$content = Get-Content $FilePath -Raw -Encoding UTF8

# Contador de cambios
$changes = 0

# Patrón 1: // ERROR: codigo_en_misma_linea
# Ejemplo: // ERROR: if (condition)
$pattern1 = '(?m)^(\s*)//\s*ERROR:\s*(.+)$'
$replacement1 = '$1$2'
$newContent = $content -replace $pattern1, $replacement1
if ($newContent -ne $content) {
    $count = ([regex]::Matches($content, $pattern1)).Count
    $changes += $count
    Write-Host "[OK] Patron 1 arreglado: $count bloques (// ERROR: en misma linea)" -ForegroundColor Green
    $content = $newContent
}

# Patrón 2: // ERROR: funcion(
#              parametros
#           );
# Buscar y arreglar bloques multi-línea
$lines = $content -split "`r?`n"
$fixedLines = New-Object System.Collections.ArrayList
$i = 0
$multiLineBlocks = 0

while ($i -lt $lines.Count) {
    $line = $lines[$i]
    
    # Detectar inicio de bloque multi-línea
    if ($line -match '^(\s*)//\s*ERROR:\s*(.+)\($') {
        $indent = $matches[1]
        $funcStart = $matches[2]
        
        # Descomentar primera línea
        $fixedLine = "$indent$funcStart("
        [void]$fixedLines.Add($fixedLine)
        
        $i++
        $blockLines = 1
        
        # Procesar líneas siguientes hasta encontrar el cierre
        while ($i -lt $lines.Count) {
            $nextLine = $lines[$i]
            
            # Si encuentra el cierre del bloque
            if ($nextLine -match '^\s*\);?\s*$') {
                [void]$fixedLines.Add($nextLine)
                $blockLines++
                $multiLineBlocks++
                Write-Host "[OK] Bloque multi-linea arreglado ($blockLines lineas) en linea $($i - $blockLines + 2)" -ForegroundColor Yellow
                $i++
                break
            }
            # Si encuentra una línea que no está indentada, terminar
            elseif ($nextLine -match '^[^\s]' -or $nextLine -match '^\s{0,3}[^\s]') {
                # No es parte del bloque, retroceder
                break
            }
            else {
                # Parte del bloque, mantener
                [void]$fixedLines.Add($nextLine)
                $blockLines++
                $i++
            }
        }
        
        continue
    }
    
    # Línea normal
    [void]$fixedLines.Add($line)
    $i++
}

if ($multiLineBlocks -gt 0) {
    $content = $fixedLines -join "`r`n"
    $changes += $multiLineBlocks
}

# Patrón 3: Líneas sueltas después de // ERROR: que quedaron sin comentar
# Buscar patrones como:
#     .OrderByDescending(...)
#     .ThenBy(...)
# que siguen a un // ERROR:
$pattern3 = '(?m)^(\s*)//\s*ERROR:[^\r\n]*\r?\n((?:\s+\.[A-Z][^\r\n]*\r?\n)+)'
if ($content -match $pattern3) {
    Write-Host "[!] Advertencia: Detectadas lineas encadenadas despues de // ERROR:" -ForegroundColor Yellow
    Write-Host "    Estas requieren revision manual" -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "RESUMEN" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total de cambios realizados: $changes" -ForegroundColor $(if ($changes -gt 0) { "Green" } else { "Yellow" })
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN] Cambios NO guardados" -ForegroundColor Magenta
    Write-Host "Ejecuta sin -DryRun para aplicar" -ForegroundColor Magenta
}
else {
    # Guardar
    $content | Set-Content $FilePath -Encoding UTF8 -NoNewline -Force
    Write-Host "[OK] Archivo guardado: $FilePath" -ForegroundColor Green
    Write-Host "[OK] Backup: $backupPath" -ForegroundColor Green
    Write-Host "`n[->] SIGUIENTE PASO: Ejecuta COMPILAR_Y_VERIFICAR.bat`n" -ForegroundColor Cyan
}
