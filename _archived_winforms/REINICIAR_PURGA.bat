@echo off
title Reiniciar Purga desde Cero
color 0C

echo ========================================
echo   REINICIAR PURGA DESDE CERO
echo ========================================
echo.
echo Este script eliminará todos los archivos
echo de progreso de purga anterior.
echo.
echo ⚠️ ADVERTENCIA: Se perderá el progreso
echo de cualquier purga en curso.
echo.
pause

cd /d c:\p2p\SlskDown

echo.
echo Eliminando archivos de progreso...
echo.

if exist purge_progress.txt (
    del /F purge_progress.txt
    echo ✅ purge_progress.txt eliminado
) else (
    echo ⚠️ purge_progress.txt no existe
)

if exist batch_progress.json (
    del /F batch_progress.json
    echo ✅ batch_progress.json eliminado
) else (
    echo ⚠️ batch_progress.json no existe
)

if exist search_cache.json (
    echo.
    echo ¿Eliminar también el caché de búsquedas?
    echo (Esto hará que todas las búsquedas se hagan de nuevo)
    echo.
    set /p ELIMINAR_CACHE="Eliminar caché? (S/N): "
    if /i "%ELIMINAR_CACHE%"=="S" (
        del /F search_cache.json
        echo ✅ search_cache.json eliminado
    ) else (
        echo ⏭️ Caché conservado (se reutilizarán búsquedas anteriores)
    )
) else (
    echo ⚠️ search_cache.json no existe
)

echo.
echo ========================================
echo   ARCHIVOS DE PROGRESO ELIMINADOS
echo ========================================
echo.
echo ⚠️ IMPORTANTE: Para que la purga empiece
echo desde el autor #1, debes CERRAR SlskDown
echo si está abierto y abrirlo de nuevo.
echo.
echo La próxima purga comenzará desde cero.
echo.
echo Para iniciar la purga:
echo 1. CIERRA SlskDown si está abierto
echo 2. Ejecuta SlskDown de nuevo
echo 3. Ve a pestaña "🤖 Automático"
echo 4. Haz clic en "🗑️ Purga"
echo.
echo El caché en memoria se limpiará al reiniciar.
echo.
pause
