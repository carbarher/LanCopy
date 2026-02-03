# Script para probar aMule Web
$password = "Carlos66*"

Write-Host "Probando aMule Web..."
Write-Host ""

try {
    $query = "asimov"
    $baseUrl = "http://localhost:4711/search.html"
    $fullUrl = "$baseUrl" + "?query=$query" + "&type=global" + "&password=$password"
    
    Write-Host "URL: $fullUrl"
    Write-Host ""
    
    $response = Invoke-WebRequest -Uri $fullUrl -UseBasicParsing
    
    Write-Host "Status: $($response.StatusCode)"
    Write-Host "Bytes: $($response.Content.Length)"
    Write-Host ""
    Write-Host "========== HTML =========="
    Write-Host $response.Content
    Write-Host "========== FIN =========="
    Write-Host ""
    
    # Guardar
    $outputFile = "c:\p2p\amule.html"
    $response.Content | Out-File -FilePath $outputFile -Encoding UTF8
    Write-Host "Guardado en: $outputFile"
    
} catch {
    Write-Host "ERROR:"
    Write-Host $_.Exception.Message
}
