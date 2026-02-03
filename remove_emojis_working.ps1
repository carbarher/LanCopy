# Script para eliminar emojis de archivos .cs
$ErrorActionPreference = "Continue"
$projectPath = "c:\p2p\SlskDown"
$filesProcessed = 0
$totalCharsRemoved = 0

# Lista exhaustiva de emojis a eliminar
$emojis = @(
    [char]0x1F50D, [char]0x1F50E, [char]0x1F4C1, [char]0x1F4C2, [char]0x1F4C4, [char]0x1F4CA, [char]0x1F4C8, [char]0x1F4C9, [char]0x1F4BE, [char]0x1F4BF,
    [char]0x1F527, [char]0x1F528, [char]0x2699, [char]0x1F680, [char]0x1F3AF, [char]0x2705, [char]0x274C, [char]0x26A0, [char]0x1F534, [char]0x1F7E2,
    [char]0x1F7E1, [char]0x1F535, [char]0x2B50, [char]0x1F4A1, [char]0x1F525, [char]0x2744, [char]0x23F0, [char]0x23F1, [char]0x1F4C5, [char]0x1F4C6,
    [char]0x1F310, [char]0x1F30D, [char]0x1F30E, [char]0x1F517, [char]0x1F513, [char]0x1F512, [char]0x1F510, [char]0x1F511, [char]0x1F4DD, [char]0x1F4CB,
    [char]0x1F4CC, [char]0x1F4CD, [char]0x1F3B5, [char]0x1F3B6, [char]0x1F3A7, [char]0x1F3A4, [char]0x1F3AC, [char]0x1F3AE, [char]0x1F3B2, [char]0x1F3B0,
    [char]0x1F3A8, [char]0x1F3AD, [char]0x1F3AA, [char]0x1F3A2, [char]0x1F3A1, [char]0x1F3A0, [char]0x1F39F, [char]0x1F3AB, [char]0x1F396, [char]0x1F3C6,
    [char]0x1F3C5, [char]0x1F947, [char]0x1F948, [char]0x1F949, [char]0x1F4B0, [char]0x1F4B8, [char]0x1F4B5, [char]0x1F4B4, [char]0x1F4B6, [char]0x1F4B7,
    [char]0x1F4B3, [char]0x1F48E, [char]0x1F4F1, [char]0x1F4F2, [char]0x260E, [char]0x1F4DE, [char]0x1F4DF, [char]0x1F4E0, [char]0x1F5A5, [char]0x1F4BB,
    [char]0x2328, [char]0x1F5B1, [char]0x1F5A8, [char]0x1F4C0, [char]0x1F4BD, [char]0x1F4BE, [char]0x1F4BF, [char]0x1F4F7, [char]0x1F4F8, [char]0x1F4F9,
    [char]0x1F3A5, [char]0x1F4FD, [char]0x1F3AC, [char]0x1F4FA, [char]0x1F4FB, [char]0x1F4E1, [char]0x1F50A, [char]0x1F509, [char]0x1F508, [char]0x1F507,
    [char]0x1F4E2, [char]0x1F4E3, [char]0x1F4EF, [char]0x1F514, [char]0x1F515, [char]0x1F3BC, [char]0x1F3B5, [char]0x1F3B6, [char]0x1F399, [char]0x1F39A,
    [char]0x1F39B, [char]0x23F8, [char]0x23EF, [char]0x23F9, [char]0x23FA, [char]0x23ED, [char]0x23EE, [char]0x23E9, [char]0x23EA, [char]0x23EB,
    [char]0x23EC, [char]0x25C0, [char]0x1F53C, [char]0x1F53D, [char]0x27A1, [char]0x2B05, [char]0x2B06, [char]0x2B07, [char]0x2197, [char]0x2198,
    [char]0x2199, [char]0x2196, [char]0x2195, [char]0x2194, [char]0x21AA, [char]0x21A9, [char]0x2934, [char]0x2935, [char]0x1F503, [char]0x1F504,
    [char]0x1F519, [char]0x1F51A, [char]0x1F51B, [char]0x1F51C, [char]0x1F51D, [char]0x1F500, [char]0x1F501, [char]0x1F502, [char]0x25B6, [char]0x26A1,
    [char]0x1F31F, [char]0x2728, [char]0x1F4AB, [char]0x1F506, [char]0x1F505, [char]0x2600, [char]0x1F319, [char]0x2B55, [char]0x2757, [char]0x2753,
    [char]0x1F4AC, [char]0x1F4AD, [char]0x1F5E8, [char]0x1F5EF, [char]0x1F4A5, [char]0x1F4A2, [char]0x1F4A4, [char]0x1F4A6, [char]0x1F4A7, [char]0x1F4A8,
    [char]0x1F389, [char]0x1F38A, [char]0x1F388, [char]0x1F380, [char]0x1F381, [char]0x1F3C1, [char]0x1F6A9, [char]0x1F38C, [char]0x1F3F4, [char]0x1F3F3,
    [char]0x1F4E6, [char]0x1F4E7, [char]0x1F4E8, [char]0x1F4E9, [char]0x1F4E4, [char]0x1F4E5, [char]0x1F4EE, [char]0x1F4EA, [char]0x1F4EB, [char]0x1F4EC,
    [char]0x1F4ED, [char]0x1F5C2, [char]0x1F5C3, [char]0x1F5C4, [char]0x1F5D1, [char]0x1F5D2, [char]0x1F5D3, [char]0x1F4C7, [char]0x1F4C8, [char]0x1F4C9,
    [char]0x1F4CA, [char]0x1F4C3, [char]0x1F4DC, [char]0x1F4C4, [char]0x1F4D1, [char]0x1F516, [char]0x1F3F7, [char]0x1F4BC, [char]0x1F4C1, [char]0x1F4C2,
    [char]0x1F5C2, [char]0x1F5DE, [char]0x1F4F0, [char]0x1F4D3, [char]0x1F4D4, [char]0x1F4D2, [char]0x1F4D5, [char]0x1F4D7, [char]0x1F4D8, [char]0x1F4D9,
    [char]0x1F4DA, [char]0x1F4D6, [char]0x1F517, [char]0x1F4CE, [char]0x1F587, [char]0x1F4D0, [char]0x1F4CF, [char]0x1F4CC, [char]0x1F4CD, [char]0x2702,
    [char]0x1F58A, [char]0x1F58B, [char]0x2712, [char]0x1F58C, [char]0x1F58D, [char]0x1F4DD, [char]0x270F, [char]0x1F50D, [char]0x1F50E, [char]0x1F50F,
    [char]0x1F510, [char]0x1F512, [char]0x1F513, [char]0x1F511, [char]0x1F5DD, [char]0x1F528, [char]0x1FA93, [char]0x26CF, [char]0x2692, [char]0x1F6E0,
    [char]0x1F5E1, [char]0x2694, [char]0x1F52B, [char]0x1FA83, [char]0x1F3F9, [char]0x1F6E1, [char]0x1FA9A, [char]0x1F527, [char]0x1FA9B, [char]0x1F529,
    [char]0x2699, [char]0x1F5DC, [char]0x2696, [char]0x1F9AF, [char]0x1F517, [char]0x26D3, [char]0x1FA9D, [char]0x1F9F0, [char]0x1F9F2, [char]0x1FA9C,
    [char]0x23F1, [char]0x23F2, [char]0x23F0, [char]0x1F570, [char]0x231B, [char]0x23F3, [char]0x1F4E1, [char]0x1F50B, [char]0x1FAAB, [char]0x1F50C,
    [char]0x1F4A1, [char]0x1F526, [char]0x1F56F, [char]0x1FA94, [char]0x1F9EF, [char]0x1F6E2, [char]0x1F4B8, [char]0x1F4B5, [char]0x1F4B4, [char]0x1F4B6,
    [char]0x1F4B7, [char]0x1FA99, [char]0x1F4B0, [char]0x1F4B3, [char]0x1F9FE, [char]0x1F48E, [char]0x2696, [char]0x1FA9C, [char]0x1FAA3, [char]0x1F9F0,
    [char]0x1F464, [char]0x1F465, [char]0x1F5D1, [char]0x1F5E8, [char]0x1F5EF, [char]0x1F39E, [char]0x1F6D2, [char]0x1F6AC, [char]0x26B0, [char]0x1FAA6,
    [char]0x26B1, [char]0x1F5FF, [char]0x1FAA7, [char]0x1F3E7, [char]0x1F6AE, [char]0x1F6B0, [char]0x267F, [char]0x1F6B9, [char]0x1F6BA, [char]0x1F6BB,
    [char]0x1F6BC, [char]0x1F6BE, [char]0x1F6C2, [char]0x1F6C3, [char]0x1F6C4, [char]0x1F6C5, [char]0x26A0, [char]0x1F6B8, [char]0x26D4, [char]0x1F6AB,
    [char]0x1F6B3, [char]0x1F6AD, [char]0x1F6AF, [char]0x1F6B1, [char]0x1F6B7, [char]0x1F4F5, [char]0x1F51E, [char]0x2622, [char]0x2623
)

Write-Host "Eliminando emojis de archivos .cs en: $projectPath" -ForegroundColor Cyan
Write-Host ""

Get-ChildItem -Path $projectPath -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    $file = $_
    
    try {
        $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        $originalLength = $content.Length
        $modified = $false
        
        foreach ($emoji in $emojis) {
            if ($content.Contains($emoji)) {
                $content = $content.Replace([string]$emoji, '')
                $modified = $true
            }
        }
        
        # También eliminar variantes con selector FE0F
        $content = $content -replace '\uFE0F', ''
        $content = $content -replace '\u200D', ''
        
        $charsRemoved = $originalLength - $content.Length
        
        if ($charsRemoved -gt 0) {
            [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]::new($false))
            Write-Host "OK $($file.Name) - $charsRemoved caracteres eliminados" -ForegroundColor Green
            $filesProcessed++
            $totalCharsRemoved += $charsRemoved
        }
    }
    catch {
        Write-Host "ERROR $($file.Name): $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Archivos procesados: $filesProcessed" -ForegroundColor Cyan
Write-Host "Total caracteres eliminados: $totalCharsRemoved" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Yellow
