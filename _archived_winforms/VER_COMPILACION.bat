@echo off
cd /d c:\p2p\SlskDown
echo Compilando...
dotnet build SlskDown.csproj -c Release > compile_result.txt 2>&1
echo.
echo ========================================
echo RESULTADO DE COMPILACION:
echo ========================================
type compile_result.txt
echo.
echo ========================================
pause
