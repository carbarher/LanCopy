@echo off
echo ========================================
echo   PRUEBA RÁPIDA DE COMPILACIÓN .NET
echo ========================================
echo.

echo [1] Verificando instalación de .NET...
echo =======================================
dotnet --version
if %ERRORLEVEL% NEQ 0 (
    echo ❌ ERROR: .NET no está instalado o no está en PATH
    pause
    exit /b 1
)
echo ✅ .NET SDK detectado
echo.

echo [2] Verificando variables de entorno...
echo =========================================
echo DOTNET_ROOT: %DOTNET_ROOT%
if "%DOTNET_ROOT%"=="" (
    echo ⚠️  ADVERTENCIA: DOTNET_ROOT no está configurado
)
echo.

echo [3] Creando proyecto de prueba...
echo ==================================
mkdir test_compilation 2>nul
cd test_compilation

echo Creando proyecto Console...
dotnet new console --force >nul 2>&1

echo [4] Compilando proyecto de prueba...
echo =====================================
dotnet build --verbosity minimal

if %ERRORLEVEL% EQU 0 (
    echo ✅ Compilación de prueba exitosa
    echo.
    echo Verificando ejecutable generado...
    dir bin\Debug\net8.0\*.exe >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo ✅ Ejecutable generado correctamente
        for %%f in (bin\Debug\net8.0\*.exe) do echo    Archivo: %%f
    ) else (
        echo ❌ No se generó ejecutable en proyecto de prueba
    )
) else (
    echo ❌ ERROR: Falló la compilación del proyecto de prueba
)

echo.
echo [5] Probando con SlskDown...
echo ============================
cd ..\SlskDown

echo Compilando SlskDown...
dotnet build SlskDown.csproj --verbosity minimal

if %ERRORLEVEL% EQU 0 (
    echo ✅ SlskDown compilado exitosamente
    echo.
    echo Verificando ejecutable de SlskDown...
    dir bin\Debug\net8.0-windows\*.exe >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo ✅ EJECUTABLE SlskDown GENERADO
        for %%f in (bin\Debug\net8.0-windows\*.exe) do echo    Archivo: %%f
        
        echo.
        echo 🎉 ¡ÉXITO COMPLETO! El entorno .NET funciona correctamente
    ) else (
        echo ❌ SlskDown compiló pero no generó ejecutable
        echo    Esto puede indicar un problema específico del proyecto
    )
) else (
    echo ❌ ERROR: SlskDown no pudo compilarse
    echo    Revisa los errores de compilación arriba
)

echo.
echo [6] Resumen final...
echo ===================
cd ..
rmdir /s /q test_compilation 2>nul

echo Prueba de compilación finalizada. Revisa los resultados arriba.
echo.
pause
