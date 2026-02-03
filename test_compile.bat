@echo off
cd /d C:\p2p\SlskDown
echo Compilando SlskDown...
echo.
msbuild SlskDown.csproj /t:Build /p:Configuration=Debug /v:minimal /nologo 2>&1 | findstr /C:"error" /C:"Errores" /C:"Advertencia" /C:"exitosa"
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
