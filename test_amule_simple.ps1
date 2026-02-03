# Script simple para ver HTML de aMule
$password = "Carlos66*"  # CAMBIA ESTO por tu contraseña real

Write-Host "Probando aMule Web..." -ForegroundColor Cyan
Write-Host ""

try {
    $url = "http://localhost:4711/search.html?query=asimov&type=global&password=$password"
    $response = Invoke-WebRequest -Uri $url -UseBasicParsing
    
    Write-Host "OK Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Tamanio: $($response.Content.Length) bytes" -ForegroundColor Green
    Write-Host ""
    Write-Host "========== HTML COMPLETO ==========" -ForegroundColor Yellow
    Write-Host $response.Content
    Write-Host "========== FIN HTML ==========" -ForegroundColor Yellow
    Write-Host ""
    
    # Guardar en archivo
    $response.Content | Out-File -FilePath "c:\p2p\amule_response.html" -Encoding UTF8
    Write-Host "Guardado en: c:\p2p\amule_response.html" -ForegroundColor Green
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
