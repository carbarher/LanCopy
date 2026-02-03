# Script para probar la interfaz web de aMule directamente
# Uso: .\test_amule_web.ps1

$baseUrl = "http://localhost:4711"
$password = "amule"  # Cambia esto por tu contraseña real

Write-Host "🧪 Probando conexión a aMule Web Interface..." -ForegroundColor Cyan
Write-Host ""

# Test 1: Página principal
Write-Host "📋 Test 1: Página principal" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/" -UseBasicParsing
    Write-Host "✅ Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "📄 Content-Length: $($response.Content.Length) bytes" -ForegroundColor Green
    Write-Host "📋 Preview (primeros 200 chars):" -ForegroundColor Cyan
    Write-Host $response.Content.Substring(0, [Math]::Min(200, $response.Content.Length))
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""

# Test 2: Búsqueda sin autenticación
Write-Host "📋 Test 2: Búsqueda sin password" -ForegroundColor Yellow
try {
    $searchUrl = "$baseUrl/search.html?query=test&type=global"
    $response = Invoke-WebRequest -Uri $searchUrl -UseBasicParsing
    Write-Host "✅ Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "📄 Content-Length: $($response.Content.Length) bytes" -ForegroundColor Green
    Write-Host "📋 Preview (primeros 500 chars):" -ForegroundColor Cyan
    Write-Host $response.Content.Substring(0, [Math]::Min(500, $response.Content.Length))
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""

# Test 3: Búsqueda con autenticación
Write-Host "📋 Test 3: Búsqueda con password" -ForegroundColor Yellow
try {
    $searchUrl = "$baseUrl/search.html?query=test&type=global&password=$password"
    $response = Invoke-WebRequest -Uri $searchUrl -UseBasicParsing
    Write-Host "✅ Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "📄 Content-Length: $($response.Content.Length) bytes" -ForegroundColor Green
    Write-Host "📋 HTML completo:" -ForegroundColor Cyan
    Write-Host $response.Content
    
    # Guardar HTML en archivo para análisis
    $htmlFile = "c:\p2p\amule_search_response.html"
    $response.Content | Out-File -FilePath $htmlFile -Encoding UTF8
    Write-Host ""
    Write-Host "💾 HTML guardado en: $htmlFile" -ForegroundColor Green
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""

# Test 4: Verificar si requiere login previo
Write-Host "📋 Test 4: Verificar endpoint de login" -ForegroundColor Yellow
try {
    $loginUrl = "$baseUrl/login.html"
    $response = Invoke-WebRequest -Uri $loginUrl -UseBasicParsing
    Write-Host "✅ Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "📋 Página de login existe" -ForegroundColor Yellow
} catch {
    Write-Host "ℹ️ No hay página de login separada" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "🎯 Prueba completada!" -ForegroundColor Green
Write-Host "📝 Revisa el archivo amule_search_response.html para ver el HTML completo" -ForegroundColor Cyan
