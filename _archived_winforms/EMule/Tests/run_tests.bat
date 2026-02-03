@echo off
REM Script para compilar y ejecutar tests de integración eMule
REM Requiere: .NET SDK instalado y amuled corriendo

echo ========================================
echo Tests de Integracion eMule - SlskDown
echo ========================================
echo.

REM Verificar que amuled está corriendo
echo Verificando si amuled esta corriendo...
tasklist /FI "IMAGENAME eq amuled.exe" 2>NUL | find /I /N "amuled.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [OK] amuled esta corriendo
) else (
    echo [ADVERTENCIA] amuled no esta corriendo
    echo Por favor inicia amuled antes de ejecutar los tests
    echo.
    echo Presiona cualquier tecla para continuar de todos modos...
    pause >nul
)

echo.
echo Compilando proyecto de tests...
cd /d "%~dp0"

REM Crear archivo de proyecto temporal si no existe
if not exist "EMuleClientTests.csproj" (
    echo Creando archivo de proyecto...
    (
        echo ^<Project Sdk="Microsoft.NET.Sdk"^>
        echo   ^<PropertyGroup^>
        echo     ^<OutputType^>Exe^</OutputType^>
        echo     ^<TargetFramework^>net6.0^</TargetFramework^>
        echo     ^<RootNamespace^>SlskDown.EMule.Tests^</RootNamespace^>
        echo   ^</PropertyGroup^>
        echo   ^<ItemGroup^>
        echo     ^<ProjectReference Include="..\..\SlskDown.csproj" /^>
        echo   ^</ItemGroup^>
        echo ^</Project^>
    ) > EMuleClientTests.csproj
)

REM Compilar
dotnet build EMuleClientTests.csproj -c Release
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Fallo la compilacion
    pause
    exit /b 1
)

echo.
echo ========================================
echo Ejecutando tests...
echo ========================================
echo.

REM Ejecutar tests
dotnet run --project EMuleClientTests.csproj -c Release

echo.
echo ========================================
echo Tests finalizados
echo ========================================
pause
