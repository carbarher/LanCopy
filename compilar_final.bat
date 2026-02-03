@echo off
echo Compilando SlskDown...
cd c:\p2p\SlskDown
dotnet build SlskDown.csproj -c Release --verbosity normal --nologo > ..\resultado_compilacion.txt 2>&1
set EXITCODE=%errorlevel%
cd ..
echo.
echo ========================================
if %EXITCODE% EQU 0 (
    echo COMPILACION EXITOSA
    echo ========================================
    echo.
    type resultado_compilacion.txt | findstr /C:"Compilaci" /C:"Tiempo" /C:"Advertencia"
) else (
    echo COMPILACION FALLIDA
    echo ========================================
    echo.
    type resultado_compilacion.txt
)
echo.
pause
