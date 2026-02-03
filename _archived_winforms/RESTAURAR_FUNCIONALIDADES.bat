@echo off
echo ========================================
echo RESTAURANDO FUNCIONALIDADES AVANZADAS
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo 1. Haciendo backup del MainForm.cs actual...
copy /Y MainForm.cs MainForm.cs_basico_backup_2 >nul

echo 2. Restaurando MainForm.cs completo (8520 líneas)...
copy /Y MainForm.cs_backup_completo MainForm.cs >nul

echo 3. Verificando tamaño del archivo...
for %%A in (MainForm.cs) do echo    Tamaño: %%~zA bytes

echo.
echo 4. Limpiando binarios anteriores...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo.
echo 5. Compilando con funcionalidades avanzadas...
echo.
"C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release

echo.
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ COMPILACION EXITOSA
    echo.
    echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Funcionalidades restauradas:
    echo   - 16 funciones completas
    echo   - Filtros avanzados
    echo   - Watchlist y Blacklist
    echo   - Modo incógnito
    echo   - Auto-descarga
    echo   - Búsqueda múltiple
    echo   - Búsqueda por autores
    echo   - Logging avanzado
    echo.
    choice /C SN /M "¿Ejecutar ahora?"
    if errorlevel 2 goto :fin
    if errorlevel 1 start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo ❌ ERROR EN COMPILACION
    echo.
    echo Revisa los errores arriba.
    echo Si hay errores, ejecuta:
    echo   MainForm.cs_basico_backup_2 para volver a la versión básica
)

:fin
echo.
pause
