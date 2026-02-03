using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Soulseek;
using SlskDown.Models;
using SlskDown.Services;

namespace SlskDown.UI
{
    /// <summary>
    /// Gestor de la pestaña de Descargas - Extraído de MainForm.cs
    /// </summary>
    public class DownloadsTabManager
    {
        private readonly MainForm _mainForm;
        private readonly ListView _lvDownloads;
        private readonly ProgressBar _progressBar;
        private readonly Label _lblStats;
        private readonly Button _btnPause;
        private readonly Button _btnResume;
        private readonly Button _btnCancel;
        private readonly Button _btnRetry;
        
        // Estado
        private readonly List<DownloadTask> _downloadQueue = new();
        private readonly object _queueLock = new();
        private bool _isPaused = false;
        private CancellationTokenSource _downloadCts;
        
        public DownloadsTabManager(MainForm mainForm, ListView lvDownloads, 
            ProgressBar progressBar, Label lblStats,
            Button btnPause, Button btnResume, Button btnCancel, Button btnRetry)
        {
            _mainForm = mainForm;
            _lvDownloads = lvDownloads;
            _progressBar = progressBar;
            _lblStats = lblStats;
            _btnPause = btnPause;
            _btnResume = btnResume;
            _btnCancel = btnCancel;
            _btnRetry = btnRetry;
            
            InitializeEvents();
            InitializeListView();
        }
        
        private void InitializeEvents()
        {
            _btnPause.Click += OnPauseClicked;
            _btnResume.Click += OnResumeClicked;
            _btnCancel.Click += OnCancelClicked;
            _btnRetry.Click += OnRetryClicked;
            _lvDownloads.KeyDown += OnListViewKeyDown;
            _lvDownloads.MouseClick += OnListViewMouseClick;
        }
        
        private void InitializeListView()
        {
            _lvDownloads.View = View.Details;
            _lvDownloads.FullRowSelect = true;
            _lvDownloads.GridLines = true;
            _lvDownloads.MultiSelect = true;
            
            // Columnas
            _lvDownloads.Columns.Add("Archivo", 300);
            _lvDownloads.Columns.Add("Tamaño", 100);
            _lvDownloads.Columns.Add("Progreso", 150);
            _lvDownloads.Columns.Add("Estado", 120);
            _lvDownloads.Columns.Add("Velocidad", 100);
            _lvDownloads.Columns.Add("Tiempo", 80);
            _lvDownloads.Columns.Add("Usuario", 120);
            _lvDownloads.Columns.Add("Red", 80);
        }
        
        /// <summary>
        /// Agrega una descarga a la cola
        /// </summary>
        public void QueueDownload(SearchResult result, string network = "Soulseek")
        {
            var task = new DownloadTask
            {
                Id = Guid.NewGuid().ToString(),
                File = result,
                Status = DownloadStatus.Queued,
                Network = network,
                AddedAt = DateTime.UtcNow,
                RetryCount = 0
            };
            
            lock (_queueLock)
            {
                _downloadQueue.Add(task);
            }
            
            UpdateUI();
            StartDownloadIfNeeded();
        }
        
        /// <summary>
        /// Inicia descargas si hay espacio disponible
        /// </summary>
        private void StartDownloadIfNeeded()
        {
            if (_isPaused) return;
            
            var activeCount = GetActiveDownloadCount();
            var maxParallel = _mainForm.MaxParallelDownloads;
            
            if (activeCount < maxParallel)
            {
                var nextTask = GetNextQueuedDownload();
                if (nextTask != null)
                {
                    _ = Task.Run(async () => await ProcessDownloadAsync(nextTask));
                }
            }
        }
        
