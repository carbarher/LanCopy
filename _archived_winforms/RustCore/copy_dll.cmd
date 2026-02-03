@echo off
echo Creando directorios si no existen...
if not exist "..\bin" mkdir "..\bin"
if not exist "..\bin\Debug" mkdir "..\bin\Debug"
if not exist "..\bin\Release" mkdir "..\bin\Release"
if not exist "..\bin\x64" mkdir "..\bin\x64"
if not exist "..\bin\x64\Debug" mkdir "..\bin\x64\Debug"
if not exist "..\bin\x64\Release" mkdir "..\bin\x64\Release"

echo.
echo Copiando DLL a todos los directorios de salida...
copy /Y target\release\slskdown_core.dll ..\bin\Debug\slskdown_core.dll
copy /Y target\release\slskdown_core.dll ..\bin\Release\slskdown_core.dll
copy /Y target\release\slskdown_core.dll ..\bin\x64\Debug\slskdown_core.dll
copy /Y target\release\slskdown_core.dll ..\bin\x64\Release\slskdown_core.dll

echo.
echo ========================================
echo DLL copiada exitosamente!
echo ========================================
echo Ubicaciones:
echo   - bin\Debug\slskdown_core.dll
echo   - bin\Release\slskdown_core.dll
echo   - bin\x64\Debug\slskdown_core.dll
echo   - bin\x64\Release\slskdown_core.dll
echo.
pause
