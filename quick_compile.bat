@echo off
cd /d C:\p2p\SlskDown
msbuild SlskDown.csproj /t:Build /p:Configuration=Debug /v:q /nologo 2>&1 | find /C "error" > nul
if %ERRORLEVEL% EQU 0 (
    echo ✅ COMPILACION EXITOSA - Sin errores
) else (
    echo ❌ ERRORES DE COMPILACION
    msbuild SlskDown.csproj /t:Build /p:Configuration=Debug /v:minimal /nologo 2>&1 | findstr "error CS"
)
