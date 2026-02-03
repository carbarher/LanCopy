@echo off
chcp 65001 >nul
echo ========================================
echo COMPILACIÓN POST-REINICIO
echo ========================================
echo.
echo Limpiando caché...
rmdir /S /Q bin 2>nul
rmdir /S /Q obj 2>nul
echo ✅ Caché limpiada
echo.
echo Compilando proyecto...
echo.
msbuild SlskDown.csproj -t:Rebuild -p:Configuration=Release -v:minimal > compile_post_reinicio.txt 2>&1

echo.
echo ========================================
echo RESULTADO:
echo ========================================
type compile_post_reinicio.txt | findstr /C:"error" /C:"Compilación" /C:"correcta" /C:"Errores"
echo.
echo ========================================
echo Archivo completo guardado en: compile_post_reinicio.txt
echo ========================================
pause
