@echo off
echo === INFORMACION DE MEMORIA RAM ===
wmic computersystem get TotalPhysicalMemory /value | findstr TotalPhysicalMemory
wmic OS get FreePhysicalMemory /value | findstr FreePhysicalMemory
echo.
echo Procesando valores...
for /f "tokens=2 delims==" %%a in ('wmic computersystem get TotalPhysicalMemory /value ^| findstr TotalPhysicalMemory') do set TOTAL=%%a
for /f "tokens=2 delims==" %%a in ('wmic OS get FreePhysicalMemory /value ^| findstr FreePhysicalMemory') do set FREE=%%a
set /a TOTAL_GB=%TOTAL%/1024/1024/1024
set /a FREE_GB=%FREE%/1024
set /a USED_GB=%TOTAL_GB%-%FREE_GB%
echo Total: %TOTAL_GB% GB
echo Libre: %FREE_GB% MB ^(aprox %FREE_GB%/1024 GB^)
echo Usada: %USED_GB% GB
pause
