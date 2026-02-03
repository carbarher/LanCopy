# Script de prueba para autenticación aMule EC
# Verifica que la contraseña y el protocolo funcionen correctamente

$host = "localhost"
$port = 4712
$password = "Carlos66*"

Write-Host "=== Test de Autenticación aMule EC ===" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar que aMule está corriendo
Write-Host "1. Verificando que aMule está corriendo..." -ForegroundColor Yellow
try {
    $connection = New-Object System.Net.Sockets.TcpClient($host, $port)
    Write-Host "   ✅ aMule está corriendo en $host`:$port" -ForegroundColor Green
    $connection.Close()
} catch {
    Write-Host "   ❌ ERROR: No se puede conectar a aMule en $host`:$port" -ForegroundColor Red
    Write-Host "   Asegúrate de que aMule está corriendo y el puerto EC es correcto." -ForegroundColor Red
    exit 1
}

# 2. Calcular MD5 de la contraseña
Write-Host ""
Write-Host "2. Calculando MD5 de la contraseña..." -ForegroundColor Yellow
$md5 = [System.Security.Cryptography.MD5]::Create()
$bytes = [System.Text.Encoding]::UTF8.GetBytes($password)
$hash = $md5.ComputeHash($bytes)
$hashString = -join ($hash | ForEach-Object { $_.ToString('x2') })
Write-Host "   Contraseña: $password" -ForegroundColor Gray
Write-Host "   MD5 Hash: $hashString" -ForegroundColor Gray

