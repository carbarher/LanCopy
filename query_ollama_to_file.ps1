$outputFile = "c:\p2p\ollama_response.txt"

Write-Output "Verificando Ollama..." | Out-File $outputFile

# Verificar si Ollama está corriendo
try {
    $tags = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
    Write-Output "✅ Ollama corriendo. Modelos: $($tags.models.name -join ', ')" | Out-File $outputFile -Append
    
    # Leer el prompt
    $prompt = Get-Content "c:\p2p\ollama_prompt.txt" -Raw
    
    # Intentar con el primer modelo disponible
    $model = $tags.models[0].name
    Write-Output "`nUsando modelo: $model" | Out-File $outputFile -Append
    Write-Output "`nConsultando a Ollama (esto puede tardar 1-2 minutos)...`n" | Out-File $outputFile -Append
    
    $body = @{
        model = $model
        prompt = $prompt
        stream = $false
    } | ConvertTo-Json -Depth 10
    
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 180
    
    Write-Output "=== RESPUESTA DE OLLAMA ===" | Out-File $outputFile -Append
    Write-Output $response.response | Out-File $outputFile -Append
    Write-Output "`n=== FIN RESPUESTA ===" | Out-File $outputFile -Append
    
} catch {
    Write-Output "❌ Error: $_" | Out-File $outputFile -Append
}

Write-Output "`nRespuesta guardada en: $outputFile" | Out-File $outputFile -Append
