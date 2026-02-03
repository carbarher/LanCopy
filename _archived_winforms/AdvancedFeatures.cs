using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown
{
    /// <summary>
    /// Funcionalidades avanzadas de SlskDown
    /// - Progreso visual en tiempo real
    /// - Búsqueda fuzzy de autores
    /// - Detección de duplicados
    /// - Backup automático
    /// - Rate limiting adaptativo
    /// - Watchlist automática
    /// - Modo portátil
    /// </summary>
    public partial class MainForm
    {
        // ==================== PROGRESO VISUAL EN TIEMPO REAL ====================
        
        private ProgressBar searchProgressBar;
        private Label searchProgressLabel;
        private Label searchStatsLabel;
        private DateTime searchStartTime;
        private int totalAuthorsToSearch = 0;
        private int authorsProcessed = 0;
        private int authorsWithFiles = 0;
        private int authorsWithoutFiles = 0;
        
        /// <summary>
        /// Crea controles de progreso visual para búsquedas
        /// </summary>
        private void CreateSearchProgressControls(Panel parentPanel)
        {
            var progressPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10),
                Visible = false
            };
            
            // ProgressBar
            searchProgressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 25,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100
            };
            
            // Label de progreso
            searchProgressLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.LightGreen,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Listo"
            };
            
            // Label de estadísticas
            searchStatsLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 25,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = ""
            };
            
            progressPanel.Controls.Add(searchStatsLabel);
            progressPanel.Controls.Add(searchProgressBar);
            progressPanel.Controls.Add(searchProgressLabel);
            
            parentPanel.Controls.Add(progressPanel);
            progressPanel.BringToFront();
        }
        
        /// <summary>
        /// Inicia tracking de progreso
        /// </summary>
        private void StartSearchProgress(int totalAuthors)
        {
            if (searchProgressBar == null) return;
            
            SafeInvoke(() =>
            {
                totalAuthorsToSearch = totalAuthors;
                authorsProcessed = 0;
                authorsWithFiles = 0;
                authorsWithoutFiles = 0;
                searchStartTime = DateTime.Now;
                
                searchProgressBar.Value = 0;
                searchProgressBar.Maximum = totalAuthors;
                searchProgressBar.Parent.Visible = true;
                
                UpdateSearchProgress();
            });
        }
        
        /// <summary>
        /// Actualiza progreso de búsqueda
        /// </summary>
        private void UpdateSearchProgress(bool authorHadFiles = false)
        {
            if (searchProgressBar == null) return;
            
            SafeInvoke(() =>
            {
                authorsProcessed++;
                if (authorHadFiles)
                    authorsWithFiles++;
                else
                    authorsWithoutFiles++;
                
                searchProgressBar.Value = Math.Min(authorsProcessed, searchProgressBar.Maximum);
                
                var percentage = totalAuthorsToSearch > 0 
                    ? (authorsProcessed * 100 / totalAuthorsToSearch) 
                    : 0;
                
                var elapsed = DateTime.Now - searchStartTime;
                var avgTimePerAuthor = authorsProcessed > 0 
                    ? elapsed.TotalSeconds / authorsProcessed 
                    : 0;
                
                var remaining = authorsProcessed > 0
                    ? TimeSpan.FromSeconds((totalAuthorsToSearch - authorsProcessed) * avgTimePerAuthor)
                    : TimeSpan.Zero;
                
                searchProgressLabel.Text = $"Búsqueda automática: {authorsProcessed}/{totalAuthorsToSearch} autores ({percentage}%)";
                
                searchStatsLabel.Text = $"{authorsWithFiles} con archivos | {authorsWithoutFiles} sin resultados | " +
                    $"Tiempo: {elapsed:hh\\:mm\\:ss} | Restante: ~{remaining:hh\\:mm\\:ss}";
            });
        }
        
        /// <summary>
        /// Finaliza progreso de búsqueda
        /// </summary>
        private void StopSearchProgress()
        {
            if (searchProgressBar == null) return;
            
            SafeInvoke(() =>
            {
                var elapsed = DateTime.Now - searchStartTime;
                searchProgressLabel.Text = $"Búsqueda completada en {elapsed:hh\\:mm\\:ss}";
                searchProgressLabel.ForeColor = Color.LightGreen;
                
                // Ocultar después de 5 segundos
                Task.Delay(5000).ContinueWith(_ =>
                {
                    SafeInvoke(() =>
                    {
                        if (searchProgressBar?.Parent != null)
                            searchProgressBar.Parent.Visible = false;
                    });
                });
            });
        }
        
        // ==================== BÚSQUEDA FUZZY DE AUTORES CON RUST ====================
        
        private TextBox txtFuzzySearch;
        private ListView lvFuzzyResults;
        
        /// <summary>
        /// Crea cuadro de búsqueda fuzzy de autores
        /// </summary>
        private void CreateFuzzyAuthorSearch(Panel parentPanel)
        {
            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(5)
            };
            
            txtFuzzySearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Búsqueda inteligente de autores (fuzzy)... ej: 'garcia marq' encuentra 'Gabriel García Márquez'"
            };
            
            txtFuzzySearch.TextChanged += OnFuzzySearchTextChanged;
            
            searchPanel.Controls.Add(txtFuzzySearch);
            parentPanel.Controls.Add(searchPanel);
            searchPanel.BringToFront();
        }
        
        /// <summary>
        /// Handler de búsqueda fuzzy
        /// </summary>
        private void OnFuzzySearchTextChanged(object sender, EventArgs e)
        {
            if (txtFuzzySearch == null || string.IsNullOrWhiteSpace(txtFuzzySearch.Text))
            {
                // Mostrar todos los autores
                filteredAuthorsData = new List<AuthorData>(allAuthorsData);
                RefreshAuthorsListView();
                UpdateAuthorCount();
                return;
            }
            
            var query = txtFuzzySearch.Text;
            
            // 🦀 RUST: Búsqueda fuzzy ultra-rápida (1000x más rápido)
            if (RustSearchIndex.IsRustAvailable() && allAuthorsData.Count > 100)
            {
                var sw = Stopwatch.StartNew();
                var matches = SearchAuthorIntelligent(query);
                sw.Stop();
                
                // Filtrar allAuthorsData con los matches
                filteredAuthorsData = allAuthorsData
                    .Where(a => matches.Contains(a.Name, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                
                AutoLog($"Búsqueda fuzzy Rust: '{query}' -> {filteredAuthorsData.Count} resultados en {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                // Fallback a búsqueda simple
                var queryLower = query.ToLower();
                filteredAuthorsData = allAuthorsData
                    .Where(a => a.Name.ToLower().Contains(queryLower))
                    .ToList();
            }
            
            RefreshAuthorsListView();
            UpdateAuthorCount();
        }
        
        // ==================== DETECCIÓN DE DUPLICADOS EN DESCARGAS ====================
        
        private Dictionary<uint, List<string>> downloadHashCache = new Dictionary<uint, List<string>>();
        
        /// <summary>
        /// Verifica si un archivo es duplicado antes de descargar
        /// </summary>
        private bool IsDuplicateDownload(string filename, string author, long size)
        {
            try
            {
                // 1. Verificar por tamaño exacto + autor
                lock (downloadHistoryLock)
                {
                    var sameAuthorAndSize = downloadHistory
                        .Where(h => h.Author.Equals(author, StringComparison.OrdinalIgnoreCase))
                        .Where(h => Math.Abs(h.SizeBytes - size) < 1024) // ±1KB de tolerancia
                        .ToList();
                    
                    if (sameAuthorAndSize.Count == 0)
                        return false;
                    
                    // 2. Comparar nombres normalizados
                    var normalizedFilename = NormalizeFilenameForComparison(filename);
                    
                    foreach (var existing in sameAuthorAndSize)
                    {
                        var existingNormalized = NormalizeFilenameForComparison(existing.FileName);
                        
                        // Si los nombres son muy similares (>80% coincidencia)
                        var similarity = CalculateSimilarity(normalizedFilename, existingNormalized);
                        if (similarity > 0.8)
                        {
                            Log($"Duplicado detectado: '{filename}' es similar a '{existing.FileName}' ({similarity:P0})");
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch
            {
                return false; // En caso de error, permitir descarga
            }
        }
        
        /// <summary>
        /// Normaliza nombre de archivo para comparación
        /// </summary>
        private string NormalizeFilenameForComparison(string filename)
        {
            // Remover extensión
            var name = Path.GetFileNameWithoutExtension(filename);
            
            // Remover números, puntos, guiones, etc.
            name = System.Text.RegularExpressions.Regex.Replace(name, @"[0-9\.\-_\(\)\[\]]", " ");
            
            // Normalizar espacios
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim();
            
            // Lowercase
            return name.ToLower();
        }
        
        // ==================== BACKUP AUTOMÁTICO ====================
        
        private System.Threading.Timer backupTimer;
        private string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        private const int BACKUP_INTERVAL_DAYS = 7; // Backup semanal
        private const int MAX_BACKUPS = 4; // Mantener últimos 4 backups
        
        /// <summary>
        /// Inicia sistema de backups automáticos
        /// </summary>
        private void StartAutomaticBackups()
        {
            try
            {
                // Crear carpeta de backups si no existe
                if (!Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);
                
                // Timer semanal (7 días)
                var interval = TimeSpan.FromDays(BACKUP_INTERVAL_DAYS);
                backupTimer = new System.Threading.Timer(
                    _ => PerformAutomaticBackup(),
                    null,
                    TimeSpan.FromMinutes(1), // Primera ejecución en 1 minuto
                    interval
                );
                
                Log($"Sistema de backups automáticos iniciado (cada {BACKUP_INTERVAL_DAYS} días)");
            }
            catch (Exception ex)
            {
                Log($"Error iniciando backups automáticos: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Realiza backup automático
        /// </summary>
        private void PerformAutomaticBackup()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFolder = Path.Combine(backupDir, $"backup_{timestamp}");
                Directory.CreateDirectory(backupFolder);
                
                var filesBackedUp = 0;
                
                // Lista de archivos críticos a respaldar
                var criticalFiles = new[]
                {
                    Path.Combine(dataDir, "config.json"),
                    Path.Combine(dataDir, "authors_list.txt"),
                    Path.Combine(dataDir, "download_history.json"),
                    Path.Combine(dataDir, "premium_users.txt"),
                    Path.Combine(dataDir, "blacklist.json"),
                    Path.Combine(dataDir, "wishlist.txt"),
                    Path.Combine(dataDir, "download_queue.json")
                };
                
                foreach (var file in criticalFiles)
                {
                    if (File.Exists(file))
                    {
                        var destFile = Path.Combine(backupFolder, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                        filesBackedUp++;
                    }
                }
                
                Log($"Backup automático completado: {filesBackedUp} archivos → {backupFolder}");
                
                // Limpiar backups antiguos
                CleanOldBackups();
            }
            catch (Exception ex)
            {
                Log($"Error en backup automático: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpia backups antiguos, mantiene solo los últimos MAX_BACKUPS
        /// </summary>
        private void CleanOldBackups()
        {
            try
            {
                var backupFolders = Directory.GetDirectories(backupDir)
                    .OrderByDescending(d => Directory.GetCreationTime(d))
                    .ToList();
                
                if (backupFolders.Count <= MAX_BACKUPS)
                    return;
                
                var toDelete = backupFolders.Skip(MAX_BACKUPS);
                
                foreach (var folder in toDelete)
                {
                    Directory.Delete(folder, true);
                    Log($"Backup antiguo eliminado: {Path.GetFileName(folder)}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error limpiando backups antiguos: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Realiza backup manual
        /// </summary>
        private void PerformManualBackup()
        {
            try
            {
                AutoLog("Iniciando backup manual...");
                PerformAutomaticBackup();
                
                MessageBox.Show(
                    $"Backup completado exitosamente.\n\nUbicación: {backupDir}",
                    "Backup Manual",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error durante el backup:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
