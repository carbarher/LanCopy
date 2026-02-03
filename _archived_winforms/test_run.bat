@echo off
echo ========================================
echo Testing SlskDown Execution
echo ========================================
echo.

cd bin\Release\net8.0-windows

echo Current directory:
cd
echo.

echo Files in directory:
dir SlskDown.exe SlskDown.dll
echo.

echo Attempting to run with dotnet...
dotnet SlskDown.dll
echo Exit code: %ERRORLEVEL%
echo.

echo Checking for log files...
dir *.txt 2>nul
echo.

echo Attempting to run EXE directly...
SlskDown.exe
echo Exit code: %ERRORLEVEL%
echo.

pause
