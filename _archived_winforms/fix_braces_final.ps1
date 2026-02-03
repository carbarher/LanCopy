# Script para corregir el balance de llaves en MainForm.cs
$filePath = "MainForm.cs"

Write-Host "Leyendo archivo..."
$lines = Get-Content $filePath

Write-Host "Buscando el bloque problemático..."

# Buscar las líneas específicas
$found = $false
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match 'AutoLog\(\$"▶️ REANUDANDO BÚSQUEDAS') {
        Write-Host "Encontrado en línea $($i+1)"
        
        # Verificar las siguientes líneas
        if ($i + 3 -lt $lines.Count) {
            $line1 = $lines[$i+1].Trim()
            $line2 = $lines[$i+2].Trim()
            $line3 = $lines[$i+3].Trim()
            
            Write-Host "Línea $($i+2): $line1"
            Write-Host "Línea $($i+3): $line2"
            Write-Host "Línea $($i+4): $line3"
            
            # Si hay dos llaves de cierre seguidas antes del finally, eliminar una
            if ($line2 -eq "}" -and $line3 -eq "}" -and $i + 4 -lt $lines.Count -and $lines[$i+4] -match "finally") {
                Write-Host "PROBLEMA DETECTADO: Dos llaves de cierre antes de finally"
                Write-Host "Eliminando llave extra en línea $($i+4)..."
                
                # Eliminar la línea extra
                $newLines = @()
                for ($j = 0; $j -lt $lines.Count; $j++) {
                    if ($j -ne ($i+3)) {
                        $newLines += $lines[$j]
                    }
                }
                
                Write-Host "Guardando archivo corregido..."
                $newLines | Set-Content $filePath -Encoding UTF8
                Write-Host "¡Corrección aplicada exitosamente!"
                $found = $true
                break
            }
            elseif ($line2 -eq "}" -and $line3 -match "finally") {
                Write-Host "Estructura correcta detectada - no se necesita corrección"
                $found = $true
                break
            }
        }
    }
}

if (-not $found) {
    Write-Host "No se encontró el patrón esperado. Buscando alternativa..."
    
    # Buscar patrón alternativo
    for ($i = 20275; $i -lt 20295 -and $i -lt $lines.Count; $i++) {
        Write-Host "Línea $($i+1): $($lines[$i])"
    }
}

Write-Host ""
Write-Host "Presiona Enter para continuar..."
Read-Host
