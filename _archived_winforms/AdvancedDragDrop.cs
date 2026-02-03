using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    public class AdvancedDragDrop
    {
        private Action<string> logAction;
        private Func<string, Task> loadAuthorListAction;
        private Func<string, Task> addToCalibreAction;
        
        public AdvancedDragDrop(
            Action<string> logger,
            Func<string, Task> loadAuthors,
            Func<string, Task> addToCalibre)
        {
            logAction = logger;
            loadAuthorListAction = loadAuthors;
            addToCalibreAction = addToCalibre;
        }
        
        public void EnableDragDrop(Form form)
        {
            form.AllowDrop = true;
            form.DragEnter += Form_DragEnter;
            form.DragDrop += Form_DragDrop;
            
            logAction?.Invoke("✅ Drag & Drop habilitado");
        }
        
        private void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }
        
        private async void Form_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                
                logAction?.Invoke($"📂 {files.Length} archivo(s) arrastrado(s)");
                
                foreach (var file in files)
                {
                    await ProcessDroppedFile(file);
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error procesando archivos: {ex.Message}");
            }
        }
        
        private async Task ProcessDroppedFile(string file)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            
            // Lista de autores (.txt)
            if (ext == ".txt")
            {
                logAction?.Invoke($"📝 Cargando lista de autores: {Path.GetFileName(file)}");
                await loadAuthorListAction(file);
            }
            // Ebooks
            else if (IsEbookFile(ext))
            {
                logAction?.Invoke($"📚 Agregando a Calibre: {Path.GetFileName(file)}");
                await addToCalibreAction(file);
            }
            // Carpeta
            else if (Directory.Exists(file))
            {
                logAction?.Invoke($"📁 Procesando carpeta: {Path.GetFileName(file)}");
                await ProcessFolder(file);
            }
            else
            {
                logAction?.Invoke($"⚠️ Tipo de archivo no soportado: {ext}");
            }
        }
        
        private async Task ProcessFolder(string folder)
        {
            try
            {
                var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                var ebookFiles = files.Where(f => IsEbookFile(Path.GetExtension(f))).ToList();
                
                logAction?.Invoke($"📚 Encontrados {ebookFiles.Count} ebooks en carpeta");
                
                foreach (var file in ebookFiles)
                {
                    await addToCalibreAction(file);
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error procesando carpeta: {ex.Message}");
            }
        }
        
        private bool IsEbookFile(string ext)
        {
            var ebookExtensions = new[] { ".epub", ".mobi", ".pdf", ".azw3", ".fb2", ".cbr", ".cbz" };
            return ebookExtensions.Contains(ext.ToLowerInvariant());
        }
    }
}
