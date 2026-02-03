@echo off
echo ========================================
echo   SlskDown - Limpiar
echo ========================================
echo.

echo Limpiando directorios bin y obj...

if exist "bin" (
    rmdir /s /q bin
    echo ✅ Directorio bin eliminado
)

if exist "obj" (
    rmdir /s /q obj
    echo ✅ Directorio obj eliminado
)

echo.
echo ✅ Limpieza completada
pause
