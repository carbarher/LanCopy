@echo off
cd /d C:\p2p\SlskDown
echo Compilando SlskDown...
msbuild SlskDown.csproj /t:Build /p:Configuration=Debug /v:minimal /nologo
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo COMPILACION EXITOSA
    echo ========================================
) else (
    echo.
    echo ========================================
    echo ERRORES DE COMPILACION
    echo ========================================
)
pause
