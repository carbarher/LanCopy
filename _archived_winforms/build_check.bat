@echo off
dotnet build SlskDown.csproj -c Release > build_latest.txt 2>&1
type build_latest.txt | findstr /C:"error" /C:"Errores"
echo.
echo Compilacion terminada. Revisa build_latest.txt para detalles.
