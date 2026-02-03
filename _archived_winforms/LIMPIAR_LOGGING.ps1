# Script para eliminar todas las líneas de logging de diagnóstico

$mainFormPath = "MainForm.cs"

# Leer el archivo
$content = Get-Content $mainFormPath -Raw

# Eliminar todas las líneas que contienen startup_log.txt
$content = $content -replace '[\t ]*System\.IO\.File\.AppendAllText\("startup_log\.txt"[^\n]*\n', ''

# Guardar el archivo
$content | Set-Content $mainFormPath -NoNewline

Write-Host "✅ Logging eliminado de MainForm.cs"
Write-Host "Compilando para verificar..."

dotnet build SlskDown.csproj -c Release --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Compilación exitosa"
} else {
    Write-Host "❌ Error en la compilación"
}
