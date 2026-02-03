@echo off
echo ========================================
echo DIAGNOSTICO SLSKDOWN
echo ========================================
echo.

cd /d c:\p2p\SlskDown\bin\Release\net9.0-windows

echo Limpiando logs antiguos...
del /q constructor_*.txt 2>nul
del /q close_attempt_*.txt 2>nul
del /q program_start_*.txt 2>nul
del /q mainform_error.txt 2>nul
echo.

echo Ejecutando SlskDown...
start /wait SlskDown.exe

echo.
echo ========================================
echo LOGS GENERADOS:
echo ========================================
dir /b *.txt 2>nul | findstr /v "Data canonical gutenberg startup"

echo.
echo ========================================
echo CONTENIDO DE LOGS:
echo ========================================

if exist constructor_*.txt (
    echo.
    echo === CONSTRUCTOR LOG ===
    type constructor_*.txt
)

if exist close_attempt_*.txt (
    echo.
    echo === CLOSE ATTEMPT LOG ===
    type close_attempt_*.txt
)

if exist program_start_*.txt (
    echo.
    echo === PROGRAM START LOG (ultimas 10 lineas) ===
    powershell -Command "Get-Content program_start_*.txt -Tail 10"
)

if exist mainform_error.txt (
    echo.
    echo === MAINFORM ERROR ===
    type mainform_error.txt
)

echo.
pause
