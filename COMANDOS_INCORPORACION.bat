@echo off
echo ======================================================================
echo INCORPORACION DE LIBROS PRE-1900 A c:\p2p\downloads
echo ======================================================================
echo.

echo Ejecutando script de incorporacion...
echo.

python c:\p2p\scripts\incorporar_a_downloads.py

echo.
echo ======================================================================
echo PROCESO COMPLETADO
echo ======================================================================
echo.
echo Revisa el log generado en c:\p2p\downloads\incorporacion_*.log
echo.
pause
