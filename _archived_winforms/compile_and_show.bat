@echo off
echo === Compilando SlskDown ===
msbuild SlskDown.csproj /t:Build /v:minimal /nologo > compile_result.txt 2>&1
type compile_result.txt
echo.
echo === Fin de compilacion ===
