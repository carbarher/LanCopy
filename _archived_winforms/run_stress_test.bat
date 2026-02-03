@echo off
echo ========================================
echo   EJECUTANDO PRUEBA DE CARGA
echo ========================================
echo.

REM Verificar si se proporcionó una URL como argumento
if "%1"=="" (
    dotnet run --project StressTestRunner.csproj
) else (
    dotnet run --project StressTestRunner.csproj %1
)

pause
