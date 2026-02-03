@echo off
cd /d c:\p2p\SlskDown
rmdir /s /q obj bin 2>nul
echo Compilando...
dotnet build SlskDown.csproj -c Debug
echo.
echo Exit code: %ERRORLEVEL%
pause
