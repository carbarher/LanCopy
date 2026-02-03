# Script para arreglar bloques de código comentados incorrectamente en MainForm.cs
# Patrón: // ERROR: funcion( seguido de líneas sin comentar

param(
    [string]$FilePath = "c:\p2p\SlskDown\MainForm.cs",
    [switch]$DryRun = $false,
    [switch]$Verbose = $false
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "FIX COMMENTED BLOCKS - MainForm.cs" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $FilePath)) {
    Write-Host "ERROR: No se encuentra el archivo $FilePath" -ForegroundColor Red
    exit 1
}

# Crear backup
$backupPath = "$FilePath.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $FilePath $backupPath -Force
Write-Host "[✓] Backup creado: $backupPath" -ForegroundColor Green
Write-Host ""

# Leer todas las líneas
$lines = Get-Content $FilePath -Encoding UTF8

$fixedLines = New-Object System.Collections.ArrayList
$blocksFixed = 0
$linesFixed = 0
$inErrorBlock = $false
$currentBlockStart = 0
$indentLevel = ""

Write-Host "Procesando archivo..." -ForegroundColor Yellow
Write-Host ""

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $lineNum = $i + 1
    
    # Detectar inicio de bloque ERROR
    if ($line -match '^\s*//\s*ERROR:\s*(.+)$') {
        $inErrorBlock = $true
        $currentBlockStart = $lineNum
        $errorContent = $matches[1].Trim()
        
        # Extraer nivel de indentación
        if ($line -match '^(\s*)//') {
            $indentLevel = $matches[1]
        }
        
        # Descomentar la línea ERROR
        $fixedLine = $line -replace '^\s*//\s*ERROR:\s*', $indentLevel
        [void]$fixedLines.Add($fixedLine)
        
        $blocksFixed++
        $linesFixed++
        
        if ($Verbose) {
            Write-Host "Línea $lineNum - Inicio bloque ERROR:" -ForegroundColor Cyan
            Write-Host "  Original: $line" -ForegroundColor Gray
            Write-Host "  Arreglado: $fixedLine" -ForegroundColor Green
        }
        
        continue
    }
    
    # Si estamos en un bloque ERROR, descomentar líneas subsiguientes
    if ($inErrorBlock) {
        # Verificar si la línea está indentada (parte del bloque)
        $isIndented = $line -match "^\s{$($indentLevel.Length + 4),}"
        $isClosingBrace = $line -match '^\s*\);?\s*$'
        $isEmptyOrComment = $line -match '^\s*$' -or $line -match '^\s*//'
        
        if ($isIndented -or $isClosingBrace) {
            # Esta línea es parte del bloque, mantenerla sin cambios
            [void]$fixedLines.Add($line)
            $linesFixed++
            
            if ($Verbose) {
                Write-Host "Línea $lineNum - Parte del bloque:" -ForegroundColor Yellow
                Write-Host "  $line" -ForegroundColor Gray
            }
            
            # Si es un cierre de paréntesis/punto y coma, terminar el bloque
            if ($isClosingBrace) {
                $inErrorBlock = $false
                if ($Verbose) {
                    Write-Host "  [Fin del bloque]" -ForegroundColor Magenta
                    Write-Host ""
                }
            }
        }
        else {
            # Línea no indentada o comentario = fin del bloque
            $inErrorBlock = $false
            [void]$fixedLines.Add($line)
            
            if ($Verbose) {
                Write-Host "  [Fin del bloque - línea no relacionada]" -ForegroundColor Magenta
                Write-Host ""
            }
        }
        
        continue
    }
    
    # Línea normal, copiar sin cambios
    [void]$fixedLines.Add($line)
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RESUMEN" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Bloques ERROR encontrados: $blocksFixed" -ForegroundColor Yellow
Write-Host "Líneas arregladas: $linesFixed" -ForegroundColor Yellow
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN] No se guardaron cambios" -ForegroundColor Magenta
    Write-Host "Ejecuta sin -DryRun para aplicar los cambios" -ForegroundColor Magenta
}
else {
    # Guardar archivo arreglado
    $fixedLines | Set-Content $FilePath -Encoding UTF8 -Force
    Write-Host "[✓] Archivo arreglado guardado: $FilePath" -ForegroundColor Green
    Write-Host "[✓] Backup disponible en: $backupPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SIGUIENTE PASO" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Ejecuta: COMPILAR_Y_VERIFICAR.bat" -ForegroundColor Yellow
Write-Host ""
