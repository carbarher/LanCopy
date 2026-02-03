# Script para corregir definitivamente el balance de llaves en MainForm.cs
$ErrorActionPreference = 'Stop'

try {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "CORRECCION FINAL DE MAINFORM.CS" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    $filePath = "MainForm.cs"
    
    Write-Host "Leyendo archivo..." -ForegroundColor Yellow
    $content = [System.IO.File]::ReadAllText($filePath, [System.Text.Encoding]::UTF8)
    $lines = $content -split "`r`n"
    
    Write-Host "Total líneas: $($lines.Count)" -ForegroundColor White
    $hash = (Get-FileHash $filePath -Algorithm SHA256).Hash
    Write-Host "Hash actual: $hash" -ForegroundColor White
    Write-Host ""
    
    Write-Host "Analizando líneas 20283-20286:" -ForegroundColor Yellow
    Write-Host "  20284: '$($lines[20283])'" -ForegroundColor Gray
    Write-Host "  20285: '$($lines[20284])'" -ForegroundColor Gray
    Write-Host "  20286: '$($lines[20285])'" -ForegroundColor Gray
    Write-Host ""
    
    $line20284 = $lines[20283].Trim()
    $line20285 = $lines[20284].Trim()
    $line20286 = $lines[20285].Trim()
    
    if ($line20285 -eq '}' -and $line20286 -eq 'finally') {
        Write-Host "PROBLEMA DETECTADO: Llave extra en línea 20285" -ForegroundColor Red
        Write-Host "Eliminando línea 20285..." -ForegroundColor Yellow
        
        # Crear nuevo array sin la línea 20285 (índice 20284)
        $newLines = @()
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($i -ne 20284) {
                $newLines += $lines[$i]
            }
        }
        
        # Guardar archivo
        $newContent = $newLines -join "`r`n"
        [System.IO.File]::WriteAllText($filePath, $newContent, [System.Text.UTF8Encoding]::new($false))
        
        Write-Host ""
        Write-Host "CORRECCION APLICADA EXITOSAMENTE!" -ForegroundColor Green
        Write-Host "Nuevas líneas totales: $($newLines.Count)" -ForegroundColor White
        
        $newHash = (Get-FileHash $filePath -Algorithm SHA256).Hash
        Write-Host "Nuevo hash: $newHash" -ForegroundColor White
        
        if ($newHash -ne $hash) {
            Write-Host ""
            Write-Host "ARCHIVO MODIFICADO CORRECTAMENTE" -ForegroundColor Green
            Write-Host "Ejecuta 'lanza' para compilar" -ForegroundColor Cyan
        } else {
            Write-Host ""
            Write-Host "ADVERTENCIA: El hash no cambió" -ForegroundColor Yellow
        }
    }
    elseif ($line20284 -eq '}' -and $line20285 -eq 'finally') {
        Write-Host "Estructura CORRECTA detectada" -ForegroundColor Green
        Write-Host "No se necesita corrección" -ForegroundColor White
    }
    else {
        Write-Host "Estructura no reconocida:" -ForegroundColor Red
        Write-Host "  Línea 20284 trim: '$line20284'" -ForegroundColor Gray
        Write-Host "  Línea 20285 trim: '$line20285'" -ForegroundColor Gray
        Write-Host "  Línea 20286 trim: '$line20286'" -ForegroundColor Gray
    }
}
catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Presiona Enter para continuar..." -ForegroundColor Cyan
Read-Host
