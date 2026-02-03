@echo off
title SlskDown Multi-Red - Inicio Automatico
color 0A

echo.
echo  ╔════════════════════════════════════════╗
echo  ║   SlskDown Multi-Red - Inicio Auto    ║
echo  ╚════════════════════════════════════════╝
echo.

REM ============================================
REM Paso 1: Verificar e iniciar eMule
REM ============================================
echo [1/3] Gestionando eMule...
tasklist | findstr /I "emule.exe" >nul 2>&1
if %errorlevel% equ 0 (
    echo      ✅ eMule ya esta corriendo
) else (
    echo      ⚠️  eMule no esta corriendo
    echo      🔄 Iniciando eMule...
    
    REM Buscar eMule en ubicaciones comunes
    if exist "C:\Program Files\eMule\emule.exe" (
        start "" "C:\Program Files\eMule\emule.exe"
        echo      ✅ eMule iniciado
    ) else if exist "C:\Program Files (x86)\eMule\emule.exe" (
        start "" "C:\Program Files (x86)\eMule\emule.exe"
        echo      ✅ eMule iniciado
    ) else if exist "%USERPROFILE%\eMule\emule.exe" (
        start "" "%USERPROFILE%\eMule\emule.exe"
        echo      ✅ eMule iniciado
    ) else (
        echo      ⚠️  eMule no encontrado - SlskDown usara solo Soulseek
        goto :skip_emule_wait
    )
    
    REM Esperar a que eMule inicie
    echo      ⏳ Esperando a que eMule inicie (3 segundos)...
    timeout /t 3 /nobreak >nul
    
    REM Verificar que eMule inicio correctamente
    tasklist | findstr /I "emule.exe" >nul 2>&1
    if %errorlevel% equ 0 (
        echo      ✅ eMule iniciado correctamente
    ) else (
        echo      ⚠️  eMule no pudo iniciar
    )
)
:skip_emule_wait
echo.

REM ============================================
REM Paso 2: Verificar puerto eMule
REM ============================================
echo [2/3] Verificando puerto EC (4712)...
timeout /t 1 /nobreak >nul
netstat -ano | findstr :4712 >nul 2>&1
if %errorlevel% equ 0 (
    echo      ✅ Puerto 4712 activo - eMule listo
) else (
    echo      ⚠️  Puerto 4712 no activo - Solo Soulseek disponible
)
echo.

REM ============================================
REM Paso 3: Iniciar SlskDown
REM ============================================
echo [3/3] Iniciando SlskDown...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    cd bin\Release\net8.0-windows
    start "" "SlskDown.exe"
    timeout /t 2 /nobreak >nul
    
    REM Verificar que SlskDown inicio
    tasklist | findstr /I "SlskDown.exe" >nul 2>&1
    if %errorlevel% equ 0 (
        echo      ✅ SlskDown iniciado correctamente
    ) else (
        echo      ⚠️  SlskDown no pudo iniciar
        goto :error
    )
) else (
    echo      ❌ SlskDown.exe no encontrado
    echo      📁 Ubicacion esperada: bin\Release\net8.0-windows\SlskDown.exe
    goto :error
)
echo.

REM ============================================
REM Resumen Final
REM ============================================
echo  ╔════════════════════════════════════════╗
echo  ║          Estado del Sistema            ║
echo  ╚════════════════════════════════════════╝
echo.

REM Verificar eMule
tasklist | findstr /I "emule.exe" >nul 2>&1
if %errorlevel% equ 0 (
    echo      ✅ eMule: CORRIENDO
) else (
    echo      ⚠️  eMule: NO DISPONIBLE
)

REM Verificar puerto
netstat -ano | findstr :4712 >nul 2>&1
if %errorlevel% equ 0 (
    echo      ✅ Puerto EC: ACTIVO
) else (
    echo      ⚠️  Puerto EC: INACTIVO
)

REM Verificar SlskDown
tasklist | findstr /I "SlskDown.exe" >nul 2>&1
if %errorlevel% equ 0 (
    echo      ✅ SlskDown: CORRIENDO
) else (
    echo      ❌ SlskDown: ERROR
)

echo.
REM Determinar modo
netstat -ano | findstr :4712 >nul 2>&1
if %errorlevel% equ 0 (
    echo      🌐 MODO: MULTI-RED (Soulseek + eMule)
) else (
    echo      📡 MODO: SOLO SOULSEEK
)
echo.
echo  ╔════════════════════════════════════════╗
echo  ║            ¡Todo Listo!                ║
echo  ╚════════════════════════════════════════╝
echo.
echo      Presiona cualquier tecla para cerrar...
pause >nul
exit /b 0

:error
echo.
echo  ╔════════════════════════════════════════╗
echo  ║              ERROR                     ║
echo  ╚════════════════════════════════════════╝
echo.
echo      ❌ Hubo un problema al iniciar
echo.
pause
exit /b 1
