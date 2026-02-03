using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

namespace SlskDown
{
    public partial class MainForm
    {
        private void LvAutoAuthors_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            // Log de depuración
            System.IO.File.AppendAllText("retrieve_log.txt", $"RetrieveVirtualItem llamado: Index={e.ItemIndex}\n");
            
            try
            {
                // Verificar que las colecciones estén inicializadas
                if (filteredAuthorsData == null || itemCache == null)
                {
                    System.IO.File.AppendAllText("retrieve_log.txt", "Colecciones null, devolviendo item vacío\n");
                    e.Item = new ListViewItem("") { BackColor = Color.FromArgb(30, 30, 30) };
                    return;
                }

                // Usar caché si el ítem está dentro del rango cacheado
                if (itemCache.ContainsKey(e.ItemIndex))
                {
                    e.Item = itemCache[e.ItemIndex];
                    return;
                }

                // Verificar que el índice sea válido
                if (e.ItemIndex < 0 || e.ItemIndex >= filteredAuthorsData.Count)
                {
                    e.Item = new ListViewItem("") { BackColor = Color.FromArgb(30, 30, 30) };
                    return;
                }

                // Crear nuevo ítem
                var author = filteredAuthorsData[e.ItemIndex];
                if (author == null)
                {
                    e.Item = new ListViewItem("") { BackColor = Color.FromArgb(30, 30, 30) };
                    return;
                }

                var item = new ListViewItem(author.Name ?? "")
                {
                    ForeColor = author.ForeColor,
                    BackColor = Color.FromArgb(30, 30, 30)
                };
                
                // Agregar subitems para que coincidan con las columnas
                item.SubItems.Add(author.DownloadedCount.ToString());
                
                e.Item = item;
            }
            catch (Exception ex)
            {
                // En caso de error, devolver un item vacío válido
                e.Item = new ListViewItem($"Error: {ex.Message}") 
                { 
                    BackColor = Color.FromArgb(30, 30, 30),
                    ForeColor = Color.Red
                };
            }
        }

        private void LvAutoAuthors_CacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            // Verificar si necesitamos reconstruir el caché
            if (cacheStart != -1 && e.StartIndex >= cacheStart && e.EndIndex <= cacheEnd)
            {
                return; // El caché ya contiene este rango
            }

            // Limpiar caché antiguo solo si es necesario (optimización para 50K+ items)
            if (itemCache.Count > 3000) // Mantener cache más grande para mejor rendimiento
            {
                itemCache.Clear();
            }
            
            cacheStart = e.StartIndex;
            cacheEnd = e.EndIndex;

            // Cachear los ítems visibles + buffer (aumentado para 50K+ items)
            int bufferSize = 100; // Buffer más grande para scroll más suave
            int start = Math.Max(0, e.StartIndex - bufferSize);
            int end = Math.Min(filteredAuthorsData.Count - 1, e.EndIndex + bufferSize);

            // Pre-cachear en paralelo para mejor rendimiento con grandes volúmenes
            if (end - start > 200)
            {
                var itemsToCache = new System.Collections.Concurrent.ConcurrentDictionary<int, ListViewItem>();
                
                Parallel.For(start, end + 1, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                {
                    if (i >= 0 && i < filteredAuthorsData.Count && !itemCache.ContainsKey(i))
                    {
                        var author = filteredAuthorsData[i];
                        var item = new ListViewItem(author.Name)
                        {
                            ForeColor = author.ForeColor,
                            BackColor = Color.FromArgb(30, 30, 30)
                        };
                        item.SubItems.Add(author.DownloadedCount.ToString());
                        
                        itemsToCache[i] = item;
                    }
                });
                
                // Agregar items cacheados al cache principal
                foreach (var kvp in itemsToCache)
                {
                    itemCache[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                // Cacheo secuencial para rangos pequeños
                for (int i = start; i <= end; i++)
                {
                    if (i >= 0 && i < filteredAuthorsData.Count && !itemCache.ContainsKey(i))
                    {
                        var author = filteredAuthorsData[i];
                        var item = new ListViewItem(author.Name)
                        {
                            ForeColor = author.ForeColor,
                            BackColor = Color.FromArgb(30, 30, 30)
                        };
                        item.SubItems.Add(author.DownloadedCount.ToString());
                        
                        itemCache[i] = item;
                    }
                }
            }
        }

        private void FilterAuthors(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                filteredAuthorsData = new List<AuthorData>(allAuthorsData);
            }
            else
            {
                var search = searchText.ToLower();
                filteredAuthorsData = allAuthorsData
                    .Where(a => a.Name.ToLower().Contains(search))
                    .ToList();
            }

            // Actualizar VirtualListSize y refrescar
            if (lvAutoAuthors != null)
            {
                lvAutoAuthors.VirtualListSize = filteredAuthorsData.Count;
                itemCache.Clear();
                cacheStart = -1;
                cacheEnd = -1;
                
                // Actualizar contador
                UpdateAuthorCount();
                
                lvAutoAuthors.Invalidate();
            }
        }

