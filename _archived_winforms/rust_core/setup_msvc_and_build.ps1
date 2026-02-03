# Script para configurar MSVC y compilar Rust DLL
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CONFIGURAR MSVC Y COMPILAR RUST DLL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Buscar vcvars64.bat
Write-Host "[1] Buscando vcvars64.bat..." -ForegroundColor Yellow
$vcvarsPath = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"

if (Test-Path $vcvarsPath) {
    Write-Host "✓ Encontrado: $vcvarsPath" -ForegroundColor Green
} else {
    Write-Host "✗ NO ENCONTRADO en ubicación estándar" -ForegroundColor Red
    Write-Host "Buscando en otras ubicaciones..." -ForegroundColor Yellow
    
    $possiblePaths = @(
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat"
    )
    
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $vcvarsPath = $path
            Write-Host "✓ Encontrado: $vcvarsPath" -ForegroundColor Green
            break
        }
    }
    
    if (-not (Test-Path $vcvarsPath)) {
        Write-Host "✗ ERROR: vcvars64.bat no encontrado en ninguna ubicación" -ForegroundColor Red
        Write-Host "Build Tools no está instalado correctamente" -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

# 2. Configurar entorno MSVC
Write-Host "[2] Configurando entorno MSVC..." -ForegroundColor Yellow
Write-Host "Ejecutando: $vcvarsPath" -ForegroundColor Gray

# Ejecutar vcvars64.bat y capturar variables de entorno
$tempFile = [System.IO.Path]::GetTempFileName()
cmd /c "`"$vcvarsPath`" && set" > $tempFile

# Leer variables de entorno
Get-Content $tempFile | ForEach-Object {
    if ($_ -match '^([^=]+)=(.*)$') {
        $name = $matches[1]
        $value = $matches[2]
        Set-Item -Path "env:$name" -Value $value -ErrorAction SilentlyContinue
    }
}
Remove-Item $tempFile

Write-Host "✓ Entorno MSVC configurado" -ForegroundColor Green
Write-Host ""

# 3. Verificar linker
Write-Host "[3] Verificando linker MSVC..." -ForegroundColor Yellow
$linker = Get-Command link.exe -ErrorAction SilentlyContinue
if ($linker) {
    Write-Host "✓ Linker encontrado: $($linker.Source)" -ForegroundColor Green
    
    # Verificar versión
    $version = & link.exe 2>&1 | Select-String "Version"
    Write-Host "  Versión: $version" -ForegroundColor Gray
} else {
    Write-Host "✗ ERROR: link.exe no encontrado incluso después de configurar MSVC" -ForegroundColor Red
    Write-Host "PATH actual:" -ForegroundColor Yellow
    Write-Host $env:PATH -ForegroundColor Gray
    exit 1
}
Write-Host ""

# 4. Verificar variables de entorno
Write-Host "[4] Verificando variables de entorno MSVC..." -ForegroundColor Yellow
if ($env:LIB) {
    Write-Host "✓ LIB configurada" -ForegroundColor Green
} else {
    Write-Host "✗ LIB no configurada" -ForegroundColor Red
}

if ($env:LIBPATH) {
    Write-Host "✓ LIBPATH configurada" -ForegroundColor Green
} else {
    Write-Host "✗ LIBPATH no configurada" -ForegroundColor Red
}

if ($env:INCLUDE) {
    Write-Host "✓ INCLUDE configurada" -ForegroundColor Green
} else {
    Write-Host "✗ INCLUDE no configurada" -ForegroundColor Red
}
Write-Host ""

# 5. Limpiar build anterior
Write-Host "[5] Limpiando build anterior..." -ForegroundColor Yellow
cargo clean
Write-Host "✓ Build limpiado" -ForegroundColor Green
Write-Host ""

# 6. Compilar Rust
Write-Host "[6] Compilando Rust DLL..." -ForegroundColor Yellow
Write-Host "Ejecutando: cargo build --release" -ForegroundColor Gray
Write-Host ""

$output = cargo build --release 2>&1
$output | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✓ Compilación exitosa" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "✗ Compilación falló con código: $LASTEXITCODE" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 7. Verificar DLL generada
Write-Host "[7] Verificando DLL generada..." -ForegroundColor Yellow

$dllPath = "target\release\slskdown_core.dll"
if (Test-Path $dllPath) {
    Write-Host "✓✓✓ ÉXITO: DLL GENERADA ✓✓✓" -ForegroundColor Green
    $dll = Get-Item $dllPath
    Write-Host ""
    Write-Host "Ubicación: $($dll.FullName)" -ForegroundColor Cyan
    Write-Host "Tamaño: $($dll.Length) bytes" -ForegroundColor Cyan
    Write-Host "Fecha: $($dll.LastWriteTime)" -ForegroundColor Cyan
    Write-Host ""
    
    # 8. Copiar a directorio de salida C#
    Write-Host "[8] Copiando DLL a directorio de salida C#..." -ForegroundColor Yellow
    $destPath = "..\bin\Release\net8.0-windows\slskdown_core.dll"
    $destDir = Split-Path $destPath -Parent
    
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        Write-Host "✓ Directorio creado: $destDir" -ForegroundColor Green
    }
    
    Copy-Item $dllPath $destPath -Force
    Write-Host "✓ DLL copiada a: $destPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "PROCESO COMPLETADO CON ÉXITO" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    
} else {
    Write-Host "✗ ERROR: DLL NO GENERADA" -ForegroundColor Red
    Write-Host ""
    Write-Host "Buscando en otras ubicaciones..." -ForegroundColor Yellow
    $allDlls = Get-ChildItem -Path "target" -Filter "slskdown_core.dll" -Recurse -ErrorAction SilentlyContinue
    
    if ($allDlls) {
        Write-Host "DLL encontrada en ubicación alternativa:" -ForegroundColor Yellow
        $allDlls | ForEach-Object {
            Write-Host "  $($_.FullName)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "DLL no encontrada en ninguna ubicación" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Archivos generados en target\release:" -ForegroundColor Yellow
    Get-ChildItem -Path "target\release" -Filter "*.dll" | ForEach-Object {
        Write-Host "  $($_.Name)" -ForegroundColor Gray
    }
    
    exit 1
}
