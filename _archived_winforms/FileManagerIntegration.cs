using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SlskDown
{
    public class FileManagerIntegration
    {
        private string customFileManager = "";
        private string customFileManagerArgs = "{path}";
        private Action<string> logAction;
        
        public FileManagerIntegration(Action<string> logger)
        {
            logAction = logger;
        }
        
        public void SetCustomFileManager(string path, string args)
        {
            customFileManager = path;
            customFileManagerArgs = args;
            
            if (!string.IsNullOrEmpty(path))
            {
                logAction?.Invoke($"📁 File manager personalizado: {Path.GetFileName(path)}");
            }
        }
        
        public void OpenInFileManager(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(customFileManager) && File.Exists(customFileManager))
                {
                    string args = customFileManagerArgs.Replace("{path}", $"\"{filePath}\"");
                    Process.Start(customFileManager, args);
                    logAction?.Invoke($"📁 Abriendo en {Path.GetFileName(customFileManager)}: {Path.GetFileName(filePath)}");
                }
                else
                {
                    if (File.Exists(filePath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                        logAction?.Invoke($"📁 Mostrando archivo: {Path.GetFileName(filePath)}");
                    }
                    else if (Directory.Exists(filePath))
                    {
                        Process.Start("explorer.exe", $"\"{filePath}\"");
                        logAction?.Invoke($"📁 Abriendo carpeta: {Path.GetFileName(filePath)}");
                    }
                    else
                    {
                        string directory = Path.GetDirectoryName(filePath);
                        if (Directory.Exists(directory))
                        {
                            Process.Start("explorer.exe", $"\"{directory}\"");
                            logAction?.Invoke($"📁 Abriendo carpeta padre: {Path.GetFileName(directory)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error abriendo file manager: {ex.Message}");
                MessageBox.Show($"Error abriendo file manager: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        public void CopyPathToClipboard(string path)
        {
            try
            {
                Clipboard.SetText(path);
                logAction?.Invoke($"📋 Ruta copiada: {path}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error copiando ruta: {ex.Message}");
            }
        }
        
        public void CopyFileNameToClipboard(string path)
        {
            try
            {
                string fileName = Path.GetFileName(path);
                Clipboard.SetText(fileName);
                logAction?.Invoke($"📋 Nombre copiado: {fileName}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error copiando nombre: {ex.Message}");
            }
        }
        
        public void EnableDragDrop(ListView listView, Func<ListViewItem, string> getFilePath)
        {
            listView.AllowDrop = true;
            
            listView.ItemDrag += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                {
                    var files = new List<string>();
                    
                    foreach (ListViewItem item in listView.SelectedItems)
                    {
                        string filePath = getFilePath(item);
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            files.Add(filePath);
                        }
                    }
                    
                    if (files.Count > 0)
                    {
                        var data = new DataObject(DataFormats.FileDrop, files.ToArray());
                        listView.DoDragDrop(data, DragDropEffects.Copy);
                        logAction?.Invoke($"📦 Arrastrando {files.Count} archivo(s)");
                    }
                }
            };
        }
        
        public void AddFileManagerContextMenu(ListView listView, Func<ListViewItem, string> getFilePath)
        {
            var contextMenu = new ContextMenuStrip();
            
            var openItem = new ToolStripMenuItem("📁 Abrir en File Manager");
            openItem.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                {
                    string filePath = getFilePath(listView.SelectedItems[0]);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        OpenInFileManager(filePath);
                    }
                }
            };
            contextMenu.Items.Add(openItem);
            
            var openFolderItem = new ToolStripMenuItem("📂 Abrir Carpeta");
            openFolderItem.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                {
                    string filePath = getFilePath(listView.SelectedItems[0]);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        string directory = Path.GetDirectoryName(filePath);
                        if (Directory.Exists(directory))
                        {
                            OpenInFileManager(directory);
                        }
                    }
                }
            };
            contextMenu.Items.Add(openFolderItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            var copyPathItem = new ToolStripMenuItem("📋 Copiar Ruta Completa");
            copyPathItem.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                {
                    string filePath = getFilePath(listView.SelectedItems[0]);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        CopyPathToClipboard(filePath);
                    }
                }
            };
            contextMenu.Items.Add(copyPathItem);
            
            var copyNameItem = new ToolStripMenuItem("📋 Copiar Nombre");
            copyNameItem.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                {
                    string filePath = getFilePath(listView.SelectedItems[0]);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        CopyFileNameToClipboard(filePath);
                    }
                }
            };
            contextMenu.Items.Add(copyNameItem);
            
            listView.ContextMenuStrip = contextMenu;
        }
    }
}
