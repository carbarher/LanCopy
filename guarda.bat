@echo off
REM Script wrapper para ejecutar guarda.ps1
REM Uso: guarda [mensaje_commit]

if "%~1"=="" (
    powershell -ExecutionPolicy Bypass -File "%~dp0guarda.ps1"
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0guarda.ps1" -mensaje "%*"
)
