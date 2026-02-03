Set-Location "c:\p2p\SlskDown"
dotnet build SlskDown.csproj -c Release --verbosity normal --nologo
$exitCode = $LASTEXITCODE
Write-Host ""
Write-Host "Exit code: $exitCode"
if ($exitCode -eq 0) {
    Write-Host "COMPILACION EXITOSA" -ForegroundColor Green
} else {
    Write-Host "COMPILACION FALLIDA" -ForegroundColor Red
}
