# Script para verificar que la UI se ve correctamente
Write-Host "=== Test de Layout de UI ===" -ForegroundColor Cyan
Write-Host ""

# Verificar que el archivo MainForm.cs existe
if (Test-Path "MainForm.cs") {
    Write-Host "✓ MainForm.cs encontrado" -ForegroundColor Green
    
    # Contar ModernCard
    $modernCards = (Select-String -Path "MainForm.cs" -Pattern "new ModernCard" -AllMatches).Matches.Count
    Write-Host "✓ ModernCard usados: $modernCards" -ForegroundColor Green
    
    # Contar ModernButton
    $modernButtons = (Select-String -Path "MainForm.cs" -Pattern "new ModernButton" -AllMatches).Matches.Count
    Write-Host "✓ ModernButton usados: $modernButtons" -ForegroundColor Green
    
    # Contar ModernListView
    $modernListViews = (Select-String -Path "MainForm.cs" -Pattern "new ModernListView" -AllMatches).Matches.Count
    Write-Host "✓ ModernListView usados: $modernListViews" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "=== Verificación de Tamaños ===" -ForegroundColor Cyan
    
    # Verificar que ModernCard no tiene Padding excesivo
    $cardWithPadding = Select-String -Path "UI\ModernControls.cs" -Pattern "Padding = new Padding\(0\)"
    if ($cardWithPadding) {
        Write-Host "✓ ModernCard sin padding automático" -ForegroundColor Green
    } else {
        Write-Host "✗ ModernCard tiene padding que puede causar problemas" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "=== Pestañas Creadas ===" -ForegroundColor Cyan
    $tabs = Select-String -Path "MainForm.cs" -Pattern "private void Create.*Tab\(TabPage" -AllMatches
    foreach ($tab in $tabs.Matches) {
        $tabName = $tab.Value -replace "private void Create", "" -replace "Tab\(TabPage", ""
        Write-Host "  - $tabName" -ForegroundColor Yellow
    }
    
} else {
    Write-Host "✗ MainForm.cs no encontrado" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Compilación ===" -ForegroundColor Cyan
dotnet build SlskDown.csproj -v q
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Compilación exitosa" -ForegroundColor Green
} else {
    Write-Host "✗ Error en compilación" -ForegroundColor Red
}

Write-Host ""
Write-Host "Ejecuta 'lanza.bat' para probar la aplicación" -ForegroundColor Cyan
