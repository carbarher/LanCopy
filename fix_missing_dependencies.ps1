# Script para comentar código que usa dependencias faltantes

$filePath = "c:\p2p\SlskDown\MainForm.cs"

Write-Host "Arreglando dependencias faltantes en MainForm.cs..." -ForegroundColor Cyan

# Crear backup
$backup = "$filePath.backup_deps_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $filePath $backup -Force
Write-Host "[OK] Backup creado: $backup" -ForegroundColor Green

# Leer contenido
$content = Get-Content $filePath -Raw -Encoding UTF8

# Variables a comentar
$varsToComment = @(
    'adaptivePurge',
    'adaptiveAutoSearch',
    'authorSearchIndex',
    'duplicateAuthorGroups',
    'authorsUpdater',
    'duplicateAuthorTotalCount'
)

$changes = 0

# Comentar líneas que usan estas variables
foreach ($var in $varsToComment) {
    # Patrón para encontrar líneas que usan la variable (no ya comentadas)
    $pattern = "(?m)^(\s*)(?!//\s*)(.*)$var(.*)$"
    
    $matches = [regex]::Matches($content, $pattern)
    
    if ($matches.Count -gt 0) {
        Write-Host "Comentando $($matches.Count) líneas que usan '$var'" -ForegroundColor Yellow
        
        # Reemplazar cada match
        $content = [regex]::Replace($content, $pattern, '$1// $2' + $var + '$3')
        $changes += $matches.Count
    }
}

Write-Host ""
Write-Host "Total de líneas comentadas: $changes" -ForegroundColor Green

# Guardar
$content | Set-Content $filePath -Encoding UTF8 -NoNewline -Force

Write-Host "[OK] Archivo guardado" -ForegroundColor Green
Write-Host ""
Write-Host "Ejecuta: dotnet build SlskDown.csproj -c Release" -ForegroundColor Cyan
