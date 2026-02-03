# Eliminar archivos duplicados que causan conflictos
$filesToDelete = @(
    "MainForm.Simple.cs",
    "MainForm.Ultra.cs",
    "MainFormClean.cs",
    "MainFormClean_backup.cs",
    "MainFormNew.cs",
    "MainFormSimple.cs",
    "MainForm_NEW.cs",
    "SmartCache.cs",
    "SpanOptimizations.cs",
    "StatisticsDashboard.cs",
    "WindowsNotificationService.cs"
)

foreach ($file in $filesToDelete) {
    $fullPath = Join-Path $PSScriptRoot $file
    if (Test-Path $fullPath) {
        Remove-Item $fullPath -Force
        Write-Host "Eliminado: $file" -ForegroundColor Green
    } else {
        Write-Host "No existe: $file" -ForegroundColor Yellow
    }
}

Write-Host "`nArchivos restantes MainForm*.cs:" -ForegroundColor Cyan
Get-ChildItem -Filter "MainForm*.cs" | Select-Object Name
