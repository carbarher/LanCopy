$prompt = Get-Content "c:\p2p\ollama_prompt.txt" -Raw

$body = @{
    model = "llama3.2"
    prompt = $prompt
    stream = $false
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 120
    Write-Output $response.response
} catch {
    Write-Output "Error: $_"
    Write-Output "Intentando con modelo alternativo..."
    
    $body = @{
        model = "llama2"
        prompt = $prompt
        stream = $false
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 120
        Write-Output $response.response
    } catch {
        Write-Output "Error con llama2: $_"
    }
}
