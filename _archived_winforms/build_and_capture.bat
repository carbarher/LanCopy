@echo off
echo Compilando proyecto...
dotnet build --no-incremental > build_output.log 2>&1
echo.
echo Buscando errores CS...
findstr /C:"error CS" build_output.log > errors_cs.log
echo.
echo === ERRORES ENCONTRADOS ===
type errors_cs.log
echo.
echo === RESUMEN ===
findstr /C:"Errores" build_output.log
pause
