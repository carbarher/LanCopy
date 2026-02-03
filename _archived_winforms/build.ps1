$ErrorActionPreference = "Continue"
Write-Host "Iniciando compilacion..."
dotnet build -c Release -v minimal 2>&1 | Tee-Object -FilePath "build_log.txt"
$exitCode = $LASTEXITCODE
Write-Host "Codigo de salida: $exitCode"
if ($exitCode -eq 0) {
    Write-Host "COMPILACION EXITOSA"
    if (Test-Path "bin\Release\net8.0-windows\SlskDown.exe") {
        $file = Get-Item "bin\Release\net8.0-windows\SlskDown.exe"
        Write-Host "Ejecutable generado: $($file.FullName)"
        Write-Host "Tamaño: $($file.Length) bytes"
        Write-Host "Fecha: $($file.LastWriteTime)"
    }
} else {
    Write-Host "COMPILACION FALLIDA"
    Get-Content "build_log.txt" | Select-String -Pattern "error" | Select-Object -Last 10
}
exit $exitCode
