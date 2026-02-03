using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SlskDown.UI;

namespace SlskDown.UI
{
    /// <summary>
    /// Helper para facilitar el uso de Virtual ListView en MainForm
    /// </summary>
    public static class VirtualListViewHelper
    {
        /// <summary>
        /// Convierte un ListView normal a Virtual ListView
        /// </summary>
        public static VirtualListView ConvertToVirtual(ListView existingListView)
        {
            var virtualListView = new VirtualListView
            {
                Name = existingListView.Name,
                Location = existingListView.Location,
                Size = existingListView.Size,
                Dock = existingListView.Dock,
                Anchor = existingListView.Anchor,
                Font = existingListView.Font,
                BackColor = existingListView.BackColor,
                ForeColor = existingListView.ForeColor,
                View = existingListView.View,
                FullRowSelect = existingListView.FullRowSelect,
                GridLines = existingListView.GridLines,
                MultiSelect = existingListView.MultiSelect
            };
            
            // Copiar columnas
            foreach (ColumnHeader column in existingListView.Columns)
            {
                virtualListView.Columns.Add(new ColumnHeader
                {
                    Text = column.Text,
                    Width = column.Width,
                    TextAlign = column.TextAlign
                });
            }
            
            // Copiar event handlers comunes
            if (existingListView.Parent != null)
            {
                var parent = existingListView.Parent;
                var index = parent.Controls.IndexOf(existingListView);
                parent.Controls.Remove(existingListView);
                parent.Controls.Add(virtualListView);
                parent.Controls.SetChildIndex(virtualListView, index);
            }
            
            return virtualListView;
        }
        
        /// <summary>
        /// Crea columnas estándar para resultados de búsqueda
        /// </summary>
        public static void SetupSearchResultColumns(ListView listView)
        {
            listView.Columns.Clear();
            listView.Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "Archivo", Width = 400 },
                new ColumnHeader { Text = "Tamaño", Width = 100, TextAlign = HorizontalAlignment.Right },
                new ColumnHeader { Text = "Usuario", Width = 150 },
                new ColumnHeader { Text = "Ext", Width = 60 },
                new ColumnHeader { Text = "Bitrate", Width = 80, TextAlign = HorizontalAlignment.Right },
                new ColumnHeader { Text = "Duración", Width = 80, TextAlign = HorizontalAlignment.Right }
            });
        }
        
        /// <summary>
        /// Crea columnas estándar para descargas
        /// </summary>
        public static void SetupDownloadColumns(ListView listView)
        {
            listView.Columns.Clear();
            listView.Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "Archivo", Width = 300 },
                new ColumnHeader { Text = "Progreso", Width = 100 },
                new ColumnHeader { Text = "Velocidad", Width = 100, TextAlign = HorizontalAlignment.Right },
                new ColumnHeader { Text = "Estado", Width = 120 },
                new ColumnHeader { Text = "Usuario", Width = 150 }
            });
        }
        
        /// <summary>
        /// Benchmark para comparar performance
        /// </summary>
        public static BenchmarkResult BenchmarkListView(int itemCount)
        {
            var result = new BenchmarkResult { ItemCount = itemCount };
            
            // Test 1: ListView Normal
            using (var normalListView = new ListView { View = View.Details })
            {
                SetupSearchResultColumns(normalListView);
                
                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                for (int i = 0; i < itemCount; i++)
                {
                    var item = new ListViewItem($"File_{i}.mp3");
                    item.SubItems.Add("5.2 MB");
                    item.SubItems.Add($"User{i % 100}");
                    normalListView.Items.Add(item);
                }
                
                sw.Stop();
                result.NormalListViewMs = sw.ElapsedMilliseconds;
                result.NormalListViewMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Test 2: Virtual ListView
            using (var virtualListView = new VirtualListView())
            {
                SetupSearchResultColumns(virtualListView);
                
                var items = Enumerable.Range(0, itemCount)
                    .Select(i => new SearchResultItem
                    {
                        Filename = $"File_{i}.mp3",
                        Size = 5_242_880,
                        Username = $"User{i % 100}",
                        Extension = "mp3"
                    })
                    .ToList();
                
                var dataSource = new SearchResultsDataSource();
                
                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                dataSource.SetItems(items);
                virtualListView.SetDataSource(dataSource);
                
                sw.Stop();
                result.VirtualListViewMs = sw.ElapsedMilliseconds;
                result.VirtualListViewMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            }
            
            result.SpeedupFactor = result.NormalListViewMs / (double)result.VirtualListViewMs;
            result.MemorySavingsMB = result.NormalListViewMemoryMB - result.VirtualListViewMemoryMB;
            
            return result;
        }
    }
    
    /// <summary>
    /// Resultado de benchmark
    /// </summary>
    public class BenchmarkResult
    {
        public int ItemCount { get; set; }
        public long NormalListViewMs { get; set; }
        public long VirtualListViewMs { get; set; }
        public double NormalListViewMemoryMB { get; set; }
        public double VirtualListViewMemoryMB { get; set; }
        public double SpeedupFactor { get; set; }
        public double MemorySavingsMB { get; set; }
        
        public override string ToString()
        {
            return $@"
Benchmark Results ({ItemCount:N0} items):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Normal ListView:
  Time:   {NormalListViewMs:N0} ms
  Memory: {NormalListViewMemoryMB:F2} MB

Virtual ListView:
  Time:   {VirtualListViewMs:N0} ms
  Memory: {VirtualListViewMemoryMB:F2} MB

Improvement:
  Speed:  {SpeedupFactor:F1}x faster
  Memory: {MemorySavingsMB:F2} MB saved ({(MemorySavingsMB / NormalListViewMemoryMB * 100):F1}% reduction)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
        }
    }
}
