using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlskDown.Core.AI;
using SlskDown.Models;

namespace SlskDown
{
    public partial class MainForm
    {
        private OllamaClient ollamaClient;
        private IntelligentSearchEngine intelligentSearch;
        private AIRecommendationEngine recommendationEngine;
        private AIFileTagger fileTagger;
        private AIQualityPredictor qualityPredictor;
        private SlskDownAssistant assistant;
        private AvailabilityPredictor availabilityPredictor;
        private BookSummarizer bookSummarizer;
        private MalwareDetector malwareDetector;

        private bool aiEnabled = false;
        private string ollamaUrl = "http://localhost:11434";
        private string ollamaModel = "llama2";

        private TextBox txtOllamaUrl;
        private ComboBox cmbOllamaModel;
        private CheckBox chkEnableAI;
        private Button btnTestAI;
        private Button btnAISearch;
        private Button btnRecommendations;

        private void InitializeAIFeatures()
        {
            try
            {
                Log("🤖 Inicializando funcionalidades de IA...");

                LoadAIConfig();

                if (!string.IsNullOrEmpty(openAIApiKey))
                {
                    InitializeAIClients();
                }

                CreateAIUI();

                Log("✅ Funcionalidades de IA inicializadas");
            }
            catch (Exception ex)
            {
                Log($"❌ Error inicializando IA: {ex.Message}");
            }
        }

        private void LoadAIConfig()
        {
            try
            {
                ollamaUrl = config.GetValue("Ollama_Url", "http://localhost:11434");
                ollamaModel = config.GetValue("Ollama_Model", "llama2");
                aiEnabled = config.GetValue("AI_Enabled", false);
            }
            catch (Exception ex)
            {
                Log($"⚠️ Error cargando config IA: {ex.Message}");
            }
        }

        private void InitializeAIClients()
        {
            try
            {
                ollamaClient = new OllamaClient(ollamaUrl, ollamaModel);
                ollamaClient.OnLog += Log;

                intelligentSearch = new IntelligentSearchEngine(ollamaClient, SearchWithAI);
                intelligentSearch.OnLog += Log;

                recommendationEngine = new AIRecommendationEngine(ollamaClient, SearchWithAI);
                recommendationEngine.OnLog += Log;

                fileTagger = new AIFileTagger(ollamaClient);
                fileTagger.OnLog += Log;

                qualityPredictor = new AIQualityPredictor(ollamaClient);
                qualityPredictor.OnLog += Log;

                assistant = new SlskDownAssistant(ollamaClient);
                assistant.OnLog += Log;

                availabilityPredictor = new AvailabilityPredictor(ollamaClient);
                availabilityPredictor.OnLog += Log;

                bookSummarizer = new BookSummarizer(ollamaClient);
                bookSummarizer.OnLog += Log;

                malwareDetector = new MalwareDetector(ollamaClient);
                malwareDetector.OnLog += Log;

                aiEnabled = true;
                Log("✅ Clientes de IA inicializados");
            }
            catch (Exception ex)
            {
                Log($"❌ Error inicializando clientes IA: {ex.Message}");
                aiEnabled = false;
            }
        }

        private void CreateAIUI()
        {
            var toolbar = Controls.Find("mainToolbar", true).FirstOrDefault() as ToolStrip;
            
            if (toolbar != null)
            {
                var btnAI = new ToolStripButton("🤖 IA");
                btnAI.Click += (s, e) => ShowAIPanel();
                toolbar.Items.Add(btnAI);

            }
        }

