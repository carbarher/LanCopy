$content = Get-Content "MainForm.cs" -Raw
$openBraces = ($content.ToCharArray() | Where-Object {$_ -eq '{'}).Count
$closeBraces = ($content.ToCharArray() | Where-Object {$_ -eq '}'}).Count

Write-Host "Open braces: $openBraces"
Write-Host "Close braces: $closeBraces"
Write-Host "Difference: $($openBraces - $closeBraces)"

if ($openBraces -ne $closeBraces) {
    Write-Host "ERROR: Braces are not balanced!" -ForegroundColor Red
    exit 1
} else {
    Write-Host "OK: Braces are balanced" -ForegroundColor Green
    exit 0
}
