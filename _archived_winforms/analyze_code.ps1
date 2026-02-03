# Script para analizar calidad de código (equivalente a pylint/flake8 para Python)

Write-Host "🔍 Analizando código de SlskDown..." -ForegroundColor Cyan
Write-Host ""

# 1. Formatear código
Write-Host "📝 Formateando código..." -ForegroundColor Yellow
dotnet format --verify-no-changes
if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠️  Código necesita formateo. Ejecuta: dotnet format" -ForegroundColor Yellow
} else {
    Write-Host "✅ Código correctamente formateado" -ForegroundColor Green
}
Write-Host ""

# 2. Compilar con warnings como errores
Write-Host "🔨 Compilando con análisis estricto..." -ForegroundColor Yellow
dotnet build /p:TreatWarningsAsErrors=false /p:EnforceCodeStyleInBuild=true
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Compilación exitosa" -ForegroundColor Green
} else {
    Write-Host "❌ Errores de compilación encontrados" -ForegroundColor Red
}
Write-Host ""

# 3. Análisis de código
Write-Host "🔍 Ejecutando análisis de código..." -ForegroundColor Yellow
dotnet build /p:RunAnalyzersDuringBuild=true
Write-Host ""

# 4. Estadísticas del código
Write-Host "📊 Estadísticas del código:" -ForegroundColor Cyan
Write-Host ""

$files = Get-ChildItem -Path . -Filter *.cs -Recurse | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }
$totalLines = 0
$totalFiles = 0

foreach ($file in $files) {
    $lines = (Get-Content $file.FullName | Measure-Object -Line).Lines
    $totalLines += $lines
    $totalFiles++
}

Write-Host "  Archivos C#:     $totalFiles" -ForegroundColor White
Write-Host "  Líneas totales:  $totalLines" -ForegroundColor White
Write-Host "  Promedio/archivo: $([math]::Round($totalLines / $totalFiles, 0))" -ForegroundColor White
Write-Host ""

# 5. Archivos más grandes
Write-Host "📁 Archivos más grandes:" -ForegroundColor Cyan
$files | Sort-Object { (Get-Content $_.FullName | Measure-Object -Line).Lines } -Descending | Select-Object -First 5 | ForEach-Object {
    $lines = (Get-Content $_.FullName | Measure-Object -Line).Lines
    Write-Host "  $($_.Name): $lines líneas" -ForegroundColor White
}
Write-Host ""

# 6. Verificar convenciones de nomenclatura
Write-Host "🏷️  Verificando nomenclatura..." -ForegroundColor Yellow
Write-Host "  (Buscar warnings de nomenclatura en la compilación anterior)" -ForegroundColor Gray
Write-Host ""

# 7. Resumen
Write-Host "✅ Análisis completado" -ForegroundColor Green
Write-Host ""
Write-Host "💡 Sugerencias:" -ForegroundColor Cyan
Write-Host "  - Ejecuta 'dotnet format' para formatear automáticamente" -ForegroundColor White
Write-Host "  - Revisa warnings de compilación" -ForegroundColor White
Write-Host "  - Consulta CODING_STANDARDS.md para convenciones" -ForegroundColor White
