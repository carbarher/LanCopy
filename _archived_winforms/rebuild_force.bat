@echo off
echo Matando proceso SlskDown...
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 1 /nobreak >nul

echo Limpiando...
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul

echo Compilando...
dotnet build -c Release

echo.
echo Listo! Ejecuta: c:\p2p\slsk.bat
pause
