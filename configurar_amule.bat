@echo off
echo ========================================
echo Configuracion Automatica de aMule
echo ========================================
echo.

echo [1/4] Verificando instalacion de aMule...
if exist "C:\Program Files\aMule\amuled.exe" (
    echo ✅ aMule encontrado en C:\Program Files\aMule\
) else (
    echo ❌ aMule NO encontrado
    echo    Por favor instala aMule primero desde:
    echo    https://www.amule.org/
    pause
    exit /b 1
)
echo.

echo [2/4] Creando directorio de configuracion...
if not exist "%USERPROFILE%\.aMule" (
    mkdir "%USERPROFILE%\.aMule"
    echo ✅ Directorio creado: %USERPROFILE%\.aMule
) else (
    echo ✅ Directorio ya existe
)
echo.

echo [3/4] Configurando contraseña EC...
echo.
echo IMPORTANTE: Necesitas configurar una contraseña para
echo             External Connections (EC)
echo.
set /p EC_PASSWORD="Ingresa una contraseña para EC: "
echo.

echo Generando hash MD5 de la contraseña...
powershell -Command "$password = '%EC_PASSWORD%'; $md5 = New-Object -TypeName System.Security.Cryptography.MD5CryptoServiceProvider; $utf8 = New-Object -TypeName System.Text.UTF8Encoding; $hash = [System.BitConverter]::ToString($md5.ComputeHash($utf8.GetBytes($password))); $hash.Replace('-','').ToLower()" > temp_hash.txt
set /p EC_HASH=<temp_hash.txt
del temp_hash.txt
echo ✅ Hash MD5 generado: %EC_HASH%
echo.

echo [4/4] Creando archivo de configuracion...
(
echo [ExternalConnect]
echo AcceptExternalConnections=1
echo ECPassword=%EC_HASH%
echo ECPort=4712
echo.
echo [eMule]
echo Nick=SlskDown-User
echo MaxUpload=100
echo MaxDownload=0
echo.
echo [Server]
echo AutoConnect=1
echo AutoConnectStaticOnly=0
echo.
echo [GUI]
echo HideOnClose=1
) > "%USERPROFILE%\.aMule\amule.conf"

echo ✅ Configuracion creada en: %USERPROFILE%\.aMule\amule.conf
echo.

echo ========================================
echo Configuracion Completada
echo ========================================
echo.
echo Contraseña EC: %EC_PASSWORD%
echo Hash MD5: %EC_HASH%
echo Puerto: 4712
echo.
echo IMPORTANTE: Anota esta informacion para SlskDown
echo.
echo Siguiente paso: Iniciar aMule daemon
echo Comando: "C:\Program Files\aMule\amuled.exe"
echo.
pause
