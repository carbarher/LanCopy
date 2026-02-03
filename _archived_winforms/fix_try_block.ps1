# Script definitivo para corregir indentación del bloque try en StartAutomaticSearch
$filePath = "c:\p2p\SlskDown\MainForm.cs"
$content = Get-Content $filePath -Raw
$lines = $content -split "`r?`n"

Write-Host "Corrigiendo indentación del bloque try..."

# El bloque try va de línea 19509 (índice 19508) a línea 20393 (índice 20392)
# Todo dentro debe tener al menos 16 espacios de indentación base

$fixed = 0
for ($i = 19565; $i -le 20392; $i++) {
    $line = $lines[$i]
    
    # Saltar líneas vacías
    if ($line.Trim() -eq '') { continue }
    
    # Detectar indentación actual
    if ($line -match '^( {0,12})([^ ].*)$') {
        # Tiene 12 espacios o menos, necesita 4 más
        $lines[$i] = "    " + $line
        $fixed++
    }
}

# Guardar
$lines -join "`r`n" | Set-Content $filePath -NoNewline -Encoding UTF8
Write-Host "✅ Corregidas $fixed líneas con indentación incorrecta"
