using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;
using SlskDown.Models;
using SlskDown.Services;
using SlskDown.Core.AI;
using SlskDown.Core.Neural;
using SlskDown.Core.Performance;

namespace SlskDown.UI
{
    /// <summary>
    /// Manejador de la pestaña de búsqueda con IA predictiva y clasificación neural
    /// </summary>
    public class SearchTabManager : IDisposable
    {
        private readonly MainForm _mainForm;
        private readonly AutoLearningEngine _autoLearningEngine;
        private readonly ContentClassifier _contentClassifier;
        private readonly MemoryPoolManager _memoryPool;
        
        // Controles UI
        private TextBox txtSearch;
        private Button btnSearch;
        private ListView lvResults;
        private ComboBox cbSearchType;
        private CheckBox chkPredictive;
        private CheckBox chkClassifyResults;
        private Label lblSearchStatus;
        private ProgressBar progressBarSearch;
        
        // Estado
        private bool _isSearching = false;
        private List<AutoSearchFileResult> _currentResults = new List<AutoSearchFileResult>();
        private Dictionary<string, List<PredictedContent>> _predictions = new Dictionary<string, List<PredictedContent>>();
        
        public event EventHandler<SearchCompletedEventArgs> SearchCompleted;
        public event EventHandler<PredictionAvailableEventArgs> PredictionsAvailable;

        public SearchTabManager(MainForm mainForm)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _autoLearningEngine = new AutoLearningEngine();
            _contentClassifier = new ContentClassifier();
            _memoryPool = MemoryPoolManager.Instance;
            
            InitializeControls();
            SetupEventHandlers();
        }

        /// <summary>
        /// Inicializa controles de la pestaña de búsqueda
        /// </summary>
        private void InitializeControls()
        {
            // Panel de búsqueda principal
            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                BackColor = _mainForm.BackColor
            };

            // TextBox de búsqueda
            txtSearch = new TextBox
            {
                Location = new System.Drawing.Point(10, 10),
                Width = 400,
                Font = new System.Drawing.Font("Segoe UI", 10f),
                PlaceholderText = "Buscar música, archivos, autores..."
            };

            // ComboBox de tipo de búsqueda
            cbSearchType = new ComboBox
            {
                Location = new System.Drawing.Point(420, 10),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cbSearchType.Items.AddRange(new[] { "Todo", "Música", "Audio", "Video", "Documentos", "Imágenes" });
            cbSearchType.SelectedIndex = 0;

            // Checkboxes de características avanzadas
            chkPredictive = new CheckBox
            {
                Location = new System.Drawing.Point(550, 12),
                Text = "🧠 IA Predictiva",
                Checked = true,
                ForeColor = System.Drawing.Color.LightGreen
            };

            chkClassifyResults = new CheckBox
            {
                Location = new System.Drawing.Point(680, 12),
                Text = "🔮 Clificar IA",
                Checked = true,
                ForeColor = System.Drawing.Color.LightBlue
            };

            // Botón de búsqueda
            btnSearch = new Button
            {
                Location = new System.Drawing.Point(800, 8),
                Width = 100,
                Height = 30,
                Text = "Buscar",
                BackColor = System.Drawing.Color.FromArgb(0, 120, 215),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };

            // Label de estado
            lblSearchStatus = new Label
            {
                Location = new System.Drawing.Point(10, 45),
                Width = 900,
                Text = "Listo para buscar",
                ForeColor = System.Drawing.Color.LightGray
            };

            // ProgressBar
            progressBarSearch = new ProgressBar
            {
                Location = new System.Drawing.Point(10, 70),
                Width = 900,
                Height = 20,
                Visible = false
            };

            // ListView de resultados
            lvResults = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
                ForeColor = System.Drawing.Color.White
            };

            // Configurar columnas
            lvResults.Columns.Add("Archivo", 300);
            lvResults.Columns.Add("Autor", 150);
            lvResults.Columns.Add("Tamaño", 80);
            lvResults.Columns.Add("Bitrate", 60);
            lvResults.Columns.Add("Género", 100);
            lvResults.Columns.Add("Calidad", 80);
            lvResults.Columns.Add("Confianza", 80);
            lvResults.Columns.Add("Red", 80);

