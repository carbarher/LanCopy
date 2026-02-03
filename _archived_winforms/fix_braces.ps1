# Script para agregar la llave de cierre faltante en MainForm.cs

$file = "MainForm.cs"
$lines = Get-Content $file

Write-Host "Total lines: $($lines.Length)"

# Buscar la línea donde termina OnBenchmarkClick (línea ~1214)
$insertIndex = -1
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '^\s+btnBenchmark\.Text = "⚡ Benchmark \(Rust\)";') {
        # Encontrar la llave de cierre del método (debería estar 2 líneas después)
        for ($j = $i; $j -lt ($i + 5); $j++) {
            if ($lines[$j] -match '^\s+\}$') {
                $insertIndex = $j + 1
                Write-Host "Found method end at line $($j + 1)"
                break
            }
        }
        break
    }
}

if ($insertIndex -eq -1) {
    Write-Host "ERROR: No se encontró el punto de inserción"
    exit 1
}

Write-Host "Inserting closing brace at line $($insertIndex + 1)"

# Crear backup
Copy-Item $file "$file.backup_before_fix"

# Insertar la llave de cierre de clase
$newLines = @()
$newLines += $lines[0..($insertIndex - 1)]
$newLines += ""
$newLines += "        // ===== FIN DE MÉTODOS PRINCIPALES ====="
$newLines += "    }  // Fin de clase MainForm"
$newLines += ""
$newLines += "    // ===== CLASES ANIDADAS Y HELPERS ====="
$newLines += "    public partial class MainForm"
$newLines += "    {"
$newLines += $lines[$insertIndex..($lines.Length - 1)]

# Guardar
$newLines | Set-Content $file

Write-Host "✅ Llave de cierre agregada"
Write-Host "✅ Backup guardado en: $file.backup_before_fix"
Write-Host ""
Write-Host "Ejecuta 'lanza' para compilar"
