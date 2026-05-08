@echo off
setlocal
if "%~1"=="" (
  echo Uso: probar_audiveris.bat ^<archivo.pdf^> [ruta_audiveris]
  exit /b 1
)

set "INPUT=%~1"
set "AUDI=%~2"

if "%AUDI%"=="" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0probar_audiveris.ps1" -InputPath "%INPUT%"
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0probar_audiveris.ps1" -InputPath "%INPUT%" -AudiverisExe "%AUDI%"
)
