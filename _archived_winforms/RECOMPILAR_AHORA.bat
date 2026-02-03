@echo off
cd /d c:\p2p\SlskDown

echo ========================================
echo LIMPIANDO TODO...
echo ========================================
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul
del *.dll 2>nul
del *.exe 2>nul

echo.
echo ========================================
echo COMPILANDO DESDE CERO...
echo ========================================
dotnet clean
dotnet restore
dotnet build SlskDown.csproj -c Release --no-incremental

echo.
echo ========================================
echo VERIFICANDO RESULTADO...
echo ========================================
cd bin\Release\net8.0-windows
dir SlskDown.exe

echo.
echo ========================================
echo HORA ACTUAL: %time%
echo ========================================
pause
