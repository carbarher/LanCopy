@echo off
echo ========================================
echo Ejecutando Tests de Integracion eMule
echo ========================================
echo.

echo [1/3] Compilando tests...
dotnet build EMule\Tests\EMuleClientTests.cs -o EMule\Tests\bin 2>nul
if %errorlevel% neq 0 (
    echo ❌ Error compilando EMuleClientTests
    goto :error
)

dotnet build EMule\Tests\EMuleDownloadTests.cs -o EMule\Tests\bin 2>nul
if %errorlevel% neq 0 (
    echo ❌ Error compilando EMuleDownloadTests
    goto :error
)

echo ✅ Tests compilados correctamente
echo.

echo [2/3] Verificando archivos de test...
if exist "EMule\Tests\EMuleClientTests.cs" (
    echo ✅ EMuleClientTests.cs encontrado
) else (
    echo ❌ EMuleClientTests.cs no encontrado
    goto :error
)

if exist "EMule\Tests\EMuleDownloadTests.cs" (
    echo ✅ EMuleDownloadTests.cs encontrado
) else (
    echo ❌ EMuleDownloadTests.cs no encontrado
    goto :error
)

echo.
echo [3/3] Tests disponibles:
echo.
echo   1. EMuleClientTests     - Tests de conexion y autenticacion
echo   2. EMuleDownloadTests   - Tests de descargas
echo.
echo ========================================
echo NOTA: Los tests requieren aMule daemon
echo       corriendo en localhost:4712
echo ========================================
echo.
echo Para ejecutar tests manualmente:
echo   dotnet run --project EMule\Tests\EMuleClientTests.cs
echo   dotnet run --project EMule\Tests\EMuleDownloadTests.cs
echo.
goto :end

:error
echo.
echo ========================================
echo ❌ Error en preparacion de tests
echo ========================================
exit /b 1

:end
echo ========================================
echo ✅ Tests listos para ejecutar
echo ========================================