        private void ShowAIPanel()
        {
            var form = new Form
            {
                Text = "🤖 Configuración de IA",
                Size = new Size(600, 500),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false
            };

            var lblUrl = new Label
            {
                Text = "Ollama URL:",
                Location = new Point(20, 20),
                AutoSize = true
            };

            txtOllamaUrl = new TextBox
            {
                Location = new Point(20, 45),
                Width = 540,
                Text = ollamaUrl
            };

            var lblModel = new Label
            {
                Text = "Modelo:",
                Location = new Point(20, 80),
                AutoSize = true
            };

            cmbOllamaModel = new ComboBox
            {
                Location = new Point(20, 105),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            
            // Cargar modelos disponibles desde Ollama
            Task.Run(async () =>
            {
                try
                {
                    var testClient = new OllamaClient(ollamaUrl, "llama2");
                    var models = await testClient.GetAvailableModelsAsync();
                    
                    this.Invoke((Action)(() =>
                    {
                        if (models != null && models.Any())
                        {
                            cmbOllamaModel.Items.Clear();
                            cmbOllamaModel.Items.AddRange(models.ToArray());
                            
                            // Seleccionar el modelo actual si existe
                            if (models.Contains(ollamaModel))
                            {
                                cmbOllamaModel.SelectedItem = ollamaModel;
                            }
                            else if (cmbOllamaModel.Items.Count > 0)
                            {
                                cmbOllamaModel.SelectedIndex = 0;
                            }
                        }
                        else
                        {
                            // Fallback a lista por defecto si no hay conexión
                            cmbOllamaModel.Items.AddRange(new object[] { "llama2", "mistral", "phi", "codellama", "llama3" });
                            cmbOllamaModel.SelectedItem = ollamaModel;
                        }
                    }));
                }
                catch
                {
                    // Fallback a lista por defecto en caso de error
                    this.Invoke((Action)(() =>
                    {
                        cmbOllamaModel.Items.AddRange(new object[] { "llama2", "mistral", "phi", "codellama", "llama3" });
                        cmbOllamaModel.SelectedItem = ollamaModel;
                    }));
                }
            });

            chkEnableAI = new CheckBox
            {
                Text = "Habilitar funcionalidades de IA (requiere Ollama instalado)",
                Location = new Point(20, 140),
                AutoSize = true,
                Checked = aiEnabled
            };

            btnTestAI = new Button
            {
                Text = "Probar Conexión",
                Location = new Point(20, 170),
                Width = 150
            };
            btnTestAI.Click += async (s, e) => await TestAIConnection();

            var lblInfo = new Label
            {
                Text = "💡 Ollama es GRATIS y funciona localmente (sin API Key)",
                Location = new Point(20, 200),
                AutoSize = true,
                ForeColor = Color.Blue
            };

            var lblFeatures = new Label
            {
                Text = "Funcionalidades disponibles:",
                Location = new Point(20, 230),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };

            var features = new[]
            {
                "🔍 Búsqueda Inteligente con NLP",
                "📚 Recomendaciones Personalizadas",
                "🏷️ Auto-Tagging de Archivos",
                "🎯 Predicción de Calidad",
                "💬 Chatbot Asistente",
                "🔮 Predicción de Disponibilidad",
                "📝 Resúmenes de Libros",
                "🚨 Detección de Malware"
            };

            var yPos = 260;
            foreach (var feature in features)
            {
                var lbl = new Label
                {
                    Text = feature,
                    Location = new Point(40, yPos),
                    AutoSize = true
                };
                form.Controls.Add(lbl);
                yPos += 25;
            }

            var btnSave = new Button
            {
                Text = "Guardar",
                Location = new Point(380, 420),
                Width = 90
            };
            btnSave.Click += (s, e) =>
            {
                SaveAIConfig();
                form.Close();
            };

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(480, 420),
                Width = 90
            };
            btnCancel.Click += (s, e) => form.Close();

            form.Controls.AddRange(new Control[] 
            { 
                lblUrl, txtOllamaUrl, lblModel, cmbOllamaModel, 
                chkEnableAI, btnTestAI, lblInfo, lblFeatures, 
                btnSave, btnCancel 
            });

            form.ShowDialog(this);
        }


