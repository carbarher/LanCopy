# Script para comentar el método BrowseUserFromCandidate que causa errores de compilación

$filePath = "c:\p2p\SlskDown\MainForm.cs"
$content = Get-Content $filePath -Raw -Encoding UTF8

# Buscar y reemplazar el método problemático
$pattern = '(?s)(        private async Task BrowseUserFromCandidate\(\).*?        \})'
$replacement = @'
        // NOTA: Este método requiere acceso a lvFiles que se crea dinámicamente en la pestaña Automático
        // La funcionalidad de "explorar usuario" desde candidatos debe implementarse en el código
        // que crea la pestaña Automático, donde lvFiles está disponible
        /*
        private async Task BrowseUserFromCandidate()
        {
            if (lvFiles.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecciona un archivo para explorar los archivos de su usuario.", 
                    "Explorar Usuario", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            try
            {
                var item = lvFiles.SelectedItems[0];
                var file = item.Tag as AutoSearchFileResult;
                
                if (file == null || string.IsNullOrWhiteSpace(file.Username))
                {
                    MessageBox.Show("No se pudo obtener el nombre de usuario del archivo seleccionado.", 
                        "Explorar Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                await BrowseUserFiles(file.Username);
            }
            catch (Exception ex)
            {
                AutoLog($"❌ Error explorando usuario desde candidatos: {ex.Message}");
                MessageBox.Show($"Error al explorar archivos del usuario:\n{ex.Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        */
'@

if ($content -match $pattern) {
    $newContent = $content -replace $pattern, $replacement
    Set-Content $filePath -Value $newContent -Encoding UTF8 -NoNewline
    Write-Host "✅ Método BrowseUserFromCandidate comentado exitosamente"
    Write-Host "Ejecuta 'lanza' para compilar"
} else {
    Write-Host "❌ No se encontró el método BrowseUserFromCandidate"
    Write-Host "El archivo puede ya estar corregido o tener un formato diferente"
}
