# Script PowerShell para forzar recompilación
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "FORZANDO RECOMPILACION TOTAL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Ir al directorio
Set-Location "c:\p2p\SlskDown"

# 1. Eliminar TODOS los binarios
Write-Host "`n[1/6] Eliminando binarios..." -ForegroundColor Yellow
Remove-Item -Path "bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "obj" -Recurse -Force -ErrorAction SilentlyContinue

# 2. Modificar MainForm.cs para cambiar timestamp
Write-Host "[2/6] Modificando MainForm.cs..." -ForegroundColor Yellow
$content = Get-Content "MainForm.cs" -Raw
$content = $content -replace "// TIMESTAMP: .*", "// TIMESTAMP: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
if ($content -notmatch "// TIMESTAMP:") {
    $content = "// TIMESTAMP: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n" + $content
}
Set-Content "MainForm.cs" -Value $content -NoNewline

# 3. Limpiar caché de compilación
Write-Host "[3/6] Limpiando caché..." -ForegroundColor Yellow
dotnet clean --verbosity quiet

# 4. Restaurar paquetes
Write-Host "[4/6] Restaurando paquetes..." -ForegroundColor Yellow
dotnet restore --verbosity quiet

# 5. Compilar SIN caché compartida
Write-Host "[5/6] Compilando desde cero..." -ForegroundColor Yellow
dotnet build SlskDown.csproj -c Release --no-incremental /p:UseSharedCompilation=false

# 6. Verificar resultado
Write-Host "`n[6/6] Verificando resultado..." -ForegroundColor Yellow
if (Test-Path "bin\Release\net8.0-windows\SlskDown.exe") {
    $exe = Get-Item "bin\Release\net8.0-windows\SlskDown.exe"
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "EXITO - Ejecutable creado:" -ForegroundColor Green
    Write-Host "  Fecha: $($exe.LastWriteTime)" -ForegroundColor Green
    Write-Host "  Tamaño: $($exe.Length) bytes" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "ERROR - No se creó el ejecutable" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
}

Write-Host "`nPresiona cualquier tecla para continuar..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
