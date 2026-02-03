@echo off
echo Compilando SlskDown con MSBuild...
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" SlskDown.csproj /p:Configuration=Release /v:minimal /t:Rebuild
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo ===== COMPILACION EXITOSA =====
    echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
    dir bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo.
    echo ===== ERROR: No se genero el ejecutable =====
)
pause
