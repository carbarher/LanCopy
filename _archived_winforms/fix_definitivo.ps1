# Script definitivo para corregir MainForm.cs
$ErrorActionPreference = 'Stop'

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CORRECCION DEFINITIVA DE MAINFORM.CS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$filePath = "MainForm.cs"
$backupPath = "MainForm.cs.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

try {
    # Crear backup
    Write-Host "Creando backup: $backupPath" -ForegroundColor Yellow
    Copy-Item $filePath $backupPath -Force
    
    # Leer archivo
    Write-Host "Leyendo archivo..." -ForegroundColor Yellow
    $content = [System.IO.File]::ReadAllText($filePath, [System.Text.Encoding]::UTF8)
    $lines = $content -split "`r`n"
    
    Write-Host "Total líneas: $($lines.Count)" -ForegroundColor White
    $hash = (Get-FileHash $filePath -Algorithm SHA256).Hash
    Write-Host "Hash actual: $hash" -ForegroundColor White
    Write-Host ""
    
    # Analizar las líneas problemáticas
    Write-Host "Analizando estructura try-finally..." -ForegroundColor Yellow
    Write-Host "  20283: '$($lines[20282])'" -ForegroundColor Gray
    Write-Host "  20284: '$($lines[20283])'" -ForegroundColor Gray
    Write-Host "  20285: '$($lines[20284])'" -ForegroundColor Gray
    Write-Host "  20286: '$($lines[20285])'" -ForegroundColor Gray
    Write-Host ""
    
    $line20284 = $lines[20283].Trim()
    $line20285 = $lines[20284].Trim()
    $line20286 = $lines[20285].Trim()
    
    # Verificar si hay llave extra
    if ($line20285 -eq '}' -and $line20286 -eq 'finally') {
        Write-Host "PROBLEMA: Llave extra en línea 20285" -ForegroundColor Red
        Write-Host "Eliminando línea 20285..." -ForegroundColor Yellow
        
        # Eliminar línea 20285 (índice 20284)
        $newLines = @()
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($i -ne 20284) {
                $newLines += $lines[$i]
            }
        }
        
        # Guardar
        $newContent = $newLines -join "`r`n"
        [System.IO.File]::WriteAllText($filePath, $newContent, [System.Text.UTF8Encoding]::new($false))
        
        # Marcar archivo como solo lectura temporalmente
        Set-ItemProperty $filePath -Name IsReadOnly -Value $true
        Start-Sleep -Seconds 2
        Set-ItemProperty $filePath -Name IsReadOnly -Value $false
        
        Write-Host ""
        Write-Host "CORRECCION APLICADA!" -ForegroundColor Green
        Write-Host "Nuevas líneas: $($newLines.Count)" -ForegroundColor White
        Write-Host "Nuevo hash: $((Get-FileHash $filePath -Algorithm SHA256).Hash)" -ForegroundColor White
        Write-Host ""
        Write-Host "CIERRA WINDSURF COMPLETAMENTE antes de compilar" -ForegroundColor Yellow
        Write-Host "Luego ejecuta: lanza" -ForegroundColor Cyan
    }
    elseif ($line20284 -eq '}' -and $line20285 -eq 'finally') {
        Write-Host "Estructura CORRECTA" -ForegroundColor Green
        Write-Host "Pero hay error de compilación..." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "El problema puede estar en otro lugar." -ForegroundColor Yellow
        Write-Host "Verifica que el archivo no esté siendo modificado por el IDE." -ForegroundColor Yellow
    }
    else {
        Write-Host "Estructura no reconocida:" -ForegroundColor Red
        Write-Host "  Línea 20284: '$line20284'" -ForegroundColor Gray
        Write-Host "  Línea 20285: '$line20285'" -ForegroundColor Gray
        Write-Host "  Línea 20286: '$line20286'" -ForegroundColor Gray
    }
}
catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    
    # Restaurar backup si existe
    if (Test-Path $backupPath) {
        Write-Host "Restaurando backup..." -ForegroundColor Yellow
        Copy-Item $backupPath $filePath -Force
    }
    
    exit 1
}

Write-Host ""
Write-Host "Presiona Enter para continuar..." -ForegroundColor Cyan
Read-Host
