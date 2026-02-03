@echo off
echo ========================================
echo COMPILACION Y VERIFICACION
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1] Eliminando binarios antiguos...
del /F /Q bin\Release\net8.0-windows\SlskDown_NEW.exe 2>nul
del /F /Q bin\Release\net8.0-windows\SlskDown_NEW.dll 2>nul

echo [2] Compilando...
dotnet build SlskDown.csproj -c Release --no-incremental

echo.
echo [3] Verificando resultado...
if exist "bin\Release\net8.0-windows\SlskDown_NEW.exe" (
    echo [OK] Ejecutable generado
    dir bin\Release\net8.0-windows\SlskDown_NEW.exe
) else (
    echo [ERROR] No se genero el ejecutable
)

echo.
echo [4] Verificando DLL...
if exist "bin\Release\net8.0-windows\SlskDown_NEW.dll" (
    echo [OK] DLL generada
    dir bin\Release\net8.0-windows\SlskDown_NEW.dll
) else (
    echo [ERROR] No se genero la DLL
)

echo.
echo ========================================
echo Presiona cualquier tecla para continuar
pause >nul
