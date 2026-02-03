@echo off
title Limpieza de Logs - SlskDown
color 0E

echo ========================================
echo   LIMPIEZA DE LOGS - SLSKDOWN
echo ========================================
echo.
echo Este script eliminará logs antiguos para
echo liberar espacio en disco.
echo.

cd /d c:\p2p\SlskDown

REM Verificar si existe carpeta de logs
if not exist "logs" (
    echo ⚠️ No se encontró carpeta de logs
    echo.
    echo Creando carpeta logs...
    mkdir logs
    echo ✅ Carpeta creada
    echo.
    pause
    exit /b
)

echo Carpeta de logs: %CD%\logs
echo.

REM Contar archivos antes
for /f %%A in ('dir /b logs\*.log 2^>nul ^| find /c /v ""') do set ANTES=%%A
for /f %%A in ('dir /b logs\*.txt 2^>nul ^| find /c /v ""') do set ANTES_TXT=%%A
set /a ANTES_TOTAL=%ANTES%+%ANTES_TXT%

echo Archivos actuales:
echo   - Logs (.log): %ANTES%
echo   - Textos (.txt): %ANTES_TXT%
echo   - Total: %ANTES_TOTAL%
echo.

REM Mostrar tamaño total
for /f "tokens=3" %%A in ('dir /s logs 2^>nul ^| find "bytes"') do set TAMANO=%%A
echo Espacio usado: %TAMANO% bytes
echo.

echo ========================================
echo   OPCIONES DE LIMPIEZA
echo ========================================
echo.
echo 1. Eliminar logs de más de 30 días
echo 2. Eliminar logs de más de 7 días
echo 3. Eliminar TODOS los logs
echo 4. Comprimir logs antiguos (ZIP)
echo 5. Ver logs más recientes
echo 6. Salir
echo.
set /p OPCION="Selecciona una opción (1-6): "

if "%OPCION%"=="1" goto DIAS30
if "%OPCION%"=="2" goto DIAS7
if "%OPCION%"=="3" goto TODOS
if "%OPCION%"=="4" goto COMPRIMIR
if "%OPCION%"=="5" goto VER
if "%OPCION%"=="6" goto FIN
goto FIN

:DIAS30
echo.
echo Eliminando logs de más de 30 días...
forfiles /P logs /M *.log /D -30 /C "cmd /c del @path" 2>nul
forfiles /P logs /M *.txt /D -30 /C "cmd /c del @path" 2>nul
echo ✅ Logs antiguos eliminados
goto RESUMEN

:DIAS7
echo.
echo Eliminando logs de más de 7 días...
forfiles /P logs /M *.log /D -7 /C "cmd /c del @path" 2>nul
forfiles /P logs /M *.txt /D -7 /C "cmd /c del @path" 2>nul
echo ✅ Logs antiguos eliminados
goto RESUMEN

:TODOS
echo.
echo ⚠️ ADVERTENCIA: Esto eliminará TODOS los logs
set /p CONFIRMAR="¿Estás seguro? (S/N): "
if /i not "%CONFIRMAR%"=="S" goto FIN
echo.
echo Eliminando todos los logs...
del /Q logs\*.log 2>nul
del /Q logs\*.txt 2>nul
echo ✅ Todos los logs eliminados
goto RESUMEN

:COMPRIMIR
echo.
echo Comprimiendo logs antiguos...
if not exist "logs\archivos" mkdir logs\archivos
powershell -Command "Compress-Archive -Path 'logs\*.log' -DestinationPath 'logs\archivos\logs_%date:~-4,4%%date:~-10,2%%date:~-7,2%.zip' -Force"
echo ✅ Logs comprimidos en: logs\archivos\
echo.
echo ¿Eliminar logs originales? (S/N): 
set /p ELIMINAR=
if /i "%ELIMINAR%"=="S" (
    del /Q logs\*.log
    echo ✅ Logs originales eliminados
)
goto RESUMEN

:VER
echo.
echo ========================================
echo   LOGS MÁS RECIENTES
echo ========================================
echo.
dir /O-D /B logs\*.log 2>nul | more
echo.
pause
goto FIN

:RESUMEN
echo.
echo ========================================
echo   RESUMEN
echo ========================================
echo.

REM Contar archivos después
for /f %%A in ('dir /b logs\*.log 2^>nul ^| find /c /v ""') do set DESPUES=%%A
for /f %%A in ('dir /b logs\*.txt 2^>nul ^| find /c /v ""') do set DESPUES_TXT=%%A
set /a DESPUES_TOTAL=%DESPUES%+%DESPUES_TXT%

echo Antes:  %ANTES_TOTAL% archivos
echo Después: %DESPUES_TOTAL% archivos
set /a ELIMINADOS=%ANTES_TOTAL%-%DESPUES_TOTAL%
echo Eliminados: %ELIMINADOS% archivos
echo.

REM Mostrar nuevo tamaño
for /f "tokens=3" %%A in ('dir /s logs 2^>nul ^| find "bytes"') do set NUEVO_TAMANO=%%A
echo Espacio liberado: Aproximadamente %TAMANO% → %NUEVO_TAMANO% bytes
echo.

:FIN
echo ========================================
pause
