@echo off
cd /d c:\p2p\SlskDown
echo Compilando...
"C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release > compile_result.txt 2>&1
echo.
echo === RESULTADO ===
type compile_result.txt
echo.
echo === BUSCANDO EJECUTABLE ===
dir /s /b bin\*.exe 2>nul
dir /s /b bin\*.dll 2>nul
pause
