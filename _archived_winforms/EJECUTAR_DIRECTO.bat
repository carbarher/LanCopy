@echo off
echo Limpiando...
dotnet clean SlskDown.csproj

echo.
echo Compilando...
dotnet build SlskDown.csproj -c Release

echo.
echo Ejecutando...
dotnet run --project SlskDown.csproj

pause
