@echo off
echo === VERIFICANDO INSTALACIÓN DE VPN ===
echo.
echo 1. Buscando Windscribe en Program Files...
if exist "C:\Program Files\Windscribe\windscribe-cli.exe" (
    echo ✅ Windscribe CLI encontrado en: C:\Program Files\Windscribe\windscribe-cli.exe
) else (
    echo ❌ Windscribe CLI NO encontrado en C:\Program Files\Windscribe\
)
echo.
echo 2. Buscando Windscribe en Program Files (x86)...
if exist "C:\Program Files (x86)\Windscribe\windscribe-cli.exe" (
    echo ✅ Windscribe CLI encontrado en: C:\Program Files (x86)\Windscribe\windscribe-cli.exe
) else (
    echo ❌ Windscribe CLI NO encontrado en C:\Program Files (x86)\Windscribe\
)
echo.
echo 3. Verificando si windscribe está en PATH...
where windscribe-cli >nul 2>&1
if %errorlevel% equ 0 (
    echo ✅ Windscribe CLI disponible en PATH
    where windscribe-cli
) else (
    echo ❌ Windscribe CLI NO disponible en PATH
)
echo.
echo 4. Listando carpetas de Windscribe...
dir "C:\Program Files\Windscribe*" /b 2>nul
dir "C:\Program Files (x86)\Windscribe*" /b 2>nul
echo.
pause
