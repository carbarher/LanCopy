@echo off
cd /d c:\p2p\SlskDownAvalonia

echo Compilando...
dotnet build -c Release

echo.
echo Ejecutando con dotnet run...
dotnet run -c Release

pause
