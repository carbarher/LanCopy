Write-Host "Verificando compilacion..." -ForegroundColor Cyan
Write-Host ""

Set-Location "c:\p2p\SlskDown"

Write-Host "[1/3] Limpiando proyecto..." -ForegroundColor Yellow
dotnet clean SlskDown.csproj -c Release --nologo | Out-Null

Write-Host "[2/3] Compilando..." -ForegroundColor Yellow
$output = dotnet build SlskDown.csproj -c Release --nologo 2>&1
$exitCode = $LASTEXITCODE

Write-Host "[3/3] Analizando resultado..." -ForegroundColor Yellow
Write-Host ""

if ($exitCode -eq 0) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "COMPILACION EXITOSA" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    
    $exePath = "bin\Release\net8.0-windows\SlskDown.exe"
    if (Test-Path $exePath) {
        $fileInfo = Get-Item $exePath
        Write-Host "Ejecutable generado: $exePath" -ForegroundColor Green
        Write-Host "Tamano: $($fileInfo.Length) bytes" -ForegroundColor Green
        Write-Host "Fecha: $($fileInfo.LastWriteTime)" -ForegroundColor Green
    } else {
        Write-Host "ADVERTENCIA: Ejecutable no encontrado en $exePath" -ForegroundColor Yellow
    }
} else {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "COMPILACION FALLIDA" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Errores:" -ForegroundColor Red
    $output | Select-String "error" | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host ""
    Write-Host "Output completo:" -ForegroundColor Yellow
    Write-Host $output
}

Write-Host ""
Write-Host "Exit code: $exitCode"
