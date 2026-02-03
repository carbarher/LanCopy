cd c:\p2p\SlskDown
Write-Host "Limpiando directorios..."
Remove-Item -Path bin -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path obj -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`nCompilando SlskDown..."
& "C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release

if (Test-Path "bin\Release\net8.0-windows\SlskDown.exe") {
    Write-Host "`n===== COMPILACION EXITOSA =====" -ForegroundColor Green
    Write-Host "Ejecutable: bin\Release\net8.0-windows\SlskDown.exe"
    Write-Host "`nIniciando aplicacion..."
    Start-Process "bin\Release\net8.0-windows\SlskDown.exe"
} else {
    Write-Host "`n===== ERROR: NO SE GENERO EL EJECUTABLE =====" -ForegroundColor Red
    Write-Host "`nVerificando archivos en obj..."
    Get-ChildItem -Path obj -Recurse -Filter *.exe -ErrorAction SilentlyContinue
}