        private async Task TestAIConnection()
        {
            try
            {
                btnTestAI.Enabled = false;
                btnTestAI.Text = "Probando...";

                var testUrl = txtOllamaUrl.Text.Trim();
                var testModel = cmbOllamaModel.SelectedItem?.ToString() ?? "llama2";

                var testClient = new OllamaClient(testUrl, testModel);
                var isAvailable = await testClient.IsAvailableAsync();

                if (isAvailable)
                {
                    var models = await testClient.GetAvailableModelsAsync();
                    var modelsList = string.Join(", ", models.Take(5));
                    MessageBox.Show(
                        $"✅ Conexión exitosa!\n\nModelos disponibles: {modelsList}",
                        "Éxito",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        "❌ No se pudo conectar a Ollama.\n\n" +
                        "Asegúrate de que Ollama esté instalado y ejecutándose.\n" +
                        "Descárgalo en: https://ollama.ai",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Error: {ex.Message}\n\n" +
                    "Verifica que Ollama esté instalado y ejecutándose.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                btnTestAI.Enabled = true;
                btnTestAI.Text = "Probar Conexión";
            }
        }

        private void SaveAIConfig()
        {
            try
            {
                ollamaUrl = txtOllamaUrl.Text.Trim();
                ollamaModel = cmbOllamaModel.SelectedItem?.ToString() ?? "llama2";
                aiEnabled = chkEnableAI.Checked;

                config.SetValue("Ollama_Url", ollamaUrl);
                config.SetValue("Ollama_Model", ollamaModel);
                config.SetValue("AI_Enabled", aiEnabled);
                SaveConfig();

                if (aiEnabled)
                {
                    InitializeAIClients();
                }

                MessageBox.Show(
                    "✅ Configuración guardada\n\n" +
                    "Recuerda: Ollama debe estar ejecutándose para usar IA.",
                    "Éxito",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error guardando: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<List<SearchResult>> SearchWithAI(string query)
        {
            try
            {
                await PerformSearchAsync(query);
                await Task.Delay(2000);
                
                return currentSearchResults ?? new List<SearchResult>();
            }
            catch
            {
                return new List<SearchResult>();
            }
        }

        private async Task PerformAISearch(string query)
        {
            if (!aiEnabled || intelligentSearch == null)
            {
                await PerformSearchAsync(query);
                return;
            }

            try
            {
                Log($"🤖 Búsqueda inteligente: {query}");
                var results = await intelligentSearch.SmartSearchAsync(query);
                
                DisplaySearchResults(results);
                Log($"✅ {results.Count} resultados encontrados con IA");
            }
            catch (Exception ex)
            {
                Log($"❌ Error en búsqueda IA: {ex.Message}");
                await PerformSearchAsync(query);
            }
        }

        private async Task ShowRecommendations(string bookTitle, string author = null)
        {
            if (!aiEnabled || recommendationEngine == null)
                return;

            try
            {
                Log($"🤖 Generando recomendaciones...");
                var recommendations = await recommendationEngine.GetRecommendationsAsync(bookTitle, author);

                if (recommendations.Any())
                {
                    ShowRecommendationsDialog(recommendations);
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error generando recomendaciones: {ex.Message}");
            }
        }

        private void ShowRecommendationsDialog(List<BookRecommendation> recommendations)
        {
            var form = new Form
            {
                Text = "📚 Recomendaciones de IA",
                Size = new Size(700, 500),
                StartPosition = FormStartPosition.CenterParent
            };

            var listView = new ListView
            {
                Location = new Point(10, 10),
                Size = new Size(660, 430),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            listView.Columns.Add("Título", 250);
            listView.Columns.Add("Autor", 150);
            listView.Columns.Add("Razón", 200);
            listView.Columns.Add("Disponible", 80);

            foreach (var rec in recommendations)
            {
                var item = new ListViewItem(rec.Title);
                item.SubItems.Add(rec.Author);
                item.SubItems.Add(rec.Reason);
                item.SubItems.Add(rec.Available ? $"✅ ({rec.ResultCount})" : "❌");
                item.Tag = rec;
                listView.Items.Add(item);
            }

            var btnDownload = new Button
            {
                Text = "Descargar Seleccionado",
                Location = new Point(480, 450),
                Width = 180
            };
            btnDownload.Click += async (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                {
                    var rec = listView.SelectedItems[0].Tag as BookRecommendation;
                    if (rec?.BestResult != null)
                    {
                        await DownloadFile(rec.BestResult);
                    }
                }
            };

            form.Controls.AddRange(new Control[] { listView, btnDownload });
            form.ShowDialog(this);
        }
    }
}
