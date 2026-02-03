# Script para probar si la sesion se mantiene
$password = "Carlos66*"
$baseUrl = "http://localhost:4711"

Write-Host "Test 1: Login"
Write-Host ""

# Crear sesion web
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

# Hacer login
$loginData = @{
    pass = $password
    submit = "Submit"
}

try {
    $loginResponse = Invoke-WebRequest -Uri "$baseUrl/" -Method Post -Body $loginData -WebSession $session -UseBasicParsing
    Write-Host "Login Status: $($loginResponse.StatusCode)"
    Write-Host "Login Bytes: $($loginResponse.Content.Length)"
    
    if ($loginResponse.Content -match "Enter password") {
        Write-Host "ERROR: Login fallo - todavia pide password"
    } else {
        Write-Host "OK: Login exitoso"
    }
    
    Write-Host ""
    Write-Host "Test 2: Busqueda con sesion"
    Write-Host ""
    
    # Buscar usando la misma sesion
    $searchResponse = Invoke-WebRequest -Uri "$baseUrl/search.html?query=asimov&type=global" -WebSession $session -UseBasicParsing
    Write-Host "Search Status: $($searchResponse.StatusCode)"
    Write-Host "Search Bytes: $($searchResponse.Content.Length)"
    Write-Host ""
    
    if ($searchResponse.Content -match "Enter password") {
        Write-Host "ERROR: Sesion perdida - pide password de nuevo"
    } else {
        Write-Host "OK: Sesion mantenida"
        Write-Host ""
        Write-Host "========== HTML BUSQUEDA =========="
        Write-Host $searchResponse.Content
        Write-Host "========== FIN =========="
    }
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}
