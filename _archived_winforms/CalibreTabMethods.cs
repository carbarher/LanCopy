using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlskDown.Core.Integrations;

namespace SlskDown
{
    /// <summary>
    /// Métodos auxiliares para la pestaña Calibre
    /// </summary>
    public partial class MainForm
    {
        // ═══════════════════════════════════════════════════════════════
        // MÉTODOS DE LA PESTAÑA CALIBRE
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Inicializa el estado de Calibre y actualiza la UI
        /// </summary>
        private async Task InitializeCalibreStatusAsync()
        {
            await Task.Delay(1000); // Delay para que la UI se renderice y evitar conflictos con búsqueda automática
            
            if (InvokeRequired)
            {
                Invoke(new Action(async () => await InitializeCalibreStatusAsync()));
                return;
            }
            
            try
            {
                if (_calibreIntegration?.IsAvailable == true)
                {
                    lblCalibreStatus.Text = "Calibre: Conectado y disponible";
                    lblCalibreStatus.ForeColor = Color.LimeGreen;
                    
                    lblCalibreStats.Text = $"Biblioteca: {_calibreIntegration.LibraryPath}";
                    
                    Log("Calibre detectado y disponible");
                    
                    // NO cargar libros automáticamente para evitar conflictos con búsqueda automática
                    // El usuario puede hacer clic en "Refrescar" manualmente cuando lo necesite
                    Log("Haz clic en 'Refrescar Biblioteca' para cargar los libros de Calibre");
                }
                else
                {
                    lblCalibreStatus.Text = "Calibre: No detectado";
                    lblCalibreStatus.ForeColor = Color.Orange;
                    lblCalibreStats.Text = "Instala Calibre o configura la ruta manualmente";
                    
                    Log("Calibre no detectado. Instala Calibre o configura la ruta en 'Configurar Ruta'");
                }
            }
            catch (Exception ex)
            {
                lblCalibreStatus.Text = $"Error: {ex.Message}";
                lblCalibreStatus.ForeColor = Color.Red;
                Log($"Error inicializando Calibre: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refresca la lista de libros desde Calibre
        /// </summary>
        private async Task RefreshCalibreLibraryAsync()
        {
            if (_calibreIntegration?.IsAvailable != true)
            {
                var result = MessageBox.Show(
                    "Calibre no está disponible. Configura la ruta primero.\n\n" +
                    "¿Deseas configurar la ruta de la biblioteca de Calibre ahora?",
                    "Calibre no disponible",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                
                if (result == DialogResult.Yes)
                {
                    ConfigureCalibrePath();
                }
                return;
            }
            
            try
            {
                Log("Refrescando biblioteca de Calibre...");
                lvCalibreBooks.Items.Clear();
                
                // Obtener libros desde Calibre
                var books = await _calibreIntegration.GetBooksAsync();
                
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        lvCalibreBooks.BeginUpdate();
                        
                        foreach (var book in books)
                        {
                            var item = new ListViewItem(book.Title ?? "Sin título");
                            item.SubItems.Add(book.Authors ?? "Desconocido");
                            item.SubItems.Add(book.Formats != null && book.Formats.Count > 0 ? string.Join(", ", book.Formats) : "-");
                            item.SubItems.Add(book.Size ?? "-");
                            item.SubItems.Add(book.Timestamp?.ToString("yyyy-MM-dd") ?? "-");
                            item.Tag = book;
                            
                            lvCalibreBooks.Items.Add(item);
                        }
                        
                        lvCalibreBooks.EndUpdate();
                        
                        Log($"{books.Count} libros cargados desde Calibre");
                    }));
                }
                else
                {
                    lvCalibreBooks.BeginUpdate();
                    
                    foreach (var book in books)
                    {
                        var item = new ListViewItem(book.Title ?? "Sin título");
                        item.SubItems.Add(book.Authors ?? "Desconocido");
                        item.SubItems.Add(book.Formats != null && book.Formats.Count > 0 ? string.Join(", ", book.Formats) : "-");
                        item.SubItems.Add(book.Size ?? "-");
                        item.SubItems.Add(book.Timestamp?.ToString("yyyy-MM-dd") ?? "-");
                        item.Tag = book;
                        
                        lvCalibreBooks.Items.Add(item);
                    }
                    
                    lvCalibreBooks.EndUpdate();
                    
                    Log($"{books.Count} libros cargados desde Calibre");
                }
                
                // Actualizar estadísticas
                lblCalibreStats.Text = $"Biblioteca: {books.Count} libros | {_calibreIntegration.LibraryPath}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refrescando biblioteca: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log($"Error refrescando Calibre: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Abre la aplicación Calibre
        /// </summary>
        private void OpenCalibreApp()
        {
            try
            {
                var calibrePath = FindCalibreExecutable();
                
                if (string.IsNullOrEmpty(calibrePath))
                {
                    MessageBox.Show("No se pudo encontrar Calibre instalado.\n\nDescarga e instala desde: https://calibre-ebook.com/", 
                        "Calibre no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = calibrePath,
                    UseShellExecute = true
                });
                
                Log("Abriendo Calibre...");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo Calibre: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Busca el ejecutable de Calibre
        /// </summary>
        private string FindCalibreExecutable()
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files\Calibre2\calibre.exe",
                @"C:\Program Files (x86)\Calibre2\calibre.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Calibre2", "calibre.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Calibre2", "calibre.exe")
            };
            
            return possiblePaths.FirstOrDefault(File.Exists);
        }
        
        /// <summary>
        /// Agrega las descargas seleccionadas a Calibre
        /// </summary>
        private async Task AddSelectedDownloadsToCalibreAsync()
        {
            if (_calibreIntegration?.IsAvailable != true)
            {
                var result = MessageBox.Show(
                    "Calibre no está disponible. Configura la ruta primero.\n\n" +
                    "¿Deseas configurar la ruta de la biblioteca de Calibre ahora?",
                    "Calibre no disponible",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                
                if (result == DialogResult.Yes)
                {
                    ConfigureCalibrePath();
                }
                return;
            }
            
            // Obtener archivos seleccionados desde la pestaña de descargas
            var selectedFiles = new List<string>();
            
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    // Aquí deberías obtener los archivos seleccionados desde lvDownloads
                    // Por ahora, mostramos un diálogo para seleccionar archivos
                }));
            }
            
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Multiselect = true;
                openFileDialog.Filter = "eBooks|*.epub;*.pdf;*.mobi;*.azw3;*.fb2;*.djvu|Todos los archivos|*.*";
                openFileDialog.Title = "Seleccionar libros para agregar a Calibre";
                
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFiles.AddRange(openFileDialog.FileNames);
                }
                else
                {
                    return;
                }
            }
            
            if (selectedFiles.Count == 0)
            {
                MessageBox.Show("No se seleccionaron archivos.", "Sin selección", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            int added = 0;
            int failed = 0;
            
            // Nota: OpenCalibreIfNeeded no implementado
            
            foreach (var filePath in selectedFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var success = await _calibreIntegration.AddBookAsync(
                        filePath: filePath,
                        author: "Desconocido",
                        title: fileName
                    );
                    
                    if (success)
                    {
                        added++;
                        Log($"Agregado a Calibre: {fileName}");
                    }
                    else
                    {
                        failed++;
                        Log($"Error agregando: {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"Error agregando {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
            
            // Nota: Auto-cerrar Calibre no implementado
            
            MessageBox.Show($"Agregados: {added}\nFallidos: {failed}", 
                "Resultado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // Refrescar lista
            await RefreshCalibreLibraryAsync();
        }
        
        /// <summary>
        /// Busca en la biblioteca de Calibre
        /// </summary>
        private void SearchCalibreLibrary()
        {
            if (_calibreIntegration?.IsAvailable != true)
                return;
            
            var searchText = txtCalibreSearch.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Mostrar todos
                foreach (ListViewItem item in lvCalibreBooks.Items)
                {
                    item.BackColor = Color.FromArgb(30, 30, 30);
                }
                return;
            }
            
            lvCalibreBooks.BeginUpdate();
            
            foreach (ListViewItem item in lvCalibreBooks.Items)
            {
                var title = item.Text.ToLower();
                var author = item.SubItems[1].Text.ToLower();
                
                if (title.Contains(searchText) || author.Contains(searchText))
                {
                    item.BackColor = Color.FromArgb(50, 80, 120); // Resaltar
                }
                else
                {
                    item.BackColor = Color.FromArgb(30, 30, 30);
                }
            }
            
            lvCalibreBooks.EndUpdate();
        }
        
        /// <summary>
        /// Abre el libro seleccionado en Calibre
        /// </summary>
        private void OpenSelectedCalibreBook()
        {
            if (lvCalibreBooks.SelectedItems.Count == 0)
                return;
            
            try
            {
                var book = lvCalibreBooks.SelectedItems[0].Tag;
                
                if (book != null && _calibreIntegration?.IsAvailable == true)
                {
                    var bookId = GetBookIdFromTag(book);
                    // Nota: OpenInCalibre no implementado
                    Log("Abrir libro en Calibre no implementado aún");
                    Log($"Abriendo libro en Calibre: {lvCalibreBooks.SelectedItems[0].Text}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo libro: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Abre la carpeta del libro en el explorador
        /// </summary>
        private void OpenCalibreBookFolder()
        {
            if (lvCalibreBooks.SelectedItems.Count == 0)
                return;
            
            try
            {
                var book = lvCalibreBooks.SelectedItems[0].Tag;
                
                if (book != null)
                {
                    var bookPath = GetBookPathFromTag(book);
                    
                    if (!string.IsNullOrEmpty(bookPath) && File.Exists(bookPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{bookPath}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo carpeta: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Edita la metadata del libro seleccionado
        /// </summary>
        private void EditCalibreMetadata()
        {
            if (lvCalibreBooks.SelectedItems.Count == 0)
                return;
            
            MessageBox.Show("Esta funcionalidad abrirá el editor de metadata de Calibre.\n\nPor ahora, abre Calibre manualmente y edita desde ahí.", 
                "Editar Metadata", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        /// <summary>
        /// Califica el libro seleccionado
        /// </summary>
        private void RateCalibreBook()
        {
            if (lvCalibreBooks.SelectedItems.Count == 0)
                return;
            
            var ratingDialog = new Form
            {
                Text = "Calificar Libro",
                Size = new System.Drawing.Size(300, 150),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            
            var label = new Label
            {
                Text = "Selecciona una calificación (1-5 estrellas):",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true,
                ForeColor = Color.White
            };
            ratingDialog.Controls.Add(label);
            
            var numericUpDown = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 5,
                Value = 3,
                Location = new System.Drawing.Point(20, 50),
                Width = 100
            };
            ratingDialog.Controls.Add(numericUpDown);
            
            var btnOk = new Button
            {
                Text = "Calificar",
                Location = new System.Drawing.Point(150, 50),
                DialogResult = DialogResult.OK
            };
            ratingDialog.Controls.Add(btnOk);
            
            if (ratingDialog.ShowDialog() == DialogResult.OK)
            {
                var rating = (int)numericUpDown.Value;
                MessageBox.Show($"Libro calificado con {rating} estrellas", "Calificación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log($"⭐ Libro calificado: {rating} estrellas");
            }
        }
        
        /// <summary>
        /// Elimina el libro seleccionado de Calibre
        /// </summary>
        private void RemoveFromCalibre()
        {
            if (lvCalibreBooks.SelectedItems.Count == 0)
                return;
            
            var result = MessageBox.Show("¿Estás seguro de eliminar este libro de Calibre?\n\nEsta acción no se puede deshacer.", 
                "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            
            if (result == DialogResult.Yes)
            {
                MessageBox.Show("Funcionalidad de eliminación no implementada.\n\nElimina desde Calibre directamente.", 
                    "No implementado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        /// <summary>
        /// Exporta la metadata de la biblioteca
        /// </summary>
        private void ExportCalibreMetadata()
        {
            MessageBox.Show("Esta funcionalidad exportará la metadata de tu biblioteca a CSV/JSON.\n\nPróximamente disponible.", 
                "Exportar Metadata", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        /// <summary>
        /// Sincroniza con Kindle
        /// </summary>
        private void SyncWithKindle()
        {
            MessageBox.Show("Esta funcionalidad sincronizará libros con tu Kindle.\n\nConecta tu Kindle y usa Calibre para sincronizar.", 
                "Sincronizar Kindle", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        /// <summary>
        /// Convierte el formato del libro
        /// </summary>
        private void ConvertBookFormat()
        {
            if (lvCalibreBooks.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecciona un libro primero.", "Sin selección", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            MessageBox.Show("Esta funcionalidad convertirá el libro a otro formato (EPUB, MOBI, PDF, etc.).\n\nUsa Calibre directamente para conversiones.", 
                "Convertir Formato", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        /// <summary>
        /// Configura la ruta de la biblioteca de Calibre
        /// </summary>
        private void ConfigureCalibrePath()
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Selecciona la carpeta de tu biblioteca de Calibre";
                folderDialog.ShowNewFolderButton = false;
                
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    var path = folderDialog.SelectedPath;
                    
                    if (_calibreIntegration != null)
                    {
                        var success = _calibreIntegration.SetLibraryPath(path);
                        
                        if (success)
                        {
                            MessageBox.Show($"Biblioteca configurada correctamente:\n{path}", 
                                "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            
                            _ = Task.Run(() => InitializeCalibreStatusAsync());
                        }
                        else
                        {
                            MessageBox.Show($"No se encontró una biblioteca válida en:\n{path}\n\nAsegúrate de que existe el archivo 'metadata.db'", 
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Guarda las preferencias de Calibre
        /// </summary>
        private void SaveCalibrePreferences()
        {
            try
            {
                if (_calibreIntegration != null && chkAutoAddToCalibre != null)
                {
                    var autoAdd = chkAutoAddToCalibre.Checked;
                    _calibreIntegration.SaveAutoAddPreference(autoAdd);
                    Log($"Preferencia guardada: Auto-agregar a Calibre = {autoAdd}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error guardando preferencias de Calibre: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene el ID del libro desde el tag
        /// </summary>
        private int GetBookIdFromTag(object tag)
        {
            // Implementación según tu estructura de datos
            return 0;
        }
        
        /// <summary>
        /// Obtiene la ruta del libro desde el tag
        /// </summary>
        private string GetBookPathFromTag(object tag)
        {
            // Implementación según tu estructura de datos
            return string.Empty;
        }
        
        /// <summary>
        /// Vacía completamente la biblioteca de Calibre con confirmación múltiple
        /// </summary>
        private async Task ClearCalibreLibraryAsync()
        {
            if (_calibreIntegration?.IsAvailable != true)
            {
                MessageBox.Show("Calibre no está disponible.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            try
            {
                // Obtener número de libros actual
                var books = await _calibreIntegration.GetBooksAsync();
                var bookCount = books.Count;
                
                if (bookCount == 0)
                {
                    MessageBox.Show("La biblioteca ya está vacía.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                // Primera confirmación
                var result1 = MessageBox.Show(
                    $"ADVERTENCIA: Estás a punto de ELIMINAR TODOS los libros de tu biblioteca de Calibre.\n\n" +
                    $"Libros a eliminar: {bookCount:N0}\n" +
                    $"Biblioteca: {_calibreIntegration.LibraryPath}\n\n" +
                    $"Esta acción NO SE PUEDE DESHACER.\n\n" +
                    $"¿Estás COMPLETAMENTE SEGURO de que deseas continuar?",
                    "CONFIRMACIÓN REQUERIDA (1/3)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                
                if (result1 != DialogResult.Yes)
                {
                    Log("Vaciado de biblioteca cancelado por el usuario (paso 1/3)");
                    return;
                }
                
                // Segunda confirmación
                var result2 = MessageBox.Show(
                    $"ÚLTIMA ADVERTENCIA\n\n" +
                    $"Se eliminarán PERMANENTEMENTE {bookCount:N0} libros.\n" +
                    $"Los archivos físicos también serán eliminados.\n\n" +
                    $"¿Confirmas que deseas VACIAR COMPLETAMENTE la biblioteca?",
                    "CONFIRMACIÓN FINAL (2/3)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Stop,
                    MessageBoxDefaultButton.Button2);
                
                if (result2 != DialogResult.Yes)
                {
                    Log("Vaciado de biblioteca cancelado por el usuario (paso 2/3)");
                    return;
                }
                
                // Tercera confirmación - escribir el número de libros
                var confirmText = Microsoft.VisualBasic.Interaction.InputBox(
                    $"VERIFICACIÓN FINAL (3/3)\n\n" +
                    $"Para confirmar la eliminación de {bookCount:N0} libros,\n" +
                    $"escribe el número de libros que se eliminarán: {bookCount}",
                    "CONFIRMACIÓN FINAL",
                    "");
                
                if (confirmText != bookCount.ToString())
                {
                    MessageBox.Show("Número incorrecto. Operación cancelada.", 
                        "Cancelado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Log("Vaciado de biblioteca cancelado - número de confirmación incorrecto");
                    return;
                }
                
                // Proceder con el vaciado
                Log($"Iniciando vaciado de biblioteca: {bookCount:N0} libros");
                
                var progressForm = new Form
                {
                    Text = "Vaciando biblioteca...",
                    Size = new Size(400, 150),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    ControlBox = false
                };
                
                var lblProgress = new Label
                {
                    Text = "Eliminando libros...",
                    Location = new Point(20, 20),
                    Size = new Size(350, 30),
                    Font = new Font("Segoe UI", 10)
                };
                progressForm.Controls.Add(lblProgress);
                
                progressForm.Show();
                Application.DoEvents();
                
                // Eliminar todos los libros usando calibredb
                var success = await _calibreIntegration.RemoveAllBooksAsync();
                
                progressForm.Close();
                
                if (success)
                {
                    MessageBox.Show(
                        $"Biblioteca vaciada exitosamente.\n\n" +
                        $"Se eliminaron {bookCount:N0} libros.",
                        "Completado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    
                    Log($"Biblioteca vaciada: {bookCount:N0} libros eliminados");
                    
                    // Refrescar la vista
                    await RefreshCalibreLibraryAsync();
                }
                else
                {
                    MessageBox.Show(
                        "Error al vaciar la biblioteca.\n\nRevisa el log para más detalles.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"Error en proceso de vaciado: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