        /// <summary>
        /// Procesa una descarga individual
        /// </summary>
        private async Task ProcessDownloadAsync(DownloadTask task)
        {
            try
            {
                task.Status = DownloadStatus.Downloading;
                task.StartedAt = DateTime.UtcNow;
                UpdateUI();
                
                // Simulación de descarga (reemplazar con lógica real)
                for (int i = 0; i <= 100; i += 5)
                {
                    if (_isPaused || task.CancellationToken.IsCancellationRequested)
                        break;
                    
                    task.Progress = i;
                    task.Speed = new Random().Next(50, 500) * 1024; // KB/s
                    UpdateUI();
                    await Task.Delay(200, task.CancellationToken);
                }
                
                if (task.Progress >= 100)
                {
                    task.Status = DownloadStatus.Completed;
                    task.CompletedAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = ex.Message;
                _mainForm.Log($"Error en descarga {task.File.Filename}: {ex.Message}");
            }
            finally
            {
                UpdateUI();
                StartDownloadIfNeeded(); // Iniciar siguiente
            }
        }
        
        /// <summary>
        /// Pausa todas las descargas
        /// </summary>
        public void PauseAll()
        {
            _isPaused = true;
            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();
            
            lock (_queueLock)
            {
                foreach (var task in _downloadQueue.Where(t => t.Status == DownloadStatus.Downloading))
                {
                    task.Status = DownloadStatus.Paused;
                }
            }
            
            UpdateUI();
        }
        
        /// <summary>
        /// Reanuda todas las descargas
        /// </summary>
        public void ResumeAll()
        {
            _isPaused = false;
            
            lock (_queueLock)
            {
                foreach (var task in _downloadQueue.Where(t => t.Status == DownloadStatus.Paused))
                {
                    task.Status = DownloadStatus.Queued;
                }
            }
            
            UpdateUI();
            StartDownloadIfNeeded();
        }
        
        /// <summary>
        /// Cancela descargas seleccionadas
        /// </summary>
        public void CancelSelected()
        {
            var selectedTasks = GetSelectedTasks();
            
            foreach (var task in selectedTasks)
            {
                task.CancellationToken?.Cancel();
                task.Status = DownloadStatus.Cancelled;
            }
            
            UpdateUI();
        }
        
        /// <summary>
        /// Reintenta descargas fallidas
        /// </summary>
        public void RetryFailed()
        {
            lock (_queueLock)
            {
                var failedTasks = _downloadQueue.Where(t => t.Status == DownloadStatus.Failed).ToList();
                foreach (var task in failedTasks)
                {
                    task.Status = DownloadStatus.Queued;
                    task.RetryCount++;
                    task.ErrorMessage = null;
                }
            }
            
            UpdateUI();
            StartDownloadIfNeeded();
        }
        
        /// <summary>
        /// Actualiza la interfaz de usuario
        /// </summary>
        private void UpdateUI()
        {
            if (_lvDownloads.InvokeRequired)
            {
                _lvDownloads.Invoke(new Action(UpdateUI));
                return;
            }
            
            _lvDownloads.BeginUpdate();
            
            try
            {
                _lvDownloads.Items.Clear();
                
                lock (_queueLock)
                {
                    foreach (var task in _downloadQueue)
                    {
                        var item = new ListViewItem(new[]
                        {
                            task.File.Filename,
                            UIHelpers.FormatFileSize(task.File.Size),
                            $"{task.Progress}%",
                            GetStatusText(task.Status),
                            UIHelpers.FormatFileSize(task.Speed) + "/s",
                            GetElapsedTime(task),
                            task.File.Username,
                            task.Network
                        });
                        
                        item.Tag = task;
                        item.BackColor = GetStatusColor(task.Status);
                        _lvDownloads.Items.Add(item);
                    }
                }
                
                UpdateStats();
            }
            finally
            {
                _lvDownloads.EndUpdate();
            }
        }
        
        private void UpdateStats()
        {
            lock (_queueLock)
            {
                var total = _downloadQueue.Count;
                var completed = _downloadQueue.Count(t => t.Status == DownloadStatus.Completed);
                var downloading = _downloadQueue.Count(t => t.Status == DownloadStatus.Downloading);
                var failed = _downloadQueue.Count(t => t.Status == DownloadStatus.Failed);
                
                _lblStats.Text = $"Total: {total} | Completados: {completed} | Descargando: {downloading} | Fallidos: {failed}";
                
                _btnPause.Enabled = downloading > 0 && !_isPaused;
                _btnResume.Enabled = _isPaused;
                _btnCancel.Enabled = GetSelectedTasks().Any();
                _btnRetry.Enabled = failed > 0;
            }
        }
        
        private int GetActiveDownloadCount()
        {
            lock (_queueLock)
            {
                return _downloadQueue.Count(t => t.Status == DownloadStatus.Downloading);
            }
        }
        
        private DownloadTask GetNextQueuedDownload()
        {
            lock (_queueLock)
            {
                return _downloadQueue.FirstOrDefault(t => t.Status == DownloadStatus.Queued);
            }
        }
        
        private List<DownloadTask> GetSelectedTasks()
        {
            return _lvDownloads.SelectedItems.Cast<ListViewItem>()
                .Select(item => item.Tag as DownloadTask)
                .Where(task => task != null)
                .ToList();
        }
        
        private string GetStatusText(DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Queued => "En cola",
                DownloadStatus.Downloading => "Descargando",
                DownloadStatus.Paused => "Pausado",
                DownloadStatus.Completed => "Completado",
                DownloadStatus.Failed => "Fallido",
                DownloadStatus.Cancelled => "Cancelado",
                _ => status.ToString()
            };
        }
        
        private Color GetStatusColor(DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Queued => Color.LightGray,
                DownloadStatus.Downloading => Color.LightGreen,
                DownloadStatus.Paused => Color.Yellow,
                DownloadStatus.Completed => Color.Green,
                DownloadStatus.Failed => Color.LightCoral,
                DownloadStatus.Cancelled => Color.Orange,
                _ => Color.White
            };
        }
        
        private string GetElapsedTime(DownloadTask task)
        {
            if (task.StartedAt == null) return "-";
            
            var elapsed = DateTime.UtcNow - task.StartedAt.Value;
            return elapsed.TotalMinutes < 60 
                ? $"{elapsed.TotalMinutes:F0}m" 
                : $"{elapsed.TotalHours:F1}h";
        }
        
        // Event Handlers
        private void OnPauseClicked(object sender, EventArgs e) => PauseAll();
        private void OnResumeClicked(object sender, EventArgs e) => ResumeAll();
        private void OnCancelClicked(object sender, EventArgs e) => CancelSelected();
        private void OnRetryClicked(object sender, EventArgs e) => RetryFailed();
        
        private void OnListViewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
                CancelSelected();
            else if (e.KeyCode == Keys.F5)
                RetryFailed();
        }
        
        private void OnListViewMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Cancelar", null, (s, e) => CancelSelected());
                contextMenu.Items.Add("Reintentar", null, (s, e) => RetryFailed());
                contextMenu.Show(_lvDownloads, e.Location);
            }
        }
    }
}
