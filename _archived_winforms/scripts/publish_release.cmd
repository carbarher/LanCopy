@echo off
setlocal enabledelayedexpansion

REM Publish Release + package ZIP

set CONFIG=Release
set RID=win-x64
set OUTDIR=%~dp0..\artifacts\publish\%RID%
set ZIPDIR=%~dp0..\artifacts
set APPNAME=SlskDown

if not exist "%~dp0..\artifacts" mkdir "%~dp0..\artifacts"
if not exist "%~dp0..\artifacts\publish" mkdir "%~dp0..\artifacts\publish"

echo.
echo Publishing %APPNAME% (%CONFIG%, %RID%)...
echo Output: %OUTDIR%
echo.

dotnet publish "%~dp0..\SlskDown.csproj" -c %CONFIG% -r %RID% --self-contained false -p:PublishSingleFile=false -o "%OUTDIR%"
if errorlevel 1 (
  echo.
  echo ERROR: dotnet publish failed.
  exit /b 1
)

for /f "tokens=1-3 delims=/ " %%a in ("%date%") do set TODAY=%%c-%%a-%%b
set ZIP=%ZIPDIR%\%APPNAME%_%RID%_%CONFIG%_%TODAY%.zip

if exist "%ZIP%" del /f /q "%ZIP%"

echo.
echo Creating ZIP: %ZIP%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%OUTDIR%\*' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 (
  echo.
  echo ERROR: ZIP creation failed.
  exit /b 1
)

echo.
echo Done.
echo Published files: %OUTDIR%
echo ZIP: %ZIP%
