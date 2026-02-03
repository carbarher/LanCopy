# Script para corregir la indentación del bloque finally
$filePath = "MainForm.cs"
$backupPath = "MainForm.cs.backup_indent_fix"

Write-Host "==================================="
Write-Host "CORRECTOR DE INDENTACION"
Write-Host "==================================="
Write-Host ""

# Leer el archivo
$lines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)
Write-Host "Total de lineas: $($lines.Count)"
Write-Host ""

# Mostrar líneas actuales
Write-Host "Lineas actuales (20303-20308):"
for ($i = 20302; $i -le 20307; $i++) {
    Write-Host "  $($i+1): [$($lines[$i])]"
}
Write-Host ""

# Crear backup
Write-Host "Creando backup..."
[System.IO.File]::Copy($filePath, $backupPath, $true)
Write-Host "Backup guardado: $backupPath"
Write-Host ""

# Corregir la indentación
Write-Host "Corrigiendo indentacion..."

# La línea 20304 (índice 20303) debe tener la indentación correcta
# Debe tener 32 espacios (8 niveles de 4 espacios)
$lines[20303] = "                                finally"
$lines[20304] = "                                {"
$lines[20305] = "                                    // Asegurar que el semaphore siempre se libere"
$lines[20306] = "                                    semaphore.Release();"
$lines[20307] = "                                }"

# Guardar el archivo
[System.IO.File]::WriteAllLines($filePath, $lines, [System.Text.Encoding]::UTF8)

Write-Host "Archivo corregido!"
Write-Host ""

# Verificar
$verifyLines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)
Write-Host "Lineas corregidas (20303-20308):"
for ($i = 20302; $i -le 20307; $i++) {
    Write-Host "  $($i+1): [$($verifyLines[$i])]"
}
Write-Host ""

Write-Host "Compilando para verificar..."
Write-Host ""
& dotnet build SlskDown.csproj --no-incremental

Write-Host ""
Write-Host "==================================="
Write-Host "Codigo de salida: $LASTEXITCODE"
Write-Host "==================================="
