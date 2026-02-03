$output = dotnet build -c Release 2>&1 | Out-String
Write-Host $output
$output | Out-File "compile_output.txt" -Encoding UTF8
if ($output -match "error") {
    Write-Host "ERRORES ENCONTRADOS" -ForegroundColor Red
} else {
    Write-Host "COMPILACION EXITOSA" -ForegroundColor Green
}
