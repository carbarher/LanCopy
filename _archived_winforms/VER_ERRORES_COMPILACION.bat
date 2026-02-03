@echo off
echo Compilando y extrayendo errores...

dotnet build SlskDown.csproj -c Release > temp_build.txt 2>&1

echo.
echo ========== ERRORES ==========
findstr /i "error CS" temp_build.txt > errores.txt

if %ERRORLEVEL% EQU 0 (
    type errores.txt
    echo.
    echo Errores guardados en: errores.txt
    notepad errores.txt
) else (
    echo No se encontraron errores de compilacion
    echo.
    echo Verificando si se genero el ejecutable...
    if exist "bin\Release\net8.0-windows\SlskDown_NEW.exe" (
        echo ✓ Compilacion exitosa
        echo Ejecutando...
        start bin\Release\net8.0-windows\SlskDown_NEW.exe
    ) else (
        echo Compilacion exitosa pero no se genero .exe
        echo Ejecutando con dotnet run...
        start dotnet run --project SlskDown.csproj
    )
)

del temp_build.txt
pause
