# Script para encontrar y mostrar la configuración de aMule Web

Write-Host "=== BUSCANDO CONFIGURACION DE AMULE ==="
Write-Host ""

# Posibles ubicaciones del archivo de configuración
$locations = @(
    "$env:APPDATA\aMule\amule.conf",
    "$env:USERPROFILE\.aMule\amule.conf",
    "C:\Program Files\aMule\amule.conf",
    "C:\Program Files (x86)\aMule\amule.conf",
    "$env:LOCALAPPDATA\aMule\amule.conf"
)

$found = $false

foreach ($location in $locations) {
    if (Test-Path $location) {
        Write-Host "Encontrado: $location"
        Write-Host ""
        Write-Host "=== CONFIGURACION WEB SERVER ==="
        
        # Leer y mostrar líneas relevantes
        $content = Get-Content $location
        $inWebSection = $false
        
        foreach ($line in $content) {
            if ($line -match "^\[WebServer\]") {
                $inWebSection = $true
            }
            elseif ($line -match "^\[") {
                $inWebSection = $false
            }
            
            if ($inWebSection) {
                Write-Host $line
            }
        }
        
        $found = $true
        Write-Host ""
        Write-Host "=== INSTRUCCIONES ==="
        Write-Host "1. Cierra aMule si esta abierto"
        Write-Host "2. Edita el archivo: $location"
        Write-Host "3. En la seccion [WebServer], asegurate de tener:"
        Write-Host "   Enabled=1"
        Write-Host "   Port=4711"
        Write-Host "   Password=<tu_password_en_MD5>"
        Write-Host ""
        Write-Host "4. Para generar el password en MD5:"
        Write-Host "   - Ve a: https://www.md5hashgenerator.com/"
        Write-Host "   - Ingresa tu password (ej: amule123)"
        Write-Host "   - Copia el hash MD5"
        Write-Host "   - Pegalo en Password="
        Write-Host ""
        Write-Host "5. Guarda el archivo y reinicia aMule"
        
        break
    }
}

if (-not $found) {
    Write-Host "No se encontro el archivo de configuracion de aMule"
    Write-Host ""
    Write-Host "Posibles causas:"
    Write-Host "1. aMule no esta instalado"
    Write-Host "2. aMule nunca se ha ejecutado (no ha creado el archivo de config)"
    Write-Host "3. Esta en una ubicacion no estandar"
    Write-Host ""
    Write-Host "Solucion:"
    Write-Host "1. Ejecuta aMule al menos una vez"
    Write-Host "2. Configura el Web Server desde Preferencias > Remote Controls"
}
