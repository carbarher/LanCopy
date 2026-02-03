Write-Host "=== DIAGNÓSTICO SLSKDOWN ==="
Write-Host ""

# Compilación
Write-Host "[1] COMPILACIÓN"
$buildResult = & dotnet build SlskDown\SlskDown.csproj -v:q --no-incremental 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Compilación exitosa" -ForegroundColor Green
} else {
    Write-Host "❌ Error de compilación" -ForegroundColor Red
    Write-Host $buildResult
}
Write-Host ""

# Ejecutable
Write-Host "[2] EJECUTABLE"
if (Test-Path "SlskDown.exe") {
    $exe = Get-Item "SlskDown.exe"
    Write-Host "✅ SlskDown.exe encontrado" -ForegroundColor Green
    Write-Host "   Tamaño: $([math]::Round($exe.Length/1KB,2)) KB"
    Write-Host "   Fecha: $($exe.LastWriteTime)"
} else {
    Write-Host "❌ SlskDown.exe NO encontrado" -ForegroundColor Red
}
Write-Host ""

# Último log
Write-Host "[3] ÚLTIMO LOG"
$lastLog = Get-ChildItem "SlskDown\lanza_last_run_*.log" -ErrorAction SilentlyContinue | 
           Sort-Object LastWriteTime -Descending | 
           Select-Object -First 1
if ($lastLog) {
    Write-Host "✅ Último log: $($lastLog.Name)" -ForegroundColor Green
    Write-Host "   Tamaño: $($lastLog.Length) bytes"
    Write-Host "   Fecha: $($lastLog.LastWriteTime)"
} else {
    Write-Host "❌ No hay logs" -ForegroundColor Red
}
Write-Host ""

# MainForm.cs
Write-Host "[4] MAINFORM.CS"
if (Test-Path "SlskDown\MainForm.cs") {
    $mainForm = Get-Item "SlskDown\MainForm.cs"
    Write-Host "✅ MainForm.cs: $([math]::Round($mainForm.Length/1KB,2)) KB" -ForegroundColor Green
    $lines = (Get-Content "SlskDown\MainForm.cs" -ErrorAction SilentlyContinue).Count
    Write-Host "   Líneas: $lines"
} else {
    Write-Host "❌ MainForm.cs NO encontrado" -ForegroundColor Red
}
Write-Host ""

# Config
Write-Host "[5] CONFIGURACIÓN"
if (Test-Path "SlskDown\config.json") {
    Write-Host "✅ config.json encontrado" -ForegroundColor Green
} else {
    Write-Host "❌ config.json NO encontrado" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== FIN DIAGNÓSTICO ==="
