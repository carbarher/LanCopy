# Script para eliminar TODOS los emojis de archivos .cs
$projectPath = "c:\p2p\SlskDown"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "ELIMINADOR DE EMOJIS - SlskDown" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$totalFiles = 0
$totalCharsRemoved = 0

# Obtener todos los archivos .cs
$csFiles = Get-ChildItem -Path $projectPath -Filter "*.cs" -Recurse

Write-Host "Archivos .cs encontrados: $($csFiles.Count)" -ForegroundColor Yellow
Write-Host ""

foreach ($file in $csFiles) {
    try {
        # Leer contenido
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        $originalLength = $content.Length
        
        # Eliminar emojis usando múltiples patrones
        # Patrón 1: Emojis comunes (U+1F300 - U+1F9FF)
        $content = $content -replace '[\u{1F300}-\u{1F9FF}]', ''
        
        # Patrón 2: Símbolos misceláneos (U+2600 - U+27BF)
        $content = $content -replace '[\u{2600}-\u{27BF}]', ''
        
        # Patrón 3: Dingbats (U+2700 - U+27BF)
        $content = $content -replace '[\u{2700}-\u{27BF}]', ''
        
        # Patrón 4: Símbolos técnicos misceláneos
        $content = $content -replace '[\u{2300}-\u{23FF}]', ''
        
        # Patrón 5: Símbolos geométricos
        $content = $content -replace '[\u{25A0}-\u{25FF}]', ''
        
        # Patrón 6: Flechas suplementarias
        $content = $content -replace '[\u{2900}-\u{297F}]', ''
        
        # Patrón 7: Emojis extendidos (U+1FA00 - U+1FAFF)
        $content = $content -replace '[\u{1FA00}-\u{1FAFF}]', ''
        
        # Patrón 8: Banderas (U+1F1E0 - U+1F1FF)
        $content = $content -replace '[\u{1F1E0}-\u{1F1FF}]', ''
        
        # Patrón 9: Selector de variación
        $content = $content -replace '\uFE0F', ''
        
        # Patrón 10: Zero Width Joiner
        $content = $content -replace '\u200D', ''
        
        # Patrón 11: Emojis específicos problemáticos
        $content = $content -replace '[⭐⚠️✅💾📂📚🔄⏳🟡🟢📊⚡🎯💡📋📄📁🗑️⏸️▶️⬇️🔵👤🚫✓📖🌐🔍📥📉🌍🚩⏭️📝📏⏱️🎉💬🔓🔒🔑🎵💰📱💻⏯⏹⏺➡⬅⬆⬇↗↘↙↖↕↔↪↩⤴⤵🔃🔙🔚🔛🔜🔝🔀🔁🔂🌟✨💫🔆🔅☀🌙⭕❗❓💭🗨🗯💥💢💤💦💧💨🎊🎈🎀🎁🏁🎌🏴🏳📦📧📨📩📤🗂🗃🗄🗒🗓📇📃📜📑🔖🏷💼🗞📰📓📔📒📕📗📘📙📎🖇📐✂🖊🖋✒🖌🖍✏🔏🗝🪓⛏⚒🛠🗡⚔🔫🪃🏹🛡🪚🪛🔩🗜⚖🦯⛓🪝🧰🧲🪜⏲🕰⌛🔋🪫🔌🔦🕯🪔🧯🛢🪙🧾🪣👥🎞🛒🚬⚰🪦⚱🗿🪧🏧🚮🚰♿🚹🚺🚻🚼🚾🛂🛃🛄🛅🚸⛔🚳🚭🚯🚱🚷📵🔞☢☣🛑🔥❌⏏⚙️🐢➕🔴🟠🔘]', ''
        
        $charsRemoved = $originalLength - $content.Length
        
        if ($charsRemoved -gt 0) {
            # Guardar archivo modificado
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            
            $relativePath = $file.FullName.Replace($projectPath + "\", "")
            Write-Host "✓ $relativePath" -ForegroundColor Green
            Write-Host "  Caracteres eliminados: $charsRemoved" -ForegroundColor Gray
            
            $totalFiles++
            $totalCharsRemoved += $charsRemoved
        }
    }
    catch {
        Write-Host "✗ Error en $($file.Name): $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "RESUMEN" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Archivos modificados: $totalFiles" -ForegroundColor Yellow
Write-Host "Total caracteres eliminados: $totalCharsRemoved" -ForegroundColor Yellow
Write-Host ""
Write-Host "✓ PROCESO COMPLETADO" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
