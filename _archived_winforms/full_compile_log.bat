@echo off
echo Compilando y guardando log completo...
dotnet build SlskDown.csproj -c Release > full_compile.log 2>&1
echo.
echo Log guardado en full_compile.log
echo.
echo Ultimas lineas del log:
tail -n 20 full_compile.log 2>nul || powershell -Command "Get-Content full_compile.log -Tail 20"
pause
