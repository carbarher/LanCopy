@echo off
cd /d c:\p2p\SlskDown

echo Eliminando ejecutable...
del bin\Release\net8.0-windows\SlskDown.exe 2>nul

echo Tocando MainForm.cs...
echo. >> MainForm.cs

echo Compilando...
dotnet build SlskDown.csproj -c Release --no-incremental

echo.
echo Verificando resultado:
cd bin\Release\net8.0-windows
dir SlskDown.exe

echo.
echo Hora actual: %time%
pause
