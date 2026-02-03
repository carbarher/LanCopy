# Script para probar busqueda directa en aMule
$password = "Carlos66*"
$baseUrl = "http://localhost:4711"
$query = "asimov"

Write-Host "=== TEST DE BUSQUEDA AMULE ==="
Write-Host ""

# Crear sesion web
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

# Paso 1: Login
Write-Host "1. Haciendo login..."
$loginData = @{
    pass = $password
    submit = "Submit"
}

try {
    $loginResponse = Invoke-WebRequest -Uri "$baseUrl/" -Method Post -Body $loginData -WebSession $session -UseBasicParsing
    Write-Host "   Status: $($loginResponse.StatusCode)"
    Write-Host "   Bytes: $($loginResponse.Content.Length)"
    
    if ($loginResponse.Content -match "Enter password") {
        Write-Host "   ERROR: Login fallo"
        exit
    }
    Write-Host "   OK: Login exitoso"
    Write-Host ""
    
    # Paso 2: Buscar
    Write-Host "2. Buscando '$query'..."
    $searchUrl = "$baseUrl/search.html?query=$query&type=global"
    $searchResponse = Invoke-WebRequest -Uri $searchUrl -WebSession $session -UseBasicParsing
    Write-Host "   Status: $($searchResponse.StatusCode)"
    Write-Host "   Bytes: $($searchResponse.Content.Length)"
    Write-Host ""
    
    # Paso 3: Analizar respuesta
    if ($searchResponse.Content -match "Enter password") {
        Write-Host "   ERROR: Sesion perdida"
    } elseif ($searchResponse.Content.Length -lt 1000) {
        Write-Host "   ADVERTENCIA: Respuesta muy pequena (probablemente login)"
        Write-Host ""
        Write-Host "========== HTML =========="
        Write-Host $searchResponse.Content
        Write-Host "========== FIN =========="
    } else {
        Write-Host "   OK: Respuesta de busqueda recibida"
        Write-Host ""
        Write-Host "========== PRIMEROS 2000 CARACTERES =========="
        Write-Host $searchResponse.Content.Substring(0, [Math]::Min(2000, $searchResponse.Content.Length))
        Write-Host "========== FIN =========="
    }
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}
