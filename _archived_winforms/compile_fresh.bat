@echo off
echo Limpiando...
dotnet clean SlskDown.csproj > nul 2>&1
echo Compilando...
dotnet build SlskDown.csproj > fresh_result.txt 2>&1
echo.
echo === ERRORES ===
findstr /C:"error CS" fresh_result.txt
echo.
echo === RESUMEN ===
findstr /C:"Errores" /C:"Advertencia" fresh_result.txt
pause
