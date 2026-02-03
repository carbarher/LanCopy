Write-Output "=== VERIFICANDO OLLAMA ==="

# Verificar si el servicio está corriendo
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
    Write-Output "✅ Ollama está corriendo"
    Write-Output "`nModelos instalados:"
    $response.models | ForEach-Object { Write-Output "  - $($_.name)" }
} catch {
    Write-Output "❌ Ollama no está corriendo o no responde"
    Write-Output "Error: $_"
}
