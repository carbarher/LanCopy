@echo off
setlocal enabledelayedexpansion

echo ========================================
echo ARREGLO AUTOMATICO Y COMPILACION
echo ========================================
echo.

REM Paso 1: Arreglar bloques comentados
echo [1/2] Arreglando bloques comentados...
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0fix_all_errors.ps1"

if %errorlevel% neq 0 (
    echo.
    echo ERROR: Fallo al arreglar bloques
    pause
    exit /b 1
)

echo.
echo ========================================
echo.

REM Paso 2: Compilar
echo [2/2] Compilando proyecto...
echo.
call "%~dp0COMPILAR_Y_VERIFICAR.bat"

if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo COMPILACION FALLO
    echo ========================================
    echo.
    echo Revisa los errores arriba.
    echo Si quedan bloques por arreglar, ejecuta de nuevo este script.
    echo.
    pause
    exit /b 1
)

echo.
echo ========================================
echo TODO COMPLETADO EXITOSAMENTE
echo ========================================
echo.
echo [✓] Bloques arreglados
echo [✓] Proyecto compilado
echo.
pause
