@echo off
echo ========================================
echo Compilando Rust DLL con MSVC
echo ========================================
echo.

REM Configurar entorno MSVC
echo [1] Configurando entorno MSVC...
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: No se pudo configurar entorno MSVC
    pause
    exit /b 1
)
echo OK - Entorno MSVC configurado
echo.

REM Verificar linker
echo [2] Verificando linker MSVC...
where link.exe
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: link.exe no encontrado
    pause
    exit /b 1
)
echo OK - Linker encontrado
echo.

REM Limpiar build anterior
echo [3] Limpiando build anterior...
cargo clean
echo OK - Build limpiado
echo.

REM Compilar con verbose
echo [4] Compilando Rust DLL...
cargo build --release -vv
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Compilacion fallida
    pause
    exit /b 1
)
echo OK - Compilacion exitosa
echo.

REM Verificar DLL generada
echo [5] Verificando DLL generada...
if exist "target\release\slskdown_core.dll" (
    echo OK - DLL GENERADA: target\release\slskdown_core.dll
    dir target\release\slskdown_core.dll
) else (
    echo ERROR: DLL NO GENERADA
    echo.
    echo Buscando en deps...
    dir /s /b target\release\deps\slskdown_core.dll 2>nul
    if %ERRORLEVEL% NEQ 0 (
        echo DLL no encontrada en deps tampoco
    )
)
echo.

echo ========================================
echo PROCESO COMPLETADO
echo ========================================
pause
