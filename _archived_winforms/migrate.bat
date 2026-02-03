@echo off
echo ============================================
echo SlskDown - Migracion de Credenciales
echo ============================================
echo.

cd /d "%~dp0"

echo Compilando script de migracion...
csc /out:MigrateToSecure.exe /r:System.Security.Cryptography.ProtectedData.dll MigrateToSecure.cs Services\SecurityService.cs Services\ISecurityService.cs Services\ConfigService.cs Services\IConfigService.cs Services\ServiceContainer.cs

if errorlevel 1 (
    echo.
    echo Error al compilar. Intentando con dotnet...
    echo.
    
    REM Crear proyecto temporal
    dotnet new console -n MigrateTool -o MigrateTool --force
    
    REM Copiar archivos
    copy MigrateToSecure.cs MigrateTool\Program.cs /Y
    xcopy Services MigrateTool\Services\ /E /I /Y
    
    REM Agregar referencia a System.Security
    cd MigrateTool
    dotnet add package System.Security.Cryptography.ProtectedData
    
    REM Ejecutar
    dotnet run
    
    cd ..
    
    REM Limpiar
    rmdir /s /q MigrateTool
) else (
    echo.
    echo Compilacion exitosa. Ejecutando...
    echo.
    MigrateToSecure.exe
    
    echo.
    echo Presiona cualquier tecla para salir...
    pause > nul
)
