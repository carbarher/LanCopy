@echo off
echo ========================================
echo   REINSTALACIÓN COMPLETA DE .NET SDK 8.0
echo ========================================
echo.

echo [PASO 1] Desinstalando versiones existentes...
echo =================================================

echo Desinstalando .NET SDKs...
for /f "tokens=*" %%i in ('dotnet --list-sdks 2^>nul') do (
    echo Desinstalando SDK: %%i
    dotnet sdk uninstall %%i
)

echo Desinstalando .NET Runtimes...
for /f "tokens=*" %%i in ('dotnet --list-runtimes 2^>nul') do (
    echo Desinstalando Runtime: %%i
    dotnet runtime uninstall %%i
)

echo.
echo [PASO 2] Limpiando directorios...
echo =================================

rmdir /s /q "C:\Program Files\dotnet" 2>nul
rmdir /s /q "C:\Program Files (x86)\dotnet" 2>nul
rmdir /s /q "%USERPROFILE%\.dotnet" 2>nul
rmdir /s /q "%LOCALAPPDATA%\Microsoft\dotnet" 2>nul

echo.
echo [PASO 3] Descargando .NET SDK 8.0...
echo ==================================

echo Descargando instalador...
powershell -Command "Invoke-WebRequest -Uri 'https://download.visualstudio.microsoft.com/download/pr/8a38614b-9d81-43b2-8a2f-3eb2bfaa6c1e/dotnet-sdk-8.0.100-win-x64.exe' -OutFile 'dotnet-sdk-installer.exe'"

if not exist "dotnet-sdk-installer.exe" (
    echo ERROR: No se pudo descargar el instalador
    pause
    exit /b 1
)

echo.
echo [PASO 4] Instalando .NET SDK 8.0...
echo ================================

echo Iniciando instalación silenciosa...
dotnet-sdk-installer.exe /quiet /norestart

echo Esperando finalización de instalación...
timeout /t 30 /nobreak >nul

echo.
echo [PASO 5] Configurando variables de entorno...
echo ===========================================

setx DOTNET_ROOT "C:\Program Files\dotnet" /M
setx PATH "%PATH%;C:\Program Files\dotnet" /M

echo.
echo [PASO 6] Verificando instalación...
echo ================================

echo Reiniciando terminal para actualizar variables...
call cmd /c "echo Verificando dotnet... && dotnet --version"

echo.
echo [PASO 7] Probando compilación...
echo ===============================

cd c:\p2p\SlskDown
echo Compilando SlskDown...
dotnet build SlskDown.csproj --verbosity minimal

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ ¡INSTALACIÓN COMPLETADA CON ÉXITO!
    echo.
    echo Verificando si se generó el ejecutable...
    dir bin\Debug\net8.0-windows\*.exe 2>nul && echo ✅ EJECUTABLE GENERADO || echo ❌ El ejecutable no se generó
) else (
    echo.
    echo ❌ ERROR EN LA COMPILACIÓN
    echo Revisa los mensajes de error arriba
)

echo.
echo Limpianzando archivos temporales...
del dotnet-sdk-installer.exe 2>nul

echo.
echo ========================================
echo   PROCESO DE REINSTALACIÓN FINALIZADO
echo ========================================
pause
