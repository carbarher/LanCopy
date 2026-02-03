# Script mejorado para eliminar todos los emojis de archivos .cs en SlskDown

$projectPath = "c:\p2p\SlskDown"
$encoding = [System.Text.UTF8Encoding]::new($false)

$filesProcessed = 0
$totalEmojisRemoved = 0

# Lista de emojis comunes a eliminar (basado en el grep anterior)
$emojis = @(
    '🔍', '🔎', '📁', '📂', '📄', '📊', '📈', '📉', '💾', '💿',
    '🔧', '🔨', '⚙️', '🚀', '🎯', '✅', '❌', '⚠️', '🔴', '🟢',
    '🟡', '🔵', '⭐', '💡', '🔥', '❄️', '⏰', '⏱️', '📅', '📆',
    '🌐', '🌍', '🌎', '🔗', '🔓', '🔒', '🔐', '🔑', '📝', '📋',
    '📌', '📍', '🎵', '🎶', '🎧', '🎤', '🎬', '🎮', '🎲', '🎰',
    '🎨', '🎭', '🎪', '🎢', '🎡', '🎠', '🎟️', '🎫', '🎖️', '🏆',
    '🏅', '🥇', '🥈', '🥉', '💰', '💸', '💵', '💴', '💶', '💷',
    '💳', '💎', '📱', '📲', '☎️', '📞', '📟', '📠', '🖥️', '💻',
    '⌨️', '🖱️', '🖨️', '📀', '💽', '💾', '💿', '📷', '📸', '📹',
    '🎥', '📽️', '🎬', '📺', '📻', '📡', '🔊', '🔉', '🔈', '🔇',
    '📢', '📣', '📯', '🔔', '🔕', '🎼', '🎵', '🎶', '🎙️', '🎚️',
    '🎛️', '⏸️', '⏯️', '⏹️', '⏺️', '⏭️', '⏮️', '⏩', '⏪', '⏫',
    '⏬', '◀️', '🔼', '🔽', '➡️', '⬅️', '⬆️', '⬇️', '↗️', '↘️',
    '↙️', '↖️', '↕️', '↔️', '↪️', '↩️', '⤴️', '⤵️', '🔃', '🔄',
    '🔙', '🔚', '🔛', '🔜', '🔝', '🔀', '🔁', '🔂', '▶️', '⚡',
    '🌟', '✨', '💫', '🔆', '🔅', '☀️', '🌙', '⭕', '❗', '❓',
    '💬', '💭', '🗨️', '🗯️', '💥', '💢', '💤', '💦', '💧', '💨',
    '🎉', '🎊', '🎈', '🎀', '🎁', '🏁', '🚩', '🎌', '🏴', '🏳️',
    '📦', '📧', '📨', '📩', '📤', '📥', '📮', '📪', '📫', '📬',
    '📭', '🗂️', '🗃️', '🗄️', '🗑️', '🗒️', '🗓️', '📇', '📈', '📉',
    '📊', '📃', '📜', '📄', '📑', '🔖', '🏷️', '💼', '📁', '📂',
    '🗂️', '🗞️', '📰', '📓', '📔', '📒', '📕', '📗', '📘', '📙',
    '📚', '📖', '🔗', '📎', '🖇️', '📐', '📏', '📌', '📍', '✂️',
    '🖊️', '🖋️', '✒️', '🖌️', '🖍️', '📝', '✏️', '🔍', '🔎', '🔏',
    '🔐', '🔒', '🔓', '🔑', '🗝️', '🔨', '🪓', '⛏️', '⚒️', '🛠️',
    '🗡️', '⚔️', '🔫', '🪃', '🏹', '🛡️', '🪚', '🔧', '🪛', '🔩',
    '⚙️', '🗜️', '⚖️', '🦯', '🔗', '⛓️', '🪝', '🧰', '🧲', '🪜',
    '⏱️', '⏲️', '⏰', '🕰️', '⌛', '⏳', '📡', '🔋', '🪫', '🔌',
    '💡', '🔦', '🕯️', '🪔', '🧯', '🛢️', '💸', '💵', '💴', '💶',
    '💷', '🪙', '💰', '💳', '🧾', '💎', '⚖️', '🪜', '🪣', '🧰'
)

Get-ChildItem -Path $projectPath -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $content = [System.IO.File]::ReadAllText($file.FullName, $encoding)
    $originalContent = $content
    $emojisInFile = 0
    
    foreach ($emoji in $emojis) {
        if ($content.Contains($emoji)) {
            $beforeLength = $content.Length
            $content = $content.Replace($emoji, '')
            $emojisInFile += ($beforeLength - $content.Length)
        }
    }
    
    if ($emojisInFile -gt 0) {
        [System.IO.File]::WriteAllText($file.FullName, $content, $encoding)
        Write-Host "Procesado: $($file.Name) - $emojisInFile caracteres eliminados"
        $filesProcessed++
        $totalEmojisRemoved += $emojisInFile
    }
}

Write-Host ""
Write-Host "=========================================="
Write-Host "Resumen:"
Write-Host "Archivos procesados: $filesProcessed"
Write-Host "Caracteres emoji eliminados: $totalEmojisRemoved"
Write-Host "=========================================="
