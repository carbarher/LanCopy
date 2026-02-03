@echo off
echo.
echo ========================================
echo    EJECUTANDO SLSKDOWN
echo ========================================
echo.
cd /d C:\p2p\SlskDown
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo Ejecutable encontrado, iniciando...
    echo.
    bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo ERROR: No se encuentra el ejecutable
    echo Compilando primero...
    dotnet build SlskDown.csproj -c Release
    if exist "bin\Release\net8.0-windows\SlskDown.exe" (
        echo Compilacion exitosa, ejecutando...
        bin\Release\net8.0-windows\SlskDown.exe
    ) else (
        echo ERROR: La compilacion fallo
        pause
    )
)
