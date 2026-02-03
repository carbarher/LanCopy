@echo off
echo Compilando SlskDown...
dotnet build -c Release 2>&1
echo.
echo Exit code: %ERRORLEVEL%
pause
