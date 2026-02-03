using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    public partial class MainForm
    {
        /// <summary>
        /// Detecta y muestra archivos duplicados usando hashes SHA256
        /// </summary>
        private async Task DetectDuplicatesAsync()
        {
            if (fileHashCache == null)
            {
                MessageBox.Show("El caché de hashes no está inicializado.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(downloadDir) || !Directory.Exists(downloadDir))
            {
                MessageBox.Show($"La carpeta de descargas no existe:\n{downloadDir}", "Carpeta no encontrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            UseWaitCursor = true;
            AutoLog("🔍 Escaneando archivos para detectar duplicados...");

            try
            {
                // Escanear todos los archivos y calcular hashes
                var files = Directory.GetFiles(downloadDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => IsDocumentFile(Path.GetFileName(f)))
                    .Where(f => !f.Contains("_NoEspanol") && !f.Contains("_Corruptos"))
                    .ToList();

                if (files.Count == 0)
                {
                    AutoLog("⚠️ No hay archivos para escanear.");
                    MessageBox.Show("No hay archivos en la biblioteca para escanear.", "Sin archivos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                AutoLog($"📊 Escaneando {files.Count} archivos...");

                // Calcular hashes en paralelo para archivos no cacheados
                var progress = 0;
                var lockObj = new object();

                await Task.Run(() =>
                {
                    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
                    {
                        try
                        {
                            // Si no está en caché, calcular hash y guardar
                            if (!fileHashCache.TryGetValidationResult(file, out _))
                            {
                                fileHashCache.SetValidationResult(file, true);
                            }

                            lock (lockObj)
                            {
                                progress++;
                                if (progress % 100 == 0)
                                {
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        AutoLog($"📊 Progreso: {progress}/{files.Count} archivos escaneados...");
                                    });
                                }
                            }
                        }
                        catch
                        {
                            // Error procesando archivo, continuar
                        }
                    });
                });

                // Buscar duplicados
                var duplicateGroups = fileHashCache.FindDuplicatesByHash();

                if (duplicateGroups.Count == 0)
                {
                    AutoLog("✅ No se encontraron archivos duplicados.");
                    MessageBox.Show("No se encontraron archivos duplicados en la biblioteca.", "Sin duplicados", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var totalDuplicates = duplicateGroups.Sum(g => g.Value.Count - 1);
                var wastedSpace = duplicateGroups.Sum(g =>
                {
                    var firstFile = g.Value.First();
                    if (File.Exists(firstFile))
                    {
                        var size = new FileInfo(firstFile).Length;
                        return size * (g.Value.Count - 1);
                    }
                    return 0L;
                });

                AutoLog($"🔍 Encontrados {duplicateGroups.Count} grupos de duplicados ({totalDuplicates} archivos duplicados)");
                AutoLog($"💾 Espacio desperdiciado: {FormatFileSize(wastedSpace)}");

                // NUEVO: Preguntar si desea eliminar automáticamente
                var autoDeleteConfirm = MessageBox.Show(
                    $"Se han detectado {duplicateGroups.Count} grupos de duplicados ({totalDuplicates} archivos).\n" +
                    $"Espacio desperdiciado: {FormatFileSize(wastedSpace)}\n\n" +
                    "¿Deseas eliminar AUTOMÁTICAMENTE los duplicados, dejando solo una copia original de cada grupo?\n\n" +
                    "Presiona 'Sí' para eliminar automáticamente.\n" +
                    "Presiona 'No' para revisar y seleccionar manualmente.",
                    "Detección Automática de Duplicados",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);

                if (autoDeleteConfirm == DialogResult.Yes)
                {
                    await PerformAutoDeleteAsync(duplicateGroups, totalDuplicates, wastedSpace);
                }
                else if (autoDeleteConfirm == DialogResult.No)
                {
                    // Mostrar diálogo manual original
                    ShowDuplicatesDialog(duplicateGroups, wastedSpace);
                }
            }
            catch (Exception ex)
            {
                AutoLog($"❌ Error detectando duplicados: {ex.Message}");
                MessageBox.Show($"Error detectando duplicados:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private async Task PerformAutoDeleteAsync(Dictionary<string, List<string>> duplicateGroups, int totalDuplicates, long wastedSpace)
        {
            UseWaitCursor = true;
            var deleted = 0;
            var failed = 0;
            var filesToDelete = new List<string>();

            foreach (var group in duplicateGroups.Values)
            {
                // Ordenar por longitud de ruta (preferir rutas más cortas como "originales")
                // y luego por fecha de creación (opcional)
                var sortedGroup = group.OrderBy(f => f.Length).ToList();
                
                // El primero se queda, el resto se borra
                for (int i = 1; i < sortedGroup.Count; i++)
                {
                    filesToDelete.Add(sortedGroup[i]);
                }
            }

            AutoLog($"🗑️ Eliminando automáticamente {filesToDelete.Count} archivos duplicados...");

            await Task.Run(() =>
            {
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                            fileHashCache?.Invalidate(file);
                            deleted++;
                        }
                    }
                    catch
                    {
                        failed++;
                    }
                }
            });

            AutoLog($"✅ Eliminación automática completada. Borrados: {deleted}, Fallidos: {failed}");
            MessageBox.Show(
                $"Eliminación automática completada:\n\n" +
                $"✅ Archivos eliminados: {deleted}\n" +
                $"❌ Fallaron: {failed}\n" +
                $"💾 Espacio liberado: {FormatFileSize(wastedSpace)}",
                "Duplicados Eliminados",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowDuplicatesDialog(Dictionary<string, List<string>> duplicateGroups, long wastedSpace)
        {
            var dialog = new Form
            {
                Text = "Archivos Duplicados Detectados",
                Size = new Size(1000, 700),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };

            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var lblInfo = new Label
            {
                Text = $"Se encontraron {duplicateGroups.Count} grupos de archivos duplicados.\n" +
                       $"Total de archivos duplicados: {duplicateGroups.Sum(g => g.Value.Count - 1)}\n" +
                       $"Espacio desperdiciado: {FormatFileSize(wastedSpace)}",
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(lblInfo);

            var listView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };

            listView.Columns.Add("Archivo", 400);
            listView.Columns.Add("Tamaño", 100);
            listView.Columns.Add("Ruta", 400);

            foreach (var group in duplicateGroups.OrderByDescending(g => g.Value.Count))
            {
                var groupItem = new ListViewItem($"[GRUPO] {group.Value.Count} copias")
                {
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    BackColor = Color.FromArgb(50, 50, 80)
                };
                groupItem.SubItems.Add("");
                groupItem.SubItems.Add("");
                listView.Items.Add(groupItem);

                // Agregar archivos del grupo (marcar todos excepto el primero)
                bool isFirst = true;
                foreach (var file in group.Value.OrderBy(f => f))
                {
                    if (File.Exists(file))
                    {
                        var fileInfo = new FileInfo(file);
                        var item = new ListViewItem($"  {Path.GetFileName(file)}")
                        {
                            Tag = file,
                            Checked = !isFirst // Marcar duplicados para eliminar
                        };
                        item.SubItems.Add(FormatFileSize(fileInfo.Length));
                        item.SubItems.Add(Path.GetDirectoryName(file) ?? "");
                        listView.Items.Add(item);
                        isFirst = false;
                    }
                }
            }

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 50,
                Padding = new Padding(5)
            };

            var btnClose = new Button
            {
                Text = "Cerrar",
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => dialog.Close();

            var btnDelete = new Button
            {
                Text = "Eliminar seleccionados",
                Size = new Size(160, 35),
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += async (s, e) =>
            {
                var checkedFiles = listView.Items.Cast<ListViewItem>()
                    .Where(item => item.Checked && item.Tag != null)
                    .Select(item => (string)item.Tag)
                    .ToList();

                if (checkedFiles.Count == 0)
                {
                    MessageBox.Show("No hay archivos seleccionados para eliminar.", "Sin selección", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var totalSize = checkedFiles.Sum(f => File.Exists(f) ? new FileInfo(f).Length : 0);

                var confirm = MessageBox.Show(
                    $"¿Estás seguro de que quieres eliminar {checkedFiles.Count} archivo(s) duplicados?\n\n" +
                    $"Espacio a liberar: {FormatFileSize(totalSize)}\n\n" +
                    "Esta acción NO se puede deshacer.",
                    "Confirmar eliminación",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (confirm != DialogResult.Yes)
                    return;

                dialog.Enabled = false;
                UseWaitCursor = true;

                var deleted = 0;
                var failed = 0;

                await Task.Run(() =>
                {
                    foreach (var file in checkedFiles)
                    {
                        try
                        {
                            if (File.Exists(file))
                            {
                                File.Delete(file);
                                fileHashCache?.Invalidate(file);
                                deleted++;
                            }
                        }
                        catch
                        {
                            failed++;
                        }
                    }
                });

                UseWaitCursor = false;
                dialog.Enabled = true;

                AutoLog($"🗑️ Duplicados eliminados: {deleted} archivos, {failed} fallaron");
                MessageBox.Show(
                    $"Eliminación completada:\n\n" +
                    $"✅ Eliminados: {deleted}\n" +
                    $"❌ Fallaron: {failed}\n" +
                    $"💾 Espacio liberado: {FormatFileSize(totalSize)}",
                    "Eliminación completada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                dialog.Close();
            };

            var btnSelectAll = new Button
            {
                Text = "Seleccionar todos",
                Size = new Size(140, 35),
                BackColor = Color.FromArgb(60, 100, 140),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSelectAll.FlatAppearance.BorderSize = 0;
            btnSelectAll.Click += (s, e) =>
            {
                foreach (ListViewItem item in listView.Items)
                {
                    if (item.Tag != null)
                        item.Checked = true;
                }
            };

            var btnDeselectAll = new Button
            {
                Text = "Deseleccionar todos",
                Size = new Size(150, 35),
                BackColor = Color.FromArgb(60, 100, 140),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnDeselectAll.FlatAppearance.BorderSize = 0;
            btnDeselectAll.Click += (s, e) =>
            {
                foreach (ListViewItem item in listView.Items)
                {
                    if (item.Tag != null)
                        item.Checked = false;
                }
            };

            buttonPanel.Controls.Add(btnClose);
            buttonPanel.Controls.Add(btnDelete);
            buttonPanel.Controls.Add(btnDeselectAll);
            buttonPanel.Controls.Add(btnSelectAll);

            mainPanel.Controls.Add(listView);
            dialog.Controls.Add(mainPanel);
            dialog.Controls.Add(buttonPanel);

            dialog.ShowDialog(this);
        }
    }
}
