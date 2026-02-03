@echo off
echo ========================================
echo  DETENIENDO TODAS LAS INSTANCIAS
echo ========================================
echo.

echo [1/4] Listando procesos SlskDown...
tasklist /FI "IMAGENAME eq SlskDown.exe" 2>nul | find /I "SlskDown.exe"
if %ERRORLEVEL% EQU 0 (
    echo      ⚠️ ENCONTRADOS procesos ejecutándose
) else (
    echo      ✓ No hay procesos ejecutándose
)

echo.
echo [2/4] Cerrando TODOS los procesos SlskDown...
taskkill /F /IM SlskDown.exe 2>nul
if %ERRORLEVEL% EQU 0 (
    echo      ✓ Procesos cerrados
    timeout /t 2 /nobreak >nul
) else (
    echo      ✓ Ya estaban cerrados
)

echo.
echo [3/4] Limpiando archivos de estado...
del /Q "auto_search_state.json" 2>nul
del /Q "bin\Release\net8.0-windows\auto_search_state.json" 2>nul
del /Q "bin\Debug\net8.0-windows\auto_search_state.json" 2>nul
echo      ✓ Archivos de estado eliminados

echo.
echo [4/4] Verificando que no queden procesos...
tasklist /FI "IMAGENAME eq SlskDown.exe" 2>nul | find /I "SlskDown.exe"
if %ERRORLEVEL% EQU 0 (
    echo      ❌ ERROR: Aún hay procesos ejecutándose
    echo      Ciérralos manualmente desde el Administrador de Tareas
) else (
    echo      ✓ Todo limpio
)

echo.
echo ========================================
echo  COMPLETADO
echo ========================================
echo.
echo ✓ Todas las instancias cerradas
echo ✓ Archivos de estado eliminados
echo.
echo Ahora puedes iniciar la aplicación manualmente.
echo La búsqueda NO se iniciará automáticamente.
echo.
pause
