# Script para corregir la indentación del método StartAutomaticSearch
$filePath = "c:\p2p\SlskDown\MainForm.cs"
$lines = Get-Content $filePath

# Líneas del método StartAutomaticSearch
$startMethod = 19506  # private async Task StartAutomaticSearch()
$startTry = 19508     # try
$endTry = 20392       # } que cierra el try (antes del catch)
$endCatch = 20406     # } que cierra el catch
$endFinally = 20427   # } que cierra el finally

Write-Host "Corrigiendo indentación del método StartAutomaticSearch..."

# Procesar líneas dentro del try (19509 a 20392)
for ($i = 19509; $i -le $endTry; $i++) {
    $line = $lines[$i]
    
    # Si la línea no está vacía y no comienza con suficiente indentación
    if ($line -match '^\s') {
        # Contar espacios actuales
        $match = [regex]::Match($line, '^(\s*)')
        $currentIndent = $match.Groups[1].Value.Length
        
        # Si tiene menos de 16 espacios (4 tabs), agregar 4 espacios
        if ($currentIndent -lt 16 -and $line.Trim() -ne '') {
            $lines[$i] = "    " + $line
            if ($i -lt 19530) {
                Write-Host "Línea $($i+1): agregados 4 espacios"
            }
        }
    }
}

# Guardar archivo
$lines | Set-Content $filePath -Encoding UTF8
Write-Host "Archivo guardado con indentación corregida"
