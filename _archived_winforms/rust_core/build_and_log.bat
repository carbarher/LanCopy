@echo off
echo === Compilando Rust DLL con logging === > build_log.txt
echo. >> build_log.txt

echo Limpiando build anterior... >> build_log.txt
cargo clean >> build_log.txt 2>&1

echo. >> build_log.txt
echo Compilando con verbose... >> build_log.txt
cargo build --release --verbose >> build_log.txt 2>&1

echo. >> build_log.txt
echo Exit code: %ERRORLEVEL% >> build_log.txt

echo. >> build_log.txt
echo === Verificando archivos === >> build_log.txt
if exist "target\release\slskdown_core.dll" (
    echo [OK] DLL encontrada en target\release\slskdown_core.dll >> build_log.txt
    dir "target\release\slskdown_core.dll" >> build_log.txt
) else (
    echo [ERROR] DLL NO encontrada en target\release\ >> build_log.txt
)

if exist "target\release\deps\slskdown_core.dll" (
    echo [OK] DLL encontrada en target\release\deps\slskdown_core.dll >> build_log.txt
    dir "target\release\deps\slskdown_core.dll" >> build_log.txt
) else (
    echo [ERROR] DLL NO encontrada en target\release\deps\ >> build_log.txt
)

echo. >> build_log.txt
echo Archivos en target\release: >> build_log.txt
dir /b "target\release\*.dll" >> build_log.txt 2>&1

echo. >> build_log.txt
echo Archivos slskdown en deps: >> build_log.txt
dir /b "target\release\deps\slskdown*" >> build_log.txt 2>&1

echo === Log guardado en build_log.txt ===
type build_log.txt