# 3. Verificar hash en amule.conf
Write-Host ""
Write-Host "3. Verificando hash en amule.conf..." -ForegroundColor Yellow
$amuleConfPath = "$env:APPDATA\aMule\amule.conf"
if (Test-Path $amuleConfPath) {
    $ecPassword = Get-Content $amuleConfPath | Select-String "ECPassword=" | ForEach-Object { $_.ToString().Split('=')[1] }
    Write-Host "   Hash en amule.conf: $ecPassword" -ForegroundColor Gray
    
    if ($ecPassword -eq $hashString) {
        Write-Host "   ✅ Los hashes coinciden" -ForegroundColor Green
    } else {
        Write-Host "   ❌ ERROR: Los hashes NO coinciden" -ForegroundColor Red
        Write-Host "   Esperado: $hashString" -ForegroundColor Red
        Write-Host "   En archivo: $ecPassword" -ForegroundColor Red
        Write-Host ""
        Write-Host "   Solución: Actualiza la contraseña EC en aMule:" -ForegroundColor Yellow
        Write-Host "   1. Abre aMule" -ForegroundColor Yellow
        Write-Host "   2. Ve a Preferencias → Conexión Externa" -ForegroundColor Yellow
        Write-Host "   3. Cambia la contraseña a: $password" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "   ⚠️ ADVERTENCIA: No se encontró amule.conf en $amuleConfPath" -ForegroundColor Yellow
}

# 4. Construir paquete AUTH_REQ
Write-Host ""
Write-Host "4. Construyendo paquete AUTH_REQ..." -ForegroundColor Yellow

# Flags: 0x00000022 (UTF-8 + ACCEPTS)
$flags = @(0x00, 0x00, 0x00, 0x22)

# Body: OpCode + Tags
# OpCode: 0x02 (EC_OP_AUTH_REQ) en UTF-8 = 0x02
# Tag count: 4 tags en UTF-8 = 0x04
$body = @(0x02, 0x04)

# Tag 1: EC_TAG_CLIENT_NAME = 0x0100 (shifted left = 0x0200) = C8 80 en UTF-8
# Type: STRING (0x06), Length: 9, Value: "SlskDown\0"
$body += @(0xC8, 0x80, 0x06, 0x09) + [System.Text.Encoding]::UTF8.GetBytes("SlskDown") + @(0x00)

# Tag 2: EC_TAG_CLIENT_VERSION = 0x0101 (shifted = 0x0202) = C8 82 en UTF-8
# Type: STRING (0x06), Length: 6, Value: "2.3.3\0"
$body += @(0xC8, 0x82, 0x06, 0x06) + [System.Text.Encoding]::UTF8.GetBytes("2.3.3") + @(0x00)

# Tag 3: EC_TAG_PROTOCOL_VERSION = 0x0004 (no shift para numeric) = 0x04
# Type: UINT16 (0x03), Length: 2, Value: 0x0204 (big-endian)
$body += @(0x04, 0x03, 0x02, 0x02, 0x04)

# Tag 4: EC_TAG_PASSWD_HASH = 0x0002 (no shift) = 0x02
# Type: HASH16 (0x09), Length: 16, Value: MD5 hash
$body += @(0x02, 0x09, 0x10) + $hash

# Body size (big-endian)
$bodySize = $body.Length
$bodySizeBytes = @(
    ($bodySize -shr 24) -band 0xFF,
    ($bodySize -shr 16) -band 0xFF,
    ($bodySize -shr 8) -band 0xFF,
    $bodySize -band 0xFF
)

# Paquete completo
$packet = $flags + $bodySizeBytes + $body

Write-Host "   Tamaño del paquete: $($packet.Length) bytes" -ForegroundColor Gray
Write-Host "   Hex: $(-join ($packet | ForEach-Object { $_.ToString('X2') + ' ' }))" -ForegroundColor Gray

# 5. Enviar paquete y recibir respuesta
Write-Host ""
Write-Host "5. Enviando AUTH_REQ a aMule..." -ForegroundColor Yellow

try {
    $client = New-Object System.Net.Sockets.TcpClient($host, $port)
    $stream = $client.GetStream()
    
    # Enviar paquete
    $stream.Write($packet, 0, $packet.Length)
    $stream.Flush()
    Write-Host "   ✅ Paquete enviado" -ForegroundColor Green
    
    # Recibir respuesta
    Start-Sleep -Milliseconds 500
    
    # Leer flags (4 bytes)
    $responseFlags = New-Object byte[] 4
    $bytesRead = $stream.Read($responseFlags, 0, 4)
    
    if ($bytesRead -eq 4) {
        $flagsValue = [BitConverter]::ToUInt32($responseFlags, 0)
        Write-Host "   Flags recibidos: 0x$($flagsValue.ToString('X8'))" -ForegroundColor Gray
        
        # Leer body size (4 bytes, big-endian)
        $bodySizeBytes = New-Object byte[] 4
        $bytesRead = $stream.Read($bodySizeBytes, 0, 4)
        $responseBodySize = ($bodySizeBytes[0] -shl 24) -bor ($bodySizeBytes[1] -shl 16) -bor ($bodySizeBytes[2] -shl 8) -bor $bodySizeBytes[3]
        Write-Host "   Body size: $responseBodySize bytes" -ForegroundColor Gray
        
        # Leer body
        $responseBody = New-Object byte[] $responseBodySize
        $bytesRead = $stream.Read($responseBody, 0, $responseBodySize)
        Write-Host "   Body hex: $(-join ($responseBody | ForEach-Object { $_.ToString('X2') + ' ' }))" -ForegroundColor Gray
        
        # Parsear OpCode
        $hasUtf8 = ($flagsValue -band 0x20) -ne 0
        if ($hasUtf8) {
            # UTF-8 format
            $opCode = $responseBody[0]
            if ($responseBody[0] -ge 0x80) {
                # Multi-byte UTF-8
                $opCode = (($responseBody[0] -band 0x7F) -shl 8) -bor $responseBody[1]
            }
        } else {
            # Plain format
            $opCode = $responseBody[0]
        }
        
        Write-Host ""
        if ($opCode -eq 0x04) {
            Write-Host "   ✅✅✅ AUTENTICACIÓN EXITOSA! OpCode: 0x04 (EC_OP_AUTH_OK)" -ForegroundColor Green
            Write-Host ""
            Write-Host "=== RESULTADO: TODO FUNCIONA CORRECTAMENTE ===" -ForegroundColor Green
        } else {
            Write-Host "   ❌ Autenticación fallida. OpCode recibido: 0x$($opCode.ToString('X2'))" -ForegroundColor Red
            Write-Host "   Se esperaba: 0x04 (EC_OP_AUTH_OK)" -ForegroundColor Red
        }
    } else {
        Write-Host "   ❌ ERROR: No se recibió respuesta de aMule" -ForegroundColor Red
    }
    
    $stream.Close()
    $client.Close()
    
} catch {
    Write-Host "   ❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Fin del test ===" -ForegroundColor Cyan
