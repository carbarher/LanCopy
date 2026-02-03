@echo off
setlocal enabledelayedexpansion

echo ============================================
echo SlskDown - Migracion de Credenciales
echo ============================================
echo.

cd /d "%~dp0"

REM Verificar si existe config_secure.json
if exist "config_secure.json" (
    echo [OK] Ya existe config_secure.json
    echo.
    echo Desea actualizar las credenciales? (S/N^)
    set /p respuesta="> "
    if /i "!respuesta!"=="S" goto :actualizar
    if /i "!respuesta!"=="SI" goto :actualizar
    echo.
    echo Migracion cancelada.
    goto :fin
)

REM Verificar si existe config.json antiguo
if not exist "config.json" (
    echo [INFO] No se encontro config.json
    echo        Creando configuracion nueva...
    echo.
    goto :crear_nuevo
)

echo [INFO] Encontrado config.json antiguo
echo        Migrando a formato seguro...
echo.

REM Leer config.json y extraer valores
for /f "tokens=*" %%a in ('type config.json ^| findstr /i "username"') do (
    set line=%%a
    set line=!line:"=!
    set line=!line: =!
    for /f "tokens=2 delims=:" %%b in ("!line!") do set username=%%b
    set username=!username:,=!
)

for /f "tokens=*" %%a in ('type config.json ^| findstr /i "password"') do (
    set line=%%a
    set line=!line:"=!
    set line=!line: =!
    for /f "tokens=2 delims=:" %%b in ("!line!") do set password=%%b
    set password=!password:,=!
)

for /f "tokens=*" %%a in ('type config.json ^| findstr /i "downloadDirectory"') do (
    set line=%%a
    set line=!line:"=!
    set line=!line: =!
    for /f "tokens=2 delims=:" %%b in ("!line!") do set downloadDir=%%b
    set downloadDir=!downloadDir:,=!
)

if "!username!"=="" set username=carbar
if "!password!"=="" set password=Carlos66*
if "!downloadDir!"=="" set downloadDir=c:\p2p\downloads

echo Usuario: !username!
echo Carpeta: !downloadDir!
echo.

REM Crear config_secure.json (sin encriptar por ahora, MainForm lo hará)
(
echo {
echo   "DownloadDirectory": "!downloadDir!",
echo   "SearchTimeout": 450,
echo   "ResponseLimit": 50,
echo   "FileLimit": 1000,
echo   "AutoConnect": true,
echo   "EncryptedUsername": null,
echo   "EncryptedPassword": null
echo }
) > config_secure.json

REM Crear archivo temporal con credenciales para que MainForm las encripte
(
echo !username!
echo !password!
) > .credentials_temp

echo [OK] Configuracion migrada a config_secure.json
echo.
echo IMPORTANTE: Al iniciar SlskDown, las credenciales se encriptaran automaticamente.
echo            Puedes eliminar config.json despues de verificar que funciona.
echo.
goto :fin

:crear_nuevo
echo Ingresa tus credenciales de Soulseek:
echo.
set /p username="Usuario [carbar]: "
if "!username!"=="" set username=carbar

set /p password="Password [Carlos66*]: "
if "!password!"=="" set password=Carlos66*

set /p downloadDir="Carpeta de descargas [c:\p2p\downloads]: "
if "!downloadDir!"=="" set downloadDir=c:\p2p\downloads

(
echo {
echo   "DownloadDirectory": "!downloadDir!",
echo   "SearchTimeout": 450,
echo   "ResponseLimit": 50,
echo   "FileLimit": 1000,
echo   "AutoConnect": true,
echo   "EncryptedUsername": null,
echo   "EncryptedPassword": null
echo }
) > config_secure.json

(
echo !username!
echo !password!
) > .credentials_temp

echo.
echo [OK] Configuracion creada exitosamente!
echo.
goto :fin

:actualizar
echo.
echo Actualizar credenciales:
echo.
set /p username="Usuario: "
set /p password="Password: "

if "!username!"=="" (
    echo.
    echo [ERROR] Usuario y password son requeridos
    goto :fin
)

if "!password!"=="" (
    echo.
    echo [ERROR] Usuario y password son requeridos
    goto :fin
)

(
echo !username!
echo !password!
) > .credentials_temp

echo.
echo [OK] Credenciales actualizadas!
echo     Se encriptaran al iniciar SlskDown.
echo.
goto :fin

:fin
echo.
echo Presiona cualquier tecla para salir...
pause > nul
