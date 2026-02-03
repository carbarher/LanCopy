@echo off
cd /d c:\p2p\SlskDown

echo ========================================
echo COMPILANDO SLSKDOWN
echo ========================================
echo.

if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

"C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release > compile_output.txt 2>&1

echo.
echo Resultado de compilacion guardado en compile_output.txt
echo.

if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ========================================
    echo COMPILACION EXITOSA
    echo ========================================
    echo.
    dir "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo Presiona cualquier tecla para ejecutar...
    pause > nul
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo ========================================
    echo ERROR EN COMPILACION
    echo ========================================
    echo.
    echo Ver detalles en compile_output.txt
    type compile_output.txt
    echo.
    pause
)
