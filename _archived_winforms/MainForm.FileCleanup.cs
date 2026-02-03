using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    public partial class MainForm
    {
        /// <summary>
        /// Analiza archivos descargados, detecta cuáles no están en español y los mueve a carpeta separada
        /// </summary>
        private async Task CleanupNonSpanishFiles(string downloadPath, bool useAI = false)
        {
            try
            {
                if (!Directory.Exists(downloadPath))
                {
                    Log($"❌ Carpeta no existe: {downloadPath}");
                    return;
                }

                Log("🧹 Iniciando limpieza de archivos no españoles...");
                
                // Carpeta destino para archivos no españoles
                var nonSpanishFolder = Path.Combine(downloadPath, "_no_español");
                Directory.CreateDirectory(nonSpanishFolder);
                
                // Obtener todos los archivos de documentos
                var documentExtensions = new[] { ".epub", ".mobi", ".azw3", ".pdf", ".txt", ".doc", ".docx" };
                var allFiles = Directory.GetFiles(downloadPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => documentExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .Where(f => !f.Contains("_no_español")) // Excluir carpeta de destino
                    .ToList();
                
                if (allFiles.Count == 0)
                {
                    Log("✅ No hay archivos para analizar");
                    return;
                }
                
                Log($"📚 Analizando {allFiles.Count} archivos...");
                
                int movedCount = 0;
                int spanishCount = 0;
                int errorCount = 0;
                
                foreach (var filePath in allFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        bool isSpanish = false;
                        
                        if (useAI && aiEnabled && ollamaClient != null)
                        {
                            // Usar IA para detección más precisa
                            isSpanish = await DetectSpanishWithAI(fileName, filePath);
                        }
                        else
                        {
                            // Usar método rápido basado en reglas
                            isSpanish = IsSpanishText(fileName);
                        }
                        
                        if (!isSpanish)
                        {
                            // Mover a carpeta de no españoles
                            var destPath = Path.Combine(nonSpanishFolder, fileName);
                            
                            // Si ya existe, agregar sufijo numérico
                            int counter = 1;
                            while (File.Exists(destPath))
                            {
                                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                                var ext = Path.GetExtension(fileName);
                                destPath = Path.Combine(nonSpanishFolder, $"{nameWithoutExt}_{counter}{ext}");
                                counter++;
                            }
                            
                            File.Move(filePath, destPath);
                            movedCount++;
                            Log($"  ➡️ Movido: {fileName}");
                        }
                        else
                        {
                            spanishCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Log($"  ❌ Error procesando {Path.GetFileName(filePath)}: {ex.Message}");
                    }
                }
                
                Log("");
                Log("═══════════════════════════════════════");
                Log("LIMPIEZA COMPLETADA");
                Log($"✅ Archivos en español: {spanishCount}");
                Log($"➡️ Archivos movidos: {movedCount}");
                if (errorCount > 0)
                    Log($"❌ Errores: {errorCount}");
                Log($"📁 Carpeta destino: {nonSpanishFolder}");
                Log("═══════════════════════════════════════");
                
                if (movedCount > 0)
                {
                    ShowNotification("Limpieza completada", 
                        $"{movedCount} archivos no españoles movidos a '{Path.GetFileName(nonSpanishFolder)}'", 
                        ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error en limpieza de archivos: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Usa IA (Ollama) para detectar si un archivo está en español con mayor precisión
        /// </summary>
        private async Task<bool> DetectSpanishWithAI(string fileName, string filePath)
        {
            try
            {
                // Primero verificar con método rápido
                if (IsSpanishText(fileName))
                    return true;
                
                // Si no está claro, usar IA para análisis más profundo
                if (ollamaClient == null)
                    return false;
                
                var available = await ollamaClient.IsAvailableAsync();
                if (!available)
                    return IsSpanishText(fileName); // Fallback
                
                // Extraer contenido del archivo según formato
                string fileContent = await ExtractFileContent(filePath);
                
                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    // Si no se pudo extraer contenido, usar solo nombre
                    return IsSpanishText(fileName);
                }
                
                // Prompt para IA
                string prompt = $"Analiza este texto y determina si está en ESPAÑOL. Responde SOLO 'SI' o 'NO'.\n\n" +
                               $"Archivo: {fileName}\n\n" +
                               $"Texto:\n{fileContent.Substring(0, Math.Min(500, fileContent.Length))}...";
                
                var response = await ollamaClient.GetCompletionAsync(prompt, 
                    "Eres un detector de idioma experto. Analiza el texto y responde SOLO 'SI' si está en español, o 'NO' si está en otro idioma.");
                
                bool isSpanish = response?.ToUpper().Contains("SI") == true;
                
                Log($"  🤖 IA: {fileName} → {(isSpanish ? "ESPAÑOL" : "OTRO IDIOMA")}");
                
                return isSpanish;
            }
            catch (Exception ex)
            {
                Log($"  ⚠️ Error en IA para {fileName}: {ex.Message}");
                // En caso de error, usar método tradicional
                return IsSpanishText(fileName);
            }
        }
        
        /// <summary>
        /// Extrae contenido de texto de diferentes formatos de archivo
        /// </summary>
        private async Task<string> ExtractFileContent(string filePath)
        {
            try
            {
                var ext = Path.GetExtension(filePath).ToLower();
                
                switch (ext)
                {
                    case ".txt":
                        return await ExtractTextFromTxt(filePath);
                    
                    case ".pdf":
                        return await ExtractTextFromPdf(filePath);
                    
                    case ".epub":
                        return await ExtractTextFromEpub(filePath);
                    
                    case ".mobi":
                    case ".azw3":
                        // MOBI/AZW3 requieren conversión, usar solo nombre
                        return "";
                    
                    default:
                        return "";
                }
            }
            catch
            {
                return "";
            }
        }
        
        /// <summary>
        /// Extrae texto de archivo TXT
        /// </summary>
        private async Task<string> ExtractTextFromTxt(string filePath)
        {
            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    char[] buffer = new char[1000];
                    int read = await reader.ReadAsync(buffer, 0, buffer.Length);
                    return new string(buffer, 0, read);
                }
            }
            catch
            {
                return "";
            }
        }
        
        /// <summary>
        /// Extrae texto de archivo PDF usando iText7
        /// </summary>
        private async Task<string> ExtractTextFromPdf(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var pdfReader = new iText.Kernel.Pdf.PdfReader(filePath))
                    using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(pdfReader))
                    {
                        var text = new System.Text.StringBuilder();
                        
                        // Leer primeras 3 páginas
                        int pagesToRead = Math.Min(3, pdfDoc.GetNumberOfPages());
                        
                        for (int i = 1; i <= pagesToRead; i++)
                        {
                            var page = pdfDoc.GetPage(i);
                            var strategy = new iText.Kernel.Pdf.Canvas.Parser.Listener.LocationTextExtractionStrategy();
                            var pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page, strategy);
                            text.AppendLine(pageText);
                            
                            // Limitar a 1000 caracteres
                            if (text.Length > 1000)
                                break;
                        }
                        
                        return text.ToString();
                    }
                }
                catch
                {
                    return "";
                }
            });
        }
        
        /// <summary>
        /// Extrae texto de archivo EPUB usando VersOne.Epub
        /// </summary>
        private async Task<string> ExtractTextFromEpub(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var book = VersOne.Epub.EpubReader.ReadBook(filePath);
                    var text = new System.Text.StringBuilder();
                    
                    // Intentar obtener contenido de los primeros capítulos
                    int count = 0;
                    foreach (var item in book.Content.Html.Local)
                    {
                        try
                        {
                            var content = item.Content;
                            
                            if (string.IsNullOrEmpty(content))
                                continue;
                            
                            // Limpiar HTML tags básicos
                            content = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]+>", " ");
                            content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ");
                            
                            text.AppendLine(content);
                            
                            count++;
                            
                            // Limitar a 1000 caracteres o 2 capítulos
                            if (text.Length > 1000 || count >= 2)
                                break;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    
                    return text.ToString();
                }
                catch
                {
                    return "";
                }
            });
        }
        
        /// <summary>
        /// Crea botón de limpieza en la pestaña de descargas
        /// </summary>
        private Button CreateCleanupButton()
        {
            var btnCleanup = CreateStyledButton("🧹 LIMPIAR NO ESPAÑOL", Color.FromArgb(138, 43, 226), 200, 45);
            btnCleanup.Margin = new Padding(0, 0, 8, 8);
            AddTooltip(btnCleanup, "Analiza archivos descargados, detecta cuáles no están en español y los mueve a carpeta '_no_español'");
            
            btnCleanup.Click += async (s, e) =>
            {
                var result = MessageBox.Show(
                    "¿Deseas usar IA (Ollama) para detección más precisa?\n\n" +
                    "• SÍ: Usa IA para análisis profundo (más lento pero preciso)\n" +
                    "• NO: Usa detección rápida basada en reglas\n" +
                    "• CANCELAR: Abortar operación",
                    "Limpieza de archivos no españoles",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.Cancel)
                    return;
                
                bool useAI = (result == DialogResult.Yes);
                
                if (useAI && (!aiEnabled || ollamaClient == null))
                {
                    MessageBox.Show(
                        "IA no está disponible.\n\n" +
                        "Para usar IA:\n" +
                        "1. Activa IA en la pestaña CONFIG\n" +
                        "2. Asegúrate de que Ollama esté ejecutándose\n\n" +
                        "Usando detección rápida...",
                        "IA no disponible",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    useAI = false;
                }
                
                btnCleanup.Enabled = false;
                btnCleanup.Text = "⏳ LIMPIANDO...";
                
                try
                {
                    var downloadPath = txtDownloadDir?.Text ?? downloadDir;
                    await CleanupNonSpanishFiles(downloadPath, useAI);
                }
                finally
                {
                    btnCleanup.Enabled = true;
                    btnCleanup.Text = "🧹 LIMPIAR NO ESPAÑOL";
                }
            };
            
            return btnCleanup;
        }
    }
}