        private void UpdateAuthorCount()
        {
            var lblCount = lvAutoAuthors?.Parent?.Controls.Find("lblAuthorCount", true).FirstOrDefault() as Label;
            if (lblCount != null)
            {
                int total = allAuthorsData.Count;
                int filtered = filteredAuthorsData.Count;
                int selected = filteredAuthorsData.Count(a => a.IsChecked);
                
                if (total == filtered)
                {
                    lblCount.Text = $"{total:N0} autores | {selected:N0} seleccionados";
                }
                else
                {
                    lblCount.Text = $"{filtered:N0} de {total:N0} autores | {selected:N0} seleccionados";
                }
            }
        }

        private void UpdateAuthorData(string authorName, int filesCount, string status, Color? foreColor = null)
        {
            // Búsqueda O(1) con índice
            if (authorIndex.TryGetValue(authorName, out var author))
            {
                author.FilesCount = filesCount;
                author.Status = status;
                
                // Usar color explícito o determinar automáticamente según el estado
                if (foreColor.HasValue)
                    author.ForeColor = foreColor.Value;
                else
                    author.ForeColor = UIColors.GetAuthorStatusColor(status);

                // OPTIMIZACIÓN: No limpiar cache ni invalidar durante procesos masivos
                // Solo invalidar si no estamos en modo purga/búsqueda automática
                if (!autoPurgeRunning && !autoSearchRunning)
                {
                    // Limpiar caché solo si es muy grande
                    if (itemCache.Count > 3000)
                    {
                        itemCache.Clear();
                        cacheStart = -1;
                        cacheEnd = -1;
                    }

                    if (lvAutoAuthors != null && lvAutoAuthors.InvokeRequired)
                    {
                        lvAutoAuthors.BeginInvoke(new Action(() => lvAutoAuthors.Invalidate()));
                    }
                    else
                    {
                        lvAutoAuthors?.Invalidate();
                    }
                }

                // Refrescar pestaña Vaciar cuando existan cambios
                if (vaciarGridRefresher != null)
                {
                    if (lvVaciarAuthors != null && lvVaciarAuthors.IsHandleCreated)
                    {
                        if (lvVaciarAuthors.InvokeRequired)
                            lvVaciarAuthors.BeginInvoke(new Action(() => vaciarGridRefresher?.Invoke()));
                        else
                            vaciarGridRefresher.Invoke();
                    }
                    else
                    {
                        vaciarGridRefresher.Invoke();
                    }
                }
            }
        }

        private void LoadAuthorsIntoVirtualList(string[] authorNames)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            allAuthorsData.Clear();
            authorIndex.Clear();
            
            // Validar límite de autores (50K+ soportado)
            if (authorNames.Length > 100000)
            {
                AutoLog($"⚠️ ADVERTENCIA: Cargando {authorNames.Length:N0} autores (límite recomendado: 100,000)");
            }
            
