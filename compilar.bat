@echo off
echo ========================================
echo Compilando SlskDown...
echo ========================================
echo.

dotnet build SlskDown.sln -c Release --no-incremental

echo.
echo ========================================
echo Compilacion completada
echo ========================================
pause
