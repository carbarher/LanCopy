# Script mejorado para corregir toda la indentación del bloque try
$filePath = "c:\p2p\SlskDown\MainForm.cs"
$lines = Get-Content $filePath

# El bloque try va desde línea 19509 hasta 20393 (antes del catch en 20394)
# Todo lo que está entre esas líneas con indentación de 12 espacios o menos
# debe tener 16 espacios (dentro del try)

$fixed = 0
for ($i = 19565; $i -le 20392; $i++) {
    $line = $lines[$i]
    
    # Si la línea no está vacía
    if ($line.Trim() -ne '') {
        # Contar espacios al inicio
        if ($line -match '^(\s*)(.*)$') {
            $indent = $matches[1]
            $content = $matches[2]
            $spaceCount = $indent.Length
            
            # Si tiene 12 espacios o menos (debería tener 16 dentro del try)
            if ($spaceCount -le 12 -and $content -ne '') {
                $lines[$i] = "    " + $line
                $fixed++
            }
        }
    }
}

# Guardar
$lines | Set-Content $filePath -Encoding UTF8
Write-Host "Corregidas $fixed líneas"
