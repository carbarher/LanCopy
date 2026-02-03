# Compilar y capturar errores
$ErrorActionPreference = 'Continue'
Write-Host "Compilando SlskDown..." -ForegroundColor Cyan
$output = dotnet build SlskDown.csproj -c Release 2>&1 | Out-String
$output | Out-File -FilePath "compile_output.txt" -Encoding UTF8

# Buscar errores
$errors = $output | Select-String "error CS"
if ($errors) {
    Write-Host "`n=== ERRORES ENCONTRADOS ===" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host $_.Line -ForegroundColor Yellow }
} else {
    Write-Host "`n=== SIN ERRORES ===" -ForegroundColor Green
}

# Verificar ejecutable
if (Test-Path "bin\Release\net8.0-windows\SlskDown.exe") {
    Write-Host "`n✅ COMPILACION EXITOSA" -ForegroundColor Green
    Get-Item "bin\Release\net8.0-windows\SlskDown.exe" | Select-Object Name, Length, LastWriteTime
} else {
    Write-Host "`n❌ COMPILACION FALLIDA" -ForegroundColor Red
}
