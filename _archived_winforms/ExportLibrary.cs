// ARCHIVO DESHABILITADO - Métodos duplicados con MainForm.cs
// Los métodos ExtractAuthorFromFilename y FormatFileSize ya existen en MainForm.cs
// Este archivo partial class ha sido vaciado para evitar errores de compilación CS0111

namespace SlskDown
{
    // Clase partial vacía - métodos movidos a MainForm.cs
    public partial class MainForm
    {
        // Método ExportLibraryAsync comentado - duplicado
        /*private async Task ExportLibraryAsync()
        {
            if (string.IsNullOrWhiteSpace(downloadDir) || !Directory.Exists(downloadDir))
            {
                MessageBox.Show($"La carpeta de descargas no existe:\n{downloadDir}", "Carpeta no encontrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new Form())
            {
                dialog.Text = "Exportar Biblioteca";
                dialog.Size = new System.Drawing.Size(450, 300);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                var panel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.TopDown,
                    Padding = new Padding(20),
                    AutoScroll = true
                };

                var lblTitle = new Label
                {
                    Text = "Selecciona el formato de exportación:",
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Margin = new Padding(0, 0, 0, 15)
                };
                panel.Controls.Add(lblTitle);

                var rbCSV = new RadioButton { Text = "📊 CSV (Excel compatible)", AutoSize = true, Checked = true, Margin = new Padding(0, 5, 0, 5) };
                var rbJSON = new RadioButton { Text = "📋 JSON (estructurado)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                var rbTXT = new RadioButton { Text = "📄 TXT (legible)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                var rbMarkdown = new RadioButton { Text = "📝 Markdown (documentación)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };

                panel.Controls.Add(rbCSV);
                panel.Controls.Add(rbJSON);
                panel.Controls.Add(rbTXT);
                panel.Controls.Add(rbMarkdown);

                var chkValidatedOnly = new CheckBox
                {
                    Text = "Solo incluir archivos validados",
                    AutoSize = true,
                    Checked = false,
                    Margin = new Padding(0, 15, 0, 5)
                };
                panel.Controls.Add(chkValidatedOnly);

                var btnPanel = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    AutoSize = true,
                    Margin = new Padding(0, 20, 0, 0)
                };

                var btnExport = new Button
                {
                    Text = "Exportar",
                    Size = new System.Drawing.Size(100, 35),
                    BackColor = Color.FromArgb(50, 100, 120),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold)
                };
                btnExport.FlatAppearance.BorderSize = 0;

                var btnCancel = new Button
                {
                    Text = "Cancelar",
                    Size = new System.Drawing.Size(100, 35),
                    BackColor = Color.FromArgb(100, 100, 100),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Margin = new Padding(10, 0, 0, 0)
                };
                btnCancel.FlatAppearance.BorderSize = 0;
                btnCancel.Click += (s, e) => dialog.Close();

                btnPanel.Controls.Add(btnExport);
                btnPanel.Controls.Add(btnCancel);
                panel.Controls.Add(btnPanel);

                dialog.Controls.Add(panel);

                btnExport.Click += async (s, e) =>
                {
                    dialog.Enabled = false;
                    UseWaitCursor = true;

                    try
                    {
                        string format = rbCSV.Checked ? "csv" :
                                       rbJSON.Checked ? "json" :
                                       rbTXT.Checked ? "txt" : "md";

                        bool validatedOnly = chkValidatedOnly.Checked;

                        var result = await Task.Run(() => GenerateLibraryExport(format, validatedOnly));

                        if (result.Success)
                        {
                            AutoLog($"📤 Biblioteca exportada: {result.FilePath} ({result.FileCount} archivos)");
                            
                            var confirmOpen = MessageBox.Show(
                                $"Biblioteca exportada exitosamente:\n\n" +
                                $"📁 {result.FilePath}\n" +
                                $"📚 {result.FileCount} archivos\n" +
                                $"💾 {FormatFileSize(result.FileSize)}\n\n" +
                                $"¿Deseas abrir el archivo?",
                                "Exportación completada",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information);

                            if (confirmOpen == DialogResult.Yes)
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = result.FilePath,
                                    UseShellExecute = true
                                });
                            }

                            dialog.Close();
                        }
                        else
                        {
                            MessageBox.Show($"Error exportando biblioteca:\n{result.Error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error exportando biblioteca:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        UseWaitCursor = false;
                        dialog.Enabled = true;
                    }
                };

                dialog.ShowDialog(this);
            }
        }

        private (bool Success, string FilePath, int FileCount, long FileSize, string Error) GenerateLibraryExport(string format, bool validatedOnly)
        {
            try
            {
                var files = Directory.GetFiles(downloadDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => IsDocumentFile(Path.GetFileName(f)))
                    .Where(f => !f.Contains("_NoEspanol") && !f.Contains("_Corruptos"))
                    .ToList();

                if (files.Count == 0)
                {
                    return (false, "", 0, 0, "No hay archivos en la biblioteca para exportar.");
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var extension = format == "md" ? "md" : format;
                var outputPath = Path.Combine(downloadDir, $"biblioteca_{timestamp}.{extension}");

                string content = format switch
                {
                    "csv" => GenerateCSV(files),
                    "json" => GenerateJSON(files),
                    "txt" => GenerateTXT(files),
                    "md" => GenerateMarkdown(files),
                    _ => throw new ArgumentException($"Formato no soportado: {format}")
                };

                File.WriteAllText(outputPath, content, Encoding.UTF8);

                var fileInfo = new FileInfo(outputPath);
                return (true, outputPath, files.Count, fileInfo.Length, "");
            }
            catch (Exception ex)
            {
                return (false, "", 0, 0, ex.Message);
            }
        }

        private string GenerateCSV(List<string> files)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Título,Autor,Formato,Tamaño (MB),Fecha Modificación,Ruta");

            foreach (var file in files.OrderBy(f => Path.GetFileName(f)))
            {
                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(file).TrimStart('.');
                var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                var relativePath = Path.GetRelativePath(downloadDir, file);

                var author = ExtractAuthorFromFilename(fileName);
                var title = ExtractTitleFromFilename(fileName);

                sb.AppendLine($"\"{title}\",\"{author}\",{extension},{sizeMB:F2},{fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss},\"{relativePath}\"");
            }

            return sb.ToString();
        }

        private string GenerateJSON(List<string> files)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"fecha_exportacion\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            sb.AppendLine($"  \"total_archivos\": {files.Count},");
            sb.AppendLine($"  \"carpeta_base\": \"{downloadDir.Replace("\\", "\\\\")}\",");
            sb.AppendLine("  \"biblioteca\": [");

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(file).TrimStart('.');
                var relativePath = Path.GetRelativePath(downloadDir, file).Replace("\\", "/");

                var author = ExtractAuthorFromFilename(fileName);
                var title = ExtractTitleFromFilename(fileName);

                sb.AppendLine("    {");
                sb.AppendLine($"      \"titulo\": \"{EscapeJson(title)}\",");
                sb.AppendLine($"      \"autor\": \"{EscapeJson(author)}\",");
                sb.AppendLine($"      \"formato\": \"{extension}\",");
                sb.AppendLine($"      \"tamaño_bytes\": {fileInfo.Length},");
                sb.AppendLine($"      \"tamaño_mb\": {fileInfo.Length / (1024.0 * 1024.0):F2},");
                sb.AppendLine($"      \"fecha_modificacion\": \"{fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\",");
                sb.AppendLine($"      \"ruta_relativa\": \"{relativePath}\"");
                sb.Append("    }");
                
                if (i < files.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateTXT(List<string> files)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  BIBLIOTECA DIGITAL - {files.Count} libros");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Fecha de exportación: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Carpeta base: {downloadDir}");
            sb.AppendLine();

            var byFormat = files.GroupBy(f => Path.GetExtension(f).TrimStart('.').ToUpperInvariant())
                .OrderByDescending(g => g.Count());

            sb.AppendLine("Resumen por formato:");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");
            foreach (var group in byFormat)
            {
                var totalSize = group.Sum(f => new FileInfo(f).Length);
                sb.AppendLine($"  {group.Key,-10} {group.Count(),6} archivos  ({FormatFileSize(totalSize)})");
            }
            sb.AppendLine();

            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("  LISTADO COMPLETO");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            foreach (var file in files.OrderBy(f => Path.GetFileName(f)))
            {
                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(file).TrimStart('.').ToUpperInvariant();
                
                var author = ExtractAuthorFromFilename(fileName);
                var title = ExtractTitleFromFilename(fileName);

                sb.AppendLine($"[{extension,-5}] {title}");
                if (!string.IsNullOrEmpty(author))
                    sb.AppendLine($"        Autor: {author}");
                sb.AppendLine($"        Tamaño: {FormatFileSize(fileInfo.Length)}");
                sb.AppendLine($"        Fecha: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerateMarkdown(List<string> files)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Mi Biblioteca Digital");
            sb.AppendLine();
            sb.AppendLine($"**Fecha de exportación:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}  ");
            sb.AppendLine($"**Total de archivos:** {files.Count}  ");
            sb.AppendLine($"**Carpeta base:** `{downloadDir}`");
            sb.AppendLine();

            var byFormat = files.GroupBy(f => Path.GetExtension(f).TrimStart('.').ToUpperInvariant())
                .OrderByDescending(g => g.Count());

            sb.AppendLine("## 📊 Resumen por formato");
            sb.AppendLine();
            sb.AppendLine("| Formato | Cantidad | Tamaño Total |");
            sb.AppendLine("|---------|----------|--------------|");
            foreach (var group in byFormat)
            {
                var totalSize = group.Sum(f => new FileInfo(f).Length);
                sb.AppendLine($"| {group.Key} | {group.Count()} | {FormatFileSize(totalSize)} |");
            }
            sb.AppendLine();

            var byAuthor = files
                .Select(f => new { File = f, Author = ExtractAuthorFromFilename(Path.GetFileNameWithoutExtension(f)) })
                .Where(x => !string.IsNullOrEmpty(x.Author))
                .GroupBy(x => x.Author)
                .OrderByDescending(g => g.Count())
                .Take(20);

            if (byAuthor.Any())
            {
                sb.AppendLine("## 👤 Top 20 Autores");
                sb.AppendLine();
                sb.AppendLine("| Autor | Libros |");
                sb.AppendLine("|-------|--------|");
                foreach (var group in byAuthor)
                {
                    sb.AppendLine($"| {group.Key} | {group.Count()} |");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## 📚 Listado completo");
            sb.AppendLine();

            foreach (var file in files.OrderBy(f => Path.GetFileName(f)))
            {
                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(file).TrimStart('.').ToUpperInvariant();
                
                var author = ExtractAuthorFromFilename(fileName);
                var title = ExtractTitleFromFilename(fileName);

                sb.Append($"- **[{extension}]** {title}");
                if (!string.IsNullOrEmpty(author))
                    sb.Append($" - *{author}*");
                sb.AppendLine($" ({FormatFileSize(fileInfo.Length)})");
            }

            return sb.ToString();
        }

        private string ExtractAuthorFromFilename_DISABLED(string filename)
        {
            var parts = filename.Split(new[] { " - ", " – " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return parts[0].Trim();
            }
            return "";
        }

        private string ExtractTitleFromFilename(string filename)
        {
            var parts = filename.Split(new[] { " - ", " – " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return string.Join(" - ", parts.Skip(1)).Trim();
            }
            return filename.Trim();
        }

        private string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private string FormatFileSize_DISABLED(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
        */
    }
}
