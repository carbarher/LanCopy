@echo off
echo ========================================
echo VERIFICACION FINAL DE COMPILACION
echo ========================================
echo.

echo [1/3] Limpiando proyecto...
dotnet clean > nul 2>&1

echo [2/3] Compilando proyecto...
dotnet build --configuration Release --no-incremental > compile_final_log.txt 2>&1

echo [3/3] Analizando resultado...
echo.

findstr /C:"Build succeeded" compile_final_log.txt > nul
if %errorlevel% == 0 (
    echo ========================================
    echo    ✅ COMPILACION EXITOSA
    echo ========================================
    echo.
    
    for /f "tokens=*" %%a in ('findstr /C:"Warning(s)" compile_final_log.txt') do echo %%a
    echo.
    
    if exist "bin\Release\net8.0-windows\SlskDown.exe" (
        echo ✅ Ejecutable generado: bin\Release\net8.0-windows\SlskDown.exe
        for %%I in ("bin\Release\net8.0-windows\SlskDown.exe") do echo    Tamaño: %%~zI bytes
        echo.
    ) else (
        echo ⚠️ Advertencia: Ejecutable no encontrado
    )
    
    echo ========================================
    echo COMPILACION COMPLETADA AL 100%%
    echo ========================================
) else (
    findstr /C:"Build FAILED" compile_final_log.txt > nul
    if %errorlevel% == 0 (
        echo ========================================
        echo    ❌ COMPILACION FALLIDA
        echo ========================================
        echo.
        
        echo Contando errores...
        findstr /C:"error CS" compile_final_log.txt > errors_list.txt
        for /f %%a in ('find /c /v "" ^< errors_list.txt') do set errorcount=%%a
        echo Total de errores: %errorcount%
        echo.
        
        echo Primeros 10 errores:
        echo ----------------------------------------
        for /f "tokens=*" %%a in ('findstr /C:"error CS" compile_final_log.txt ^| more +0') do (
            echo %%a
        )
        echo ----------------------------------------
    ) else (
        echo ========================================
        echo    ⚠️ ESTADO DESCONOCIDO
        echo ========================================
        echo.
        echo Revisa compile_final_log.txt para detalles
    )
)

echo.
echo Log completo guardado en: compile_final_log.txt
echo.
pause
