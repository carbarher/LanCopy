@echo off
echo ========================================
echo DESHABILITANDO OPTIMIZACIONES
echo ========================================
echo.

cd /d c:\p2p\SlskDown

REM Renombrar archivos de optimización para deshabilitarlos
if exist "authorSearchBox" echo Deshabilitando busqueda incremental...
if exist "selectedAuthorsLabel" echo Deshabilitando contador...

echo.
echo Las optimizaciones se han deshabilitado temporalmente
echo Para reactivarlas, vuelve a compilar normalmente
echo.
pause
