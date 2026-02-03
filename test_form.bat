@echo off
echo Limpiando archivos de log antiguos...
del /q "SlskDown\bin\Release\net9.0-windows\*.txt" 2>nul
del /q "SlskDown\bin\Release\net9.0-windows\constructor_*.txt" 2>nul
del /q "SlskDown\bin\Release\net9.0-windows\close_attempt_*.txt" 2>nul
del /q "SlskDown\bin\Release\net9.0-windows\program_start_*.txt" 2>nul

echo.
echo Compilando...
dotnet build SlskDown\SlskDown.csproj -c Release

echo.
echo Ejecutando SlskDown...
cd SlskDown\bin\Release\net9.0-windows
SlskDown.exe

echo.
echo Mostrando archivos de log generados:
dir /b *.txt 2>nul

pause
