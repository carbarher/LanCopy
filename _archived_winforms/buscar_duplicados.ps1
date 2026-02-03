$content = Get-Content "MainForm.cs"
$lineNum = 0

Write-Host "Buscando definiciones duplicadas..." -ForegroundColor Cyan
Write-Host ""

$fields = @("usernameTextBox", "passwordTextBox", "downloadDirTextBox", "downloadedFilesListTextBox", "autoDownloadCheckBox", "isLoadingConfig")

foreach ($field in $fields) {
    Write-Host "Campo: $field" -ForegroundColor Yellow
    $lineNum = 0
    foreach ($line in $content) {
        $lineNum++
        if ($line -match "private.*\b$field\b") {
            Write-Host "  Línea $lineNum : $($line.Trim())" -ForegroundColor Green
        }
    }
    Write-Host ""
}
