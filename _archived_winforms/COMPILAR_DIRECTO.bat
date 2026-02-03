@echo off
echo ========================================
echo COMPILACION DIRECTA
echo ========================================

cd /d c:\p2p\SlskDown

echo [1/5] Eliminando ejecutable...
del bin\Release\net8.0-windows\SlskDown.exe 2>nul

echo [2/5] Eliminando bin y obj...
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul

echo [3/5] Modificando MainForm.cs...
echo // FORCE REBUILD %time% >> MainForm.cs

echo [4/5] Compilando...
dotnet build SlskDown.csproj -c Release --no-incremental

echo.
echo [5/5] Verificando...
if exist bin\Release\net8.0-windows\SlskDown.exe (
    echo ========================================
    echo EXITO - Ejecutable creado
    dir bin\Release\net8.0-windows\SlskDown.exe
    echo ========================================
) else (
    echo ========================================
    echo ERROR - No se creo el ejecutable
    echo ========================================
)

pause
