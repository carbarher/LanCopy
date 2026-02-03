@echo off
echo ========================================
echo   REINSTALACIÓN MEJORADA DE .NET SDK 8.0
echo ========================================
echo.

echo [PASO 1] Verificando instalación actual...
echo ==========================================
echo Versiones de .NET SDK instaladas:
dotnet --list-sdks 2>nul
echo.
echo Runtimes instalados:
dotnet --list-runtimes 2>nul
echo.

echo [PASO 2] Omitiendo desinstalación automática...
echo ================================================
echo La desinstalación manual es más segura. Si quieres desinstalar:
echo 1. Ve a "Apps y características" en Windows
echo 2. Busca "Microsoft .NET SDK 8.0" y desinstala
echo 3. Busca "Microsoft Windows Desktop Runtime" y desinstala
echo.

echo [PASO 3] Descargando .NET SDK 8.0...
echo ==================================

echo Opción A: Descarga automática (intentando mirrors alternativos)...
set SDK_URL=https://download.visualstudio.microsoft.com/download/pr/8a38614b-9d81-43b2-8a2f-3eb2bfaa6c1e/dotnet-sdk-8.0.100-win-x64.exe
set SDK_FILE=dotnet-sdk-8.0.100-win-x64.exe

echo Intentando descarga principal...
powershell -Command "try { Invoke-WebRequest -Uri '%SDK_URL%' -OutFile '%SDK_FILE%' -UseBasicParsing; Write-Host '✅ Descarga exitosa' } catch { Write-Host '❌ Error en descarga principal' }"

if not exist "%SDK_FILE%" (
    echo.
    echo Opción B: Descarga desde mirror alternativo...
    set SDK_ALT=https://download.microsoft.com/download/8/8/5/8855F297-9345-43A8-A4F6-3E2F6A5E5E5F/dotnet-sdk-8.0.100-win-x64.exe
    powershell -Command "try { Invoke-WebRequest -Uri '%SDK_ALT%' -OutFile '%SDK_FILE%' -UseBasicParsing; Write-Host '✅ Descarga alternativa exitosa' } catch { Write-Host '❌ Error en descarga alternativa' }"
)

if not exist "%SDK_FILE%" (
    echo.
    echo ❌ ERROR: No se pudo descargar automáticamente
    echo.
    echo Opción C: Descarga manual
    echo =========================
    echo 1. Abre tu navegador web
    echo 2. Ve a: https://dotnet.microsoft.com/download/dotnet/8.0
    echo 3. Descarga "SDK .NET 8.0.100 (Windows x64)"
    echo 4. Guarda el archivo como: %SDK_FILE%
    echo 5. Ejecuta este script nuevamente
    echo.
    pause
    exit /b 1
)

echo.
echo [PASO 4] Instalando .NET SDK 8.0...
echo ================================

echo Iniciando instalación silenciosa...
echo Esto puede tomar varios minutos...
%SDK_FILE% /quiet /norestart

echo Esperando finalización...
timeout /t 60 /nobreak >nul

echo.
echo [PASO 5] Configurando variables de entorno...
echo ===========================================

echo Configurando DOTNET_ROOT...
setx DOTNET_ROOT "C:\Program Files\dotnet" /M >nul 2>&1

echo Agregando al PATH del sistema...
for /f "tokens=*" %%i in ('echo %PATH%') do set CURRENT_PATH=%%i
setx PATH "%CURRENT_PATH%;C:\Program Files\dotnet" /M >nul 2>&1

echo.
echo [PASO 6] Verificando instalación...
echo ================================

echo Actualizando variables de entorno para esta sesión...
set DOTNET_ROOT=C:\Program Files\dotnet
set PATH=%PATH%;C:\Program Files\dotnet

echo Verificando dotnet...
dotnet --version 2>nul
if %ERRORLEVEL% EQU 0 (
    echo ✅ .NET SDK instalado correctamente
) else (
    echo ❌ ERROR: .NET SDK no responde
    echo Puede que necesites reiniciar el equipo
)

echo.
echo [PASO 7] Probando compilación...
echo ===============================

cd c:\p2p\SlskDown

echo Creando proyecto de prueba...
mkdir test_quick 2>nul
cd test_quick
dotnet new console --force >nul 2>&1

echo Compilando prueba...
dotnet build --verbosity minimal >nul 2>&1

if %ERRORLEVEL% EQU 0 (
    echo ✅ Compilación de prueba exitosa
    
    echo Verificando ejecutable...
    if exist "bin\Debug\net8.0\test_quick.exe" (
        echo ✅ Ejecutable de prueba generado
    ) else (
        echo ❌ No se generó ejecutable de prueba
    )
) else (
    echo ❌ Falló compilación de prueba
)

cd ..
rmdir /s /q test_quick 2>nul

echo.
echo Probando SlskDown...
dotnet build SlskDown.csproj --verbosity minimal

if %ERRORLEVEL% EQU 0 (
    echo ✅ SlskDown compilado exitosamente
    
    if exist "bin\Debug\net8.0-windows\SlskDown.exe" (
        echo ✅ EJECUTABLE SlskDown GENERADO
        echo.
        echo 🎉 ¡ÉXITO COMPLETO!
        echo El archivo ejecutable está en: bin\Debug\net8.0-windows\SlskDown.exe
    ) else (
        echo ❌ SlskDown compiló pero no generó ejecutable
    )
) else (
    echo ❌ ERROR: SlskDown no pudo compilarse
)

echo.
echo [PASO 8] Limpieza...
echo ==================

echo Eliminando instalador...
del "%SDK_FILE%" 2>nul

echo.
echo ========================================
echo   PROCESO FINALIZADO
echo ========================================
echo.
echo IMPORTANTE: Si algo no funcionó correctamente:
echo 1. Reinicia tu equipo completamente
echo 2. Vuelve a ejecutar: PROBAR_COMPILACION.bat
echo 3. Si persiste el problema, contacta soporte técnico
echo.
pause