            // Agregar controles al panel
            searchPanel.Controls.AddRange(new Control[]
            {
                txtSearch, cbSearchType, chkPredictive, chkClassifyResults,
                btnSearch, lblSearchStatus, progressBarSearch
            });

            // Agregar al contenedor principal
            _mainForm.Controls.Add(searchPanel);
            _mainForm.Controls.Add(lvResults);
        }

        /// <summary>
        /// Configura manejadores de eventos
        /// </summary>
        private void SetupEventHandlers()
        {
            btnSearch.Click += OnSearchClicked;
            txtSearch.KeyDown += OnSearchKeyDown;
            txtSearch.TextChanged += OnSearchTextChanged;
            lvResults.DoubleClick += OnResultDoubleClicked;
            chkPredictive.CheckedChanged += OnPredictiveChanged;
            chkClassifyResults.CheckedChanged += OnClassificationChanged;
        }

        /// <summary>
        /// Maneja clic en botón de búsqueda
        /// </summary>
        private async void OnSearchClicked(object sender, EventArgs e)
        {
            await PerformSearch();
        }

        /// <summary>
        /// Maneja Enter en textbox de búsqueda
        /// </summary>
        private async void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                await PerformSearch();
            }
        }

        /// <summary>
        /// Maneja cambios en texto para predicciones en tiempo real
        /// </summary>
        private async void OnSearchTextChanged(object sender, EventArgs e)
        {
            if (!chkPredictive.Checked || string.IsNullOrWhiteSpace(txtSearch.Text))
                return;

            try
            {
                var suggestions = _autoLearningEngine.GetSearchSuggestions(txtSearch.Text);
                if (suggestions.Any())
                {
                    PredictionsAvailable?.Invoke(this, new PredictionAvailableEventArgs
                    {
                        Query = txtSearch.Text,
                        Suggestions = suggestions,
                        Type = PredictionType.Suggestions
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting suggestions: {ex.Message}");
            }
        }

        /// <summary>
        /// Realiza búsqueda con IA predictiva
        /// </summary>
        private async Task PerformSearch()
        {
            if (_isSearching || string.IsNullOrWhiteSpace(txtSearch.Text))
                return;

            _isSearching = true;
            var query = txtSearch.Text.Trim();
            var searchType = cbSearchType.SelectedItem?.ToString() ?? "Todo";

            UpdateUI(true, "Iniciando búsqueda...");

            try
            {
                // Obtener predicciones si está activada IA predictiva
                List<PredictedContent> predictions = new List<PredictedContent>();
                if (chkPredictive.Checked)
                {
                    predictions = await _autoLearningEngine.PredictContent(query, 10);
                    _predictions[query] = predictions;
                    
                    PredictionsAvailable?.Invoke(this, new PredictionAvailableEventArgs
                    {
                        Query = query,
                        Predictions = predictions,
                        Type = PredictionType.Content
                    });
                }

                // Realizar búsqueda real
                var results = await ExecuteSearch(query, searchType);
                _currentResults = results;

                // Clasificar resultados si está activado
                if (chkClassifyResults.Checked)
                {
                    await ClassifyResults(results);
                }

                // Registrar aprendizaje
                _autoLearningEngine.RecordSearch(query, results, DateTime.UtcNow);

                // Actualizar UI
                UpdateResultsList(results);
                UpdateUI(false, $"{results.Count} resultados encontrados");

                SearchCompleted?.Invoke(this, new SearchCompletedEventArgs
                {
                    Query = query,
                    Results = results,
                    Predictions = predictions,
                    Duration = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                UpdateUI(false, $"Error en búsqueda: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
            finally
            {
                _isSearching = false;
            }
        }

        /// <summary>
        /// Ejecuta búsqueda real usando el motor de MainForm
        /// </summary>
        private async Task<List<AutoSearchFileResult>> ExecuteSearch(string query, string searchType)
        {
            return await Task.Run(() =>
            {
                // Usar memory pool para optimizar rendimiento
                return _memoryPool.UsingList<AutoSearchFileResult>(resultsList =>
                {
                    // Simular búsqueda (integrar con MainForm real)
                    var random = new Random(query.GetHashCode());
                    
                    for (int i = 0; i < random.Next(10, 100); i++)
                    {
                        var result = new AutoSearchFileResult
                        {
                            Filename = $"{query}_result_{i}.mp3",
                            Author = $"Artist_{random.Next(1, 100)}",
                            Size = random.Next(1_000_000, 10_000_000),
                            BitRate = random.Next(128, 320),
                            Network = "Soulseek"
                        };
                        
                        resultsList.Add(result);
                    }
                    
                    return resultsList.ToList();
                });
            });
        }

        /// <summary>
        /// Clasifica resultados usando red neuronal
        /// </summary>
        private async Task ClassifyResults(List<AutoSearchFileResult> results)
        {
            if (!results.Any()) return;

            try
            {
                UpdateUI(true, "🧠 Clasificando con IA...");

                // Procesar en lotes para mejor rendimiento
                const int batchSize = 20;
                var tasks = new List<Task<ContentClassification>>();

                for (int i = 0; i < results.Count; i += batchSize)
                {
                    var batch = results.Skip(i).Take(batchSize).ToList();
                    var batchTask = Task.Run(async () =>
                    {
                        var classifications = new List<ContentClassification>();
                        foreach (var result in batch)
                        {
                            var classification = await _contentClassifier.ClassifyFile(result);
                            classifications.Add(classification);
                        }
                        return classifications;
                    });
                    
                    tasks.Add(batchTask);
                }

                var allClassifications = await Task.WhenAll(tasks);
                var classifications = allClassifications.SelectMany(c => c).ToList();

                // Actualizar resultados con clasificación
                for (int i = 0; i < results.Count && i < classifications.Count; i++)
                {
                    results[i].Genre = classifications[i].PredictedGenre;
                    results[i].Quality = classifications[i].PredictedQuality;
                    results[i].Confidence = classifications[i].ConfidenceScore;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Classification error: {ex.Message}");
            }
        }

        /// <summary>
        /// Detecta duplicados en resultados actuales
        /// </summary>
        public async Task<List<DuplicateGroup>> DetectDuplicates()
        {
            if (!_currentResults.Any()) return new List<DuplicateGroup>();

            try
            {
                UpdateUI(true, "Detectando duplicados...");
                
                var duplicates = await _contentClassifier.FindDuplicates(_currentResults);
                
                UpdateUI(false, $"{duplicates.Count} grupos de duplicados encontrados");
                
                return duplicates;
            }
            catch (Exception ex)
            {
                UpdateUI(false, $"Error detectando duplicados: {ex.Message}");
                return new List<DuplicateGroup>();
            }
        }

        /// <summary>
        /// Actualiza lista de resultados en UI
        /// </summary>
        private void UpdateResultsList(List<AutoSearchFileResult> results)
        {
            _mainForm.SafeBeginInvoke(() =>
            {
                lvResults.BeginUpdate();
                try
                {
                    lvResults.Items.Clear();
                    
                    var items = _memoryPool.UsingArray<ListViewItem>(results.Count, listItems =>
                    {
                        for (int i = 0; i < results.Count; i++)
                        {
                            var result = results[i];
                            var item = new ListViewItem(new[]
                            {
                                result.Filename,
                                result.Author,
                                FormatFileSize(result.Size),
                                $"{result.BitRate}kbps",
                                result.Genre ?? "Desconocido",
                                result.Quality ?? "Estándar",
                                $"{result.Confidence:P1}",
                                result.Network
                            });
                            
                            item.Tag = result;
                            listItems[i] = item;
                        }
                        
                        return listItems;
                    });
                    
                    lvResults.Items.AddRange(items);
                }
                finally
                {
                    lvResults.EndUpdate();
                }
            });
        }

        /// <summary>
        /// Actualiza estado de UI
        /// </summary>
        private void UpdateUI(bool isSearching, string status)
        {
            _mainForm.SafeBeginInvoke(() =>
            {
                btnSearch.Enabled = !isSearching;
                btnSearch.Text = isSearching ? "Buscando..." : "Buscar";
                lblSearchStatus.Text = status;
                progressBarSearch.Visible = isSearching;
                
                if (isSearching)
                {
                    progressBarSearch.Style = ProgressBarStyle.Marquee;
                }
            });
        }

        /// <summary>
        /// Maneja doble clic en resultado
        /// </summary>
        private void OnResultDoubleClicked(object sender, EventArgs e)
        {
            if (lvResults.SelectedItems.Count > 0)
            {
                var result = lvResults.SelectedItems[0].Tag as AutoSearchFileResult;
                if (result != null)
                {
                    // Registrar descarga para aprendizaje
                    _autoLearningEngine.RecordDownload(
                        result.Filename, 
                        result.Author, 
                        txtSearch.Text, 
                        DateTime.UtcNow);
                    
                    // Iniciar descarga
                    _mainForm.QueueDownload(result);
                }
            }
        }

        /// <summary>
        /// Maneja cambio en checkbox predictivo
        /// </summary>
        private void OnPredictiveChanged(object sender, EventArgs e)
        {
            if (chkPredictive.Checked)
            {
                lblSearchStatus.Text = "🧠 IA Predictiva activada";
            }
            else
            {
                lblSearchStatus.Text = "IA Predictiva desactivada";
                _predictions.Clear();
            }
        }

        /// <summary>
        /// Maneja cambio en checkbox de clasificación
        /// </summary>
        private void OnClassificationChanged(object sender, EventArgs e)
        {
            if (chkClassifyResults.Checked && _currentResults.Any())
            {
                // Reclasificar resultados existentes
                Task.Run(async () => await ClassifyResults(_currentResults));
            }
        }

        /// <summary>
        /// Formatea tamaño de archivo
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Limpia resultados actuales
        /// </summary>
        public void ClearResults()
        {
            _currentResults.Clear();
            _predictions.Clear();
            
            _mainForm.SafeBeginInvoke(() =>
            {
                lvResults.Items.Clear();
                lblSearchStatus.Text = "Listo para buscar";
            });
        }

        /// <summary>
        /// Obtiene estadísticas de búsqueda
        /// </summary>
        public SearchStatistics GetStatistics()
        {
            return new SearchStatistics
            {
                TotalSearches = _autoLearningEngine._searchHistory.Count,
                TotalDownloads = _autoLearningEngine._downloadHistory.Count,
                CurrentResults = _currentResults.Count,
                PredictiveEnabled = chkPredictive.Checked,
                ClassificationEnabled = chkClassifyResults.Checked,
                LastSearch = txtSearch.Text,
                AverageConfidence = _currentResults.Any() ? 
                    _currentResults.Average(r => r.Confidence) : 0
            };
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            _autoLearningEngine?.Dispose();
            _contentClassifier?.Dispose();
        }
    }

    #region Event Args y Modelos

    public class SearchCompletedEventArgs : EventArgs
    {
        public string Query { get; set; }
        public List<AutoSearchFileResult> Results { get; set; }
        public List<PredictedContent> Predictions { get; set; }
        public DateTime Duration { get; set; }
    }

    public class PredictionAvailableEventArgs : EventArgs
    {
        public string Query { get; set; }
        public List<string> Suggestions { get; set; }
        public List<PredictedContent> Predictions { get; set; }
        public PredictionType Type { get; set; }
    }

    public class SearchStatistics
    {
        public int TotalSearches { get; set; }
        public int TotalDownloads { get; set; }
        public int CurrentResults { get; set; }
        public bool PredictiveEnabled { get; set; }
        public bool ClassificationEnabled { get; set; }
        public string LastSearch { get; set; }
        public double AverageConfidence { get; set; }
    }

    #endregion
}
