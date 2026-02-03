@echo off
echo === DIAGNOSTICO SLSKDOWN ===
echo.

echo [1] Verificando ejecutable...
if exist "SlskDown.exe" (
    echo [OK] SlskDown.exe encontrado
    dir SlskDown.exe | findstr "SlskDown.exe"
) else (
    echo [ERROR] SlskDown.exe NO encontrado
)
echo.

echo [2] Verificando DLL...
if exist "SlskDown.dll" (
    echo [OK] SlskDown.dll encontrado
    dir SlskDown.dll | findstr "SlskDown.dll"
) else (
    echo [ERROR] SlskDown.dll NO encontrado
)
echo.

echo [3] Verificando configuracion...
if exist "SlskDown\config.json" (
    echo [OK] config.json encontrado
) else (
    echo [ERROR] config.json NO encontrado
)
echo.

echo [4] Ultimo log de ejecucion...
for /f %%i in ('dir /b /o-d SlskDown\lanza_last_run_*.log 2^>nul ^| findstr /n "^" ^| findstr "^1:"') do (
    set LASTLOG=%%i
)
if defined LASTLOG (
    echo [OK] Log encontrado: %LASTLOG:~2%
) else (
    echo [INFO] No hay logs de ejecucion
)
echo.

echo [5] Compilando proyecto...
msbuild SlskDown\SlskDown.csproj /t:Build /p:Configuration=Debug /nologo /v:q > compile_output.txt 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Compilacion exitosa
) else (
    echo [ERROR] Compilacion fallida - ver compile_output.txt
    type compile_output.txt
)
echo.

echo [6] Estructura del proyecto...
echo Archivos .cs en raiz:
dir /b SlskDown\*.cs 2>nul | find /c ".cs"
echo Archivos en Core:
dir /b /s SlskDown\Core\*.cs 2>nul | find /c ".cs"
echo Archivos en Models:
dir /b /s SlskDown\Models\*.cs 2>nul | find /c ".cs"
echo.

echo === FIN DIAGNOSTICO ===
