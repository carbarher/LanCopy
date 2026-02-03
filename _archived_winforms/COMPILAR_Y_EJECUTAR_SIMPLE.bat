@echo off
echo Matando procesos...
taskkill /F /IM SlskDown.exe 2>nul

echo Compilando...
dotnet build SlskDown.csproj -c Release --nologo --verbosity quiet

if %ERRORLEVEL% NEQ 0 (
    echo ERROR EN COMPILACION
    pause
    exit /b 1
)

echo Ejecutando...
bin\Release\net8.0-windows\SlskDown.exe

pause
