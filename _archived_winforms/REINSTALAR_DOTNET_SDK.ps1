# REINSTALACIÓN .NET SDK 8.0 - VERSIÓN POWERSHELL
# ===============================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  REINSTALACIÓN COMPLETA DE .NET SDK 8.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# [PASO 1] Desinstalación de versiones existentes
Write-Host "[PASO 1] Desinstalando versiones existentes..." -ForegroundColor Yellow
Write-Host "=================================================" -ForegroundColor Yellow

try {
    $sdks = dotnet --list-sdks 2>$null
    if ($sdks) {
        foreach ($sdk in $sdks) {
            Write-Host "Desinstalando SDK: $sdk" -ForegroundColor Gray
            $version = $sdk.Split(' ')[0]
            dotnet sdk uninstall $version --force 2>$null
        }
    }
    
    $runtimes = dotnet --list-runtimes 2>$null
    if ($runtimes) {
        foreach ($runtime in $runtimes) {
            Write-Host "Desinstalando Runtime: $runtime" -ForegroundColor Gray
            $parts = $runtime.Split(' ')
            if ($parts.Length -ge 2) {
                dotnet runtime uninstall "$($parts[0]) $($parts[1])" --force 2>$null
            }
        }
    }
} catch {
    Write-Host "Error desinstalando: $_" -ForegroundColor Red
}

# [PASO 2] Limpieza de directorios
Write-Host ""
Write-Host "[PASO 2] Limpiando directorios..." -ForegroundColor Yellow
Write-Host "=================================" -ForegroundColor Yellow

$pathsToClean = @(
    "C:\Program Files\dotnet",
    "C:\Program Files (x86)\dotnet",
    "$env:USERPROFILE\.dotnet",
    "$env:LOCALAPPDATA\Microsoft\dotnet"
)

foreach ($path in $pathsToClean) {
    if (Test-Path $path) {
        Write-Host "Eliminando: $path" -ForegroundColor Gray
        Remove-Item -Path $path -Recurse -Force 2>$null
    }
}

# [PASO 3] Descarga del instalador
Write-Host ""
Write-Host "[PASO 3] Descargando .NET SDK 8.0..." -ForegroundColor Yellow
Write-Host "==================================" -ForegroundColor Yellow

$installerUrl = "https://download.visualstudio.microsoft.com/download/pr/8a38614b-9d81-43b2-8a2f-3eb2bfaa6c1e/dotnet-sdk-8.0.100-win-x64.exe"
$installerPath = "$env:TEMP\dotnet-sdk-installer.exe"

try {
    Write-Host "Descargando instalador..." -ForegroundColor Gray
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    
    if (-not (Test-Path $installerPath)) {
        throw "No se pudo descargar el instalador"
    }
    
    Write-Host "✅ Descarga completada" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: No se pudo descargar el instalador" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    Read-Host "Presiona Enter para salir"
    exit 1
}

# [PASO 4] Instalación silenciosa
Write-Host ""
Write-Host "[PASO 4] Instalando .NET SDK 8.0..." -ForegroundColor Yellow
Write-Host "================================" -ForegroundColor Yellow

try {
    Write-Host "Iniciando instalación silenciosa..." -ForegroundColor Gray
    $process = Start-Process -FilePath $installerPath -ArgumentList "/quiet", "/norestart" -Wait -PassThru
    
    if ($process.ExitCode -eq 0) {
        Write-Host "✅ Instalación completada" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Código de salida: $($process.ExitCode)" -ForegroundColor Yellow
    }
    
    Write-Host "Esperando 30 segundos..." -ForegroundColor Gray
    Start-Sleep -Seconds 30
} catch {
    Write-Host "❌ Error en la instalación: $_" -ForegroundColor Red
}

# [PASO 5] Configuración de variables de entorno
Write-Host ""
Write-Host "[PASO 5] Configurando variables de entorno..." -ForegroundColor Yellow
Write-Host "===========================================" -ForegroundColor Yellow

try {
    [Environment]::SetEnvironmentVariable("DOTNET_ROOT", "C:\Program Files\dotnet", "Machine")
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
    [Environment]::SetEnvironmentVariable("PATH", "$currentPath;C:\Program Files\dotnet", "Machine")
    
    Write-Host "✅ Variables de entorno configuradas" -ForegroundColor Green
} catch {
    Write-Host "❌ Error configurando variables: $_" -ForegroundColor Red
}

# [PASO 6] Verificación
Write-Host ""
Write-Host "[PASO 6] Verificando instalación..." -ForegroundColor Yellow
Write-Host "=================================" -ForegroundColor Yellow

# Actualizar variables para la sesión actual
$env:DOTNET_ROOT = "C:\Program Files\dotnet"
$env:PATH = "$env:PATH;C:\Program Files\dotnet"

try {
    Write-Host "Verificando dotnet..." -ForegroundColor Gray
    $dotnetVersion = dotnet --version 2>$null
    if ($dotnetVersion) {
        Write-Host "✅ Versión de .NET: $dotnetVersion" -ForegroundColor Green
    } else {
        Write-Host "❌ No se puede ejecutar dotnet" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Error verificando instalación: $_" -ForegroundColor Red
}

# [PASO 7] Prueba de compilación
Write-Host ""
Write-Host "[PASO 7] Probando compilación..." -ForegroundColor Yellow
Write-Host "================================" -ForegroundColor Yellow

Set-Location "c:\p2p\SlskDown"

try {
    Write-Host "Compilando SlskDown..." -ForegroundColor Gray
    $buildResult = dotnet build SlskDown.csproj --verbosity minimal 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ ¡COMPILACIÓN EXITOSA!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Verificando ejecutable..." -ForegroundColor Gray
        
        $exePath = "bin\Debug\net8.0-windows\SlskDown.exe"
        if (Test-Path $exePath) {
            Write-Host "✅ EJECUTABLE GENERADO: $exePath" -ForegroundColor Green
            $size = (Get-Item $exePath).Length / 1MB
            Write-Host "Tamaño: $([math]::Round($size, 2)) MB" -ForegroundColor Gray
        } else {
            Write-Host "❌ El ejecutable no se generó" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ ERROR EN LA COMPILACIÓN" -ForegroundColor Red
        Write-Host $buildResult -ForegroundColor Gray
    }
} catch {
    Write-Host "❌ Error durante la compilación: $_" -ForegroundColor Red
}

# Limpieza
Write-Host ""
Write-Host "Limpiando archivos temporales..." -ForegroundColor Gray
Remove-Item $installerPath -Force 2>$null

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PROCESO DE REINSTALACIÓN FINALIZADO" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "IMPORTANTE: Reinicia tu terminal/IDE para que las variables de entorno tomen efecto" -ForegroundColor Yellow
Read-Host "Presiona Enter para salir"
