# Script de Prueba de Ollama para SlskDown
# Ejecutar con: powershell -ExecutionPolicy Bypass -File test_ollama_fixed.ps1

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "   PRUEBA DE CONEXION CON OLLAMA" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Verificar si Ollama esta instalado
Write-Host "[1/5] Verificando instalacion de Ollama..." -ForegroundColor Yellow
try {
    $version = & ollama --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [OK] Ollama instalado: $version" -ForegroundColor Green
    } else {
        Write-Host "  [ERROR] Ollama NO esta instalado" -ForegroundColor Red
        Write-Host "  Descarga desde: https://ollama.ai/download" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "  [ERROR] Ollama NO esta instalado" -ForegroundColor Red
    Write-Host "  Descarga desde: https://ollama.ai/download" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Test 2: Verificar si el servidor esta corriendo
Write-Host "[2/5] Verificando servidor Ollama..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -Method GET -TimeoutSec 5 -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  [OK] Servidor Ollama corriendo en http://localhost:11434" -ForegroundColor Green
    }
} catch {
    Write-Host "  [ERROR] Servidor Ollama NO esta corriendo" -ForegroundColor Red
    Write-Host "  Ejecuta en otra terminal: ollama serve" -ForegroundColor Yellow
    Write-Host "  Esperando 5 segundos para que inicies Ollama..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    # Reintentar
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -Method GET -TimeoutSec 5 -ErrorAction Stop
        Write-Host "  [OK] Servidor Ollama ahora esta corriendo" -ForegroundColor Green
    } catch {
        Write-Host "  [ERROR] Servidor sigue sin responder" -ForegroundColor Red
        Write-Host "  Abre otra terminal y ejecuta: ollama serve" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""

# Test 3: Verificar modelos instalados
Write-Host "[3/5] Verificando modelos instalados..." -ForegroundColor Yellow
try {
    $models = & ollama list 2>&1
    if ($models -match "llama2|mistral|phi|codellama") {
        Write-Host "  [OK] Modelos encontrados:" -ForegroundColor Green
        Write-Host $models
    } else {
        Write-Host "  [AVISO] No hay modelos instalados" -ForegroundColor Yellow
        Write-Host "  Descarga un modelo: ollama pull llama2" -ForegroundColor Yellow
        Write-Host "  Quieres descargar llama2 ahora? (S/N)" -ForegroundColor Cyan
        $download = Read-Host
        if ($download -eq "S" -or $download -eq "s") {
            Write-Host "  Descargando llama2 (~3.8 GB)..." -ForegroundColor Yellow
            & ollama pull llama2
            Write-Host "  [OK] Modelo descargado" -ForegroundColor Green
        }
    }
} catch {
    Write-Host "  [ERROR] Error verificando modelos" -ForegroundColor Red
}

Write-Host ""

# Test 4: Probar API de generacion
Write-Host "[4/5] Probando API de generacion..." -ForegroundColor Yellow
try {
    $body = @{
        model = "llama2"
        prompt = "Di solo 'Hola' en espanol"
        stream = $false
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 30
    
    if ($response.response) {
        Write-Host "  [OK] API funcionando correctamente" -ForegroundColor Green
        Write-Host "  Respuesta: $($response.response)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "  [ERROR] Error en API: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Message -match "404") {
        Write-Host "  Modelo 'llama2' no encontrado. Descargalo con: ollama pull llama2" -ForegroundColor Yellow
    }
}

Write-Host ""

# Test 5: Verificar configuracion de SlskDown
Write-Host "[5/5] Verificando configuracion de SlskDown..." -ForegroundColor Yellow
$configPath = "$env:APPDATA\SlskDown\config.json"
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    Write-Host "  [OK] Configuracion encontrada" -ForegroundColor Green
    Write-Host "  - IA Activada: $($config.aiEnabled)" -ForegroundColor Cyan
    Write-Host "  - URL Ollama: $($config.ollamaUrl)" -ForegroundColor Cyan
    Write-Host "  - Modelo: $($config.ollamaModel)" -ForegroundColor Cyan
    
    if ($config.aiEnabled -eq $false) {
        Write-Host "  [AVISO] IA esta desactivada en SlskDown" -ForegroundColor Yellow
        Write-Host "  Activala en: Configuracion > IA > Activar Ollama" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [AVISO] Archivo de configuracion no encontrado" -ForegroundColor Yellow
    Write-Host "  Ejecuta SlskDown al menos una vez para crear la configuracion" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "   RESUMEN" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# Resumen final
$allOk = $true
Write-Host ""
Write-Host "Estado de componentes:" -ForegroundColor White
Write-Host "  - Ollama instalado: " -NoNewline
if ($version) { Write-Host "[OK]" -ForegroundColor Green } else { Write-Host "[ERROR]" -ForegroundColor Red; $allOk = $false }

Write-Host "  - Servidor corriendo: " -NoNewline
try {
    $test = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -Method GET -TimeoutSec 2 -ErrorAction Stop
    Write-Host "[OK]" -ForegroundColor Green
} catch {
    Write-Host "[ERROR]" -ForegroundColor Red
    $allOk = $false
}

Write-Host "  - Modelos instalados: " -NoNewline
$modelList = & ollama list 2>&1
if ($modelList -match "llama2|mistral|phi") { Write-Host "[OK]" -ForegroundColor Green } else { Write-Host "[ERROR]" -ForegroundColor Red; $allOk = $false }

Write-Host ""

if ($allOk) {
    Write-Host "TODO LISTO! Ollama esta configurado correctamente" -ForegroundColor Green
    Write-Host "Puedes usar las funcionalidades de IA en SlskDown" -ForegroundColor Green
} else {
    Write-Host "Hay problemas con la configuracion" -ForegroundColor Yellow
    Write-Host "Revisa los pasos anteriores y corrige los errores" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Pasos para solucionar:" -ForegroundColor Cyan
    Write-Host "  1. Descarga Ollama: https://ollama.ai/download" -ForegroundColor White
    Write-Host "  2. Ejecuta: ollama serve" -ForegroundColor White
    Write-Host "  3. Descarga modelo: ollama pull llama2" -ForegroundColor White
    Write-Host "  4. Activa IA en SlskDown (Configuracion)" -ForegroundColor White
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Presiona cualquier tecla para salir..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
