# Prueba rapida de Ollama
Write-Host "Probando Ollama con consulta simple..." -ForegroundColor Cyan

$body = @{
    model = "llama2"
    prompt = "Responde solo con la palabra 'Hola' en espanol"
    stream = $false
} | ConvertTo-Json

try {
    Write-Host "Enviando consulta a Ollama..." -ForegroundColor Yellow
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 60
    
    Write-Host "`n[OK] Respuesta recibida:" -ForegroundColor Green
    Write-Host $response.response -ForegroundColor Cyan
    Write-Host "`nOllama funciona correctamente!" -ForegroundColor Green
} catch {
    Write-Host "`n[ERROR] $($_.Exception.Message)" -ForegroundColor Red
}
