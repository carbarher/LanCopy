param([string]$mensaje = 'Auto-save: cambios guardados')

Write-Host '=====================================' -ForegroundColor Cyan
Write-Host '[*] GUARDADO AUTOMATICO INICIADO' -ForegroundColor Cyan
Write-Host '=====================================' -ForegroundColor Cyan
Write-Host ''

Write-Host '[1/5] Limpiando archivos temporales...' -ForegroundColor Yellow
$cleanedCount = 0
Get-ChildItem -Path . -Include bin,obj,.vs -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Write-Host '   OK Limpieza completada' -ForegroundColor Green
Write-Host ''

Write-Host '[2/5] Creando backup...' -ForegroundColor Yellow
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupDir = '.\Backups'
if (-not (Test-Path $backupDir)) { New-Item -ItemType Directory -Path $backupDir -Force | Out-Null }
$zipPath = Join-Path $backupDir "backup_$timestamp.zip"
Compress-Archive -Path SlskDown\*.cs,SlskDown\*.csproj -DestinationPath $zipPath -Force
Write-Host '   OK Backup creado' -ForegroundColor Green
Write-Host ''

Write-Host '[3/5] Agregando cambios a Git...' -ForegroundColor Yellow
git add .
Write-Host '   OK Archivos agregados' -ForegroundColor Green
Write-Host ''

Write-Host '[4/5] Creando commit...' -ForegroundColor Yellow
git commit -m "$mensaje"
Write-Host '   OK Commit creado' -ForegroundColor Green
Write-Host ''

Write-Host '[5/5] Subiendo a GitHub...' -ForegroundColor Yellow
git push origin main
Write-Host '   OK Cambios subidos' -ForegroundColor Green
Write-Host ''

Write-Host '=====================================' -ForegroundColor Cyan
Write-Host '[OK] GUARDADO COMPLETADO' -ForegroundColor Green
Write-Host '=====================================' -ForegroundColor Cyan
Write-Host ''
Write-Host '[*] Todo listo para cerrar el proyecto' -ForegroundColor Green
Start-Sleep -Seconds 3