            // Procesamiento optimizado para grandes volúmenes
            if (authorNames.Length > 1000)
            {
                // Procesamiento paralelo para >1000 autores
                var tempAuthors = new System.Collections.Concurrent.ConcurrentBag<AuthorData>();
                
                Parallel.ForEach(authorNames, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, name =>
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var authorData = new AuthorData
                        {
                            Name = name.Trim(),
                            FilesCount = 0,
                            Status = "Listo",
                            ForeColor = Color.White,
                            IsChecked = true  // ✅ Marcar como seleccionado por defecto
                        };
                        tempAuthors.Add(authorData);
                    }
                });
                
                // Convertir a lista y construir índice
                allAuthorsData.AddRange(tempAuthors.OrderBy(a => a.Name));
                foreach (var author in allAuthorsData)
                {
                    authorIndex[author.Name] = author; // Índice O(1)
                }
            }
            else
            {
                // Procesamiento secuencial para pocos autores
                foreach (var name in authorNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var authorData = new AuthorData
                        {
                            Name = name.Trim(),
                            FilesCount = 0,
                            Status = "Listo",
                            ForeColor = Color.White,
                            IsChecked = true  // ✅ Marcar como seleccionado por defecto
                        };
                        allAuthorsData.Add(authorData);
                        authorIndex[authorData.Name] = authorData; // Índice O(1)
                    }
                }
            }

            filteredAuthorsData = new List<AuthorData>(allAuthorsData);
            
            if (lvAutoAuthors != null)
            {
                lvAutoAuthors.VirtualListSize = filteredAuthorsData.Count;
                itemCache.Clear();
                cacheStart = -1;
                cacheEnd = -1;
                UpdateAuthorCount();
            }
            
            sw.Stop();
            AutoLog($"📚 Cargados {allAuthorsData.Count:N0} autores en {sw.ElapsedMilliseconds}ms ({(allAuthorsData.Count / (sw.ElapsedMilliseconds + 1.0)):F0} autores/ms)");
        }

        private string[] GetAllAuthorsFromVirtualList()
        {
            return allAuthorsData.Select(a => a.Name).ToArray();
        }

        private string[] GetCheckedAuthorsFromVirtualList()
        {
            return filteredAuthorsData.Where(a => a.IsChecked).Select(a => a.Name).ToArray();
        }

        private void LvAutoAuthors_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Si se hace clic en la misma columna, invertir el orden
            if (e.Column == sortColumn)
            {
                sortAscending = !sortAscending;
            }
            else
            {
                sortColumn = e.Column;
                sortAscending = true;
            }

            // OPTIMIZACIÓN: Ordenar en background thread para no bloquear UI con 50K+ items
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // Mostrar indicador de carga
            if (lvAutoAuthors != null)
            {
                lvAutoAuthors.Enabled = false;
                AutoLog($"🔄 Ordenando {filteredAuthorsData.Count:N0} autores por columna {sortColumn}...");
            }

            Task.Run(() =>
            {
                try
                {
                    // Ordenar en paralelo para mejor rendimiento con grandes volúmenes
                    IOrderedEnumerable<AuthorData> sortedData;
                    
                    switch (sortColumn)
                    {
                        case 0: // Autor (Nombre) - usar StringComparer para mejor rendimiento
                            sortedData = sortAscending
                                ? filteredAuthorsData.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                                : filteredAuthorsData.OrderByDescending(a => a.Name, StringComparer.OrdinalIgnoreCase);
                            break;
                        case 1: // Coincidencias (DownloadedCount) - ordenación numérica
                            sortedData = sortAscending
                                ? filteredAuthorsData.OrderBy(a => a.DownloadedCount)
                                : filteredAuthorsData.OrderByDescending(a => a.DownloadedCount);
                            break;
                        default:
                            return; // Columna inválida
                    }

                    // Convertir a lista (esto es lo costoso, hacerlo en background)
                    var newList = sortedData.ToList();
                    
                    sw.Stop();

                    // Actualizar UI en el thread principal
                    SafeBeginInvoke(() =>
                    {
                        try
                        {
                            filteredAuthorsData = newList;

                            // Actualizar encabezados de columnas con indicadores visuales
                            string[] columnNames = { "Autor", "Coincidencias" };
                            for (int i = 0; i < lvAutoAuthors.Columns.Count; i++)
                            {
                                string header = columnNames[i];
                                if (i == sortColumn)
                                {
                                    header += sortAscending ? " ▲" : " ▼";
                                }
                                lvAutoAuthors.Columns[i].Text = header;
                            }

                            // Limpiar cache solo parcialmente (optimización)
                            if (itemCache.Count > 1000)
                            {
                                itemCache.Clear();
                            }
                            cacheStart = -1;
                            cacheEnd = -1;
                            
                            lvAutoAuthors.Enabled = true;
                            lvAutoAuthors?.Invalidate();
                            
                            AutoLog($"✅ Ordenación completada en {sw.ElapsedMilliseconds}ms ({(filteredAuthorsData.Count / (sw.ElapsedMilliseconds + 1.0)):F0} items/ms)");
                        }
                        catch (Exception ex)
                        {
                            AutoLog($"❌ Error actualizando UI después de ordenar: {ex.Message}");
                            if (lvAutoAuthors != null) lvAutoAuthors.Enabled = true;
                        }
                    });
                }
                catch (Exception ex)
                {
                    SafeBeginInvoke(() =>
                    {
                        AutoLog($"❌ Error ordenando autores: {ex.Message}");
                        if (lvAutoAuthors != null) lvAutoAuthors.Enabled = true;
                    });
                }
            });
        }
    }
}
