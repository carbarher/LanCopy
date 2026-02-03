@echo off
chcp 65001 >nul
echo.
echo ========================================
echo    DIAGNÓSTICO COMPLETO SLSKDOWN
echo ========================================
echo.

echo [1] COMPILACIÓN
echo ----------------------------------------
dotnet build SlskDown\SlskDown.csproj -v:q --no-incremental > compile_diag.txt 2>&1
if %ERRORLEVEL% EQU 0 (
    echo ✅ Compilación exitosa
) else (
    echo ❌ Error de compilación
    type compile_diag.txt
)
echo.

echo [2] EJECUTABLE
echo ----------------------------------------
if exist "SlskDown.exe" (
    echo ✅ SlskDown.exe encontrado
    for %%F in (SlskDown.exe) do echo    Tamaño: %%~zF bytes
    for %%F in (SlskDown.exe) do echo    Fecha: %%~tF
) else (
    echo ❌ SlskDown.exe NO encontrado
)
echo.

echo [3] ÚLTIMO LOG
echo ----------------------------------------
for /f "delims=" %%i in ('dir /b /o-d SlskDown\lanza_last_run_*.log 2^>nul ^| findstr /n "^" ^| findstr "^1:"') do (
    set LASTLOG=%%i
    set LASTLOG=!LASTLOG:~2!
)
if defined LASTLOG (
    echo Último log: %LASTLOG%
    for %%F in (SlskDown\%LASTLOG%) do echo Tamaño: %%~zF bytes
) else (
    echo ❌ No hay logs
)
echo.

echo [4] ESTRUCTURA PROYECTO
echo ----------------------------------------
echo MainForm.cs:
for %%F in (SlskDown\MainForm.cs) do echo    %%~zF bytes
echo.
echo Archivos Core:
dir /s /b SlskDown\Core\*.cs 2>nul | find /c ".cs"
echo.
echo Archivos Models:
dir /s /b SlskDown\Models\*.cs 2>nul | find /c ".cs"
echo.

echo [5] CONFIGURACIÓN
echo ----------------------------------------
if exist "SlskDown\config.json" (
    echo ✅ config.json encontrado
) else (
    echo ❌ config.json NO encontrado
)
echo.

echo ========================================
echo    FIN DIAGNÓSTICO
echo ========================================
