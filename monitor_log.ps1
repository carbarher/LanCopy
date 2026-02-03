$logPath = "C:\Users\carlo\AppData\Roaming\SlskDown\logs\slskdown-2025-11-21.txt"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MONITOREANDO LOGS DE SLSKDOWN" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Archivo: $logPath" -ForegroundColor Yellow
Write-Host "Presiona Ctrl+C para salir" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Mostrar últimas 30 líneas
Write-Host "--- ULTIMAS 30 LINEAS ---" -ForegroundColor Green
Get-Content $logPath -Tail 30
Write-Host ""
Write-Host "--- MONITOREANDO EN TIEMPO REAL ---" -ForegroundColor Green
Write-Host ""

# Monitorear en tiempo real
Get-Content $logPath -Tail 0 -Wait | ForEach-Object {
    $line = $_
    
    # Colorear según contenido
    if ($line -match "✅|CONECTADO|exitosa") {
        Write-Host $line -ForegroundColor Green
    }
    elseif ($line -match "❌|ERROR|Error|falló") {
        Write-Host $line -ForegroundColor Red
    }
    elseif ($line -match "🔄|Reconectando|Reintentando|CheckAndReconnect") {
        Write-Host $line -ForegroundColor Yellow
    }
    elseif ($line -match "⚠️|WARNING|Timeout") {
        Write-Host $line -ForegroundColor Magenta
    }
    elseif ($line -match "DEBUG") {
        Write-Host $line -ForegroundColor DarkGray
    }
    else {
        Write-Host $line
    }
}
