@echo off
dotnet build SlskDown.csproj -c Release 2>&1 | findstr /C:"error CS" > errores_actuales.txt
echo.
echo Errores encontrados:
type errores_actuales.txt
echo.
echo Total de lineas con error:
find /c "error CS" errores_actuales.txt
pause
