@echo off
cd /d c:\p2p\SlskDown
echo Compilando SlskDown...
dotnet build SlskDown.csproj -c Release --verbosity minimal
echo.
echo Codigo de salida: %ERRORLEVEL%
if %ERRORLEVEL% EQU 0 (
    echo ✅ Compilacion exitosa
) else (
    echo ❌ Error de compilacion
)
pause
