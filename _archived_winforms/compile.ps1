Write-Host "=== COMPILANDO SLSKDOWN ===" -ForegroundColor Cyan
Write-Host ""

# Limpiar
Write-Host "[1/3] Limpiando cache..." -ForegroundColor Yellow
Remove-Item -Path "obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "bin" -Recurse -Force -ErrorAction SilentlyContinue

# Compilar
Write-Host "[2/3] Compilando..." -ForegroundColor Yellow
$output = & "C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release 2>&1
$output | Out-String | Write-Host

# Verificar
Write-Host "[3/3] Verificando ejecutable..." -ForegroundColor Yellow
$exePath = "bin\Release\net8.0-windows\SlskDown.exe"
if (Test-Path $exePath) {
    Write-Host ""
    Write-Host "=== EXITO ===" -ForegroundColor Green
    Get-Item $exePath | Select-Object Name, Length, LastWriteTime | Format-List
    Write-Host "Ejecutando aplicacion..." -ForegroundColor Green
    Start-Process $exePath
} else {
    Write-Host ""
    Write-Host "=== ERROR: No se genero ejecutable ===" -ForegroundColor Red
    Write-Host ""
    Write-Host "Archivos en bin:" -ForegroundColor Yellow
    Get-ChildItem -Path "bin" -Recurse -File | Select-Object FullName
}

Write-Host ""
Write-Host "Presiona Enter para continuar..."
Read-Host
