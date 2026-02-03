$content = Get-Content "MainForm.cs"
$lineNum = 0

$methods = @("UpdateSearchProgress", "AddConfigTab", "BrowseFolder_Click")

foreach ($method in $methods) {
    Write-Host "`nBuscando: $method" -ForegroundColor Yellow
    $lineNum = 0
    $found = 0
    foreach ($line in $content) {
        $lineNum++
        if ($line -match "private void $method\(") {
            $found++
            Write-Host "  [$found] Línea $lineNum" -ForegroundColor Green
        }
    }
}
