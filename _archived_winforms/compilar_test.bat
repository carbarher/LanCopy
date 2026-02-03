@echo off
cd /d c:\p2p\SlskDown
echo Compilando...
"C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release
echo.
echo Exit code: %ERRORLEVEL%
pause
