@echo off
echo ========================================
echo   PRUEBA RAPIDA DE CARGA
echo ========================================
echo.
echo Ejecutando prueba rapida (5 busquedas, 30 segundos)...
echo.

REM Ejecutar prueba con credenciales desde config.json
dotnet run --project StressTestRunner.csproj -- carbar Carlos66*

pause
