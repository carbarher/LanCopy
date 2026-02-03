# Script para arreglar bloques de código comentados incorrectamente en archivos C#
# Busca patrones como:
#   // ERROR: codigo(
#       parametro1,
#       parametro2
#   );
# Y los convierte en:
#   // ERROR: codigo(
#   //    parametro1,
#   //    parametro2
#   // );

param(
    [string]$FilePath
)

if (-not (Test-Path $FilePath)) {
    Write-Host "ERROR: Archivo no encontrado: $FilePath" -ForegroundColor Red
    exit 1
}

Write-Host "Procesando: $FilePath" -ForegroundColor Cyan

$lines = Get-Content $FilePath -Encoding UTF8
$modified = $false
$i = 0

while ($i -lt $lines.Count) {
    $line = $lines[$i]
    $trimmed = $line.Trim()
    
    # Buscar líneas que contienen "// ERROR:" y terminan con ( o =
    if ($trimmed -match '^//\s*ERROR:.*[\(=]\s*$') {
        Write-Host "  Encontrado bloque en línea $($i+1): $trimmed" -ForegroundColor Yellow
        
        # Obtener indentación base
        $baseIndent = $line -replace '^(\s*).*$', '$1'
        
        # Comentar las siguientes líneas hasta encontrar el final
        $j = $i + 1
        while ($j -lt $lines.Count) {
            $nextLine = $lines[$j]
            $nextTrimmed = $nextLine.Trim()
            
            # Si está vacía, saltarla
            if ([string]::IsNullOrWhiteSpace($nextTrimmed)) {
                $j++
                continue
            }
            
            # Si ya está comentada, salir
            if ($nextTrimmed.StartsWith('//')) {
                break
            }
            
            # Obtener indentación de la línea actual
            $currentIndent = $nextLine -replace '^(\s*).*$', '$1'
            
            # Si la indentación es menor que la base, salir
            if ($currentIndent.Length -lt $baseIndent.Length) {
                break
            }
            
            # Comentar esta línea
            $lines[$j] = $currentIndent + '// ' + $nextLine.Substring($currentIndent.Length)
            $modified = $true
            Write-Host "    Comentada línea $($j+1)" -ForegroundColor Green
            
            # Si termina con ; o }, probablemente es el final
            if ($nextTrimmed.EndsWith(';') -or $nextTrimmed.EndsWith('}')) {
                break
            }
            
            $j++
        }
    }
    
    $i++
}

if ($modified) {
    # Guardar archivo
    $lines | Set-Content $FilePath -Encoding UTF8
    Write-Host "GUARDADO: $FilePath" -ForegroundColor Green
    return 0
} else {
    Write-Host "SIN CAMBIOS: $FilePath" -ForegroundColor Gray
    return 1
}
