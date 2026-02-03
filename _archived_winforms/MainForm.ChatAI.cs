using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;
using SlskDown.Models;

namespace SlskDown
{
    public partial class MainForm
    {
        // Nota: Las variables de Chat ya están definidas en MainForm.cs principal

        private void CreateChatAITab(Panel parent)
        {
            parent.BackColor = Color.FromArgb(30, 30, 30);
            
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(20),
                BackColor = Color.Transparent
            };
            
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            
            // Botones rápidos
            var quickButtonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 0, 10),
                WrapContents = true
            };
            
            var btnQuickSearch = CreateQuickButton("📚 Buscar", Color.FromArgb(0, 120, 215));
            btnQuickSearch.Click += (s, e) => txtChatInput.Text = "busca libros de ";
            quickButtonsPanel.Controls.Add(btnQuickSearch);
            
            var btnQuickStats = CreateQuickButton("📊 Estadísticas", Color.FromArgb(138, 43, 226));
            btnQuickStats.Click += (s, e) => { txtChatInput.Text = "estadísticas"; _ = SendChatMessage(); };
            quickButtonsPanel.Controls.Add(btnQuickStats);
            
            var btnQuickHelp = CreateQuickButton("❓ Ayuda", Color.FromArgb(100, 100, 100));
            btnQuickHelp.Click += (s, e) => { txtChatInput.Text = "ayuda"; _ = SendChatMessage(); };
            quickButtonsPanel.Controls.Add(btnQuickHelp);
            
            mainLayout.Controls.Add(quickButtonsPanel, 0, 0);
            
            // Historial de chat
            txtChatHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10)
            };
            
            var historyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(2)
            };
            historyPanel.Controls.Add(txtChatHistory);
            mainLayout.Controls.Add(historyPanel, 0, 1);
            
            // Área de input
            var inputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 0)
            };
            
            var inputLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            
            inputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            inputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            
            txtChatInput = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            txtChatInput.KeyDown += TxtChatInput_KeyDown;
            inputLayout.Controls.Add(txtChatInput, 0, 0);
            
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 5, 0, 0)
            };
            
            btnChatSend = new Button
            {
                Text = "📤 Enviar",
                Width = 120,
                Height = 35,
                BackColor = Color.FromArgb(0, 120, 215),
                FlatStyle = FlatStyle.Flat
            };
            btnChatSend.Click += async (s, e) => await SendChatMessage();
            buttonPanel.Controls.Add(btnChatSend);
            
            btnChatClear = new Button
            {
                Text = "🗑️ Limpiar",
                Width = 120,
                Height = 35,
                BackColor = Color.FromArgb(80, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(10, 0, 0, 0)
            };
            btnChatClear.Click += (s, e) => ClearChatHistory();
            buttonPanel.Controls.Add(btnChatClear);
            
            inputLayout.Controls.Add(buttonPanel, 0, 1);
            inputPanel.Controls.Add(inputLayout);
            mainLayout.Controls.Add(inputPanel, 0, 2);
            
            // Barra de estado
            lblChatStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "🤖 Chat IA - Escribe 'ayuda' para comenzar",
                ForeColor = Color.LightGreen,
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };
            mainLayout.Controls.Add(lblChatStatus, 0, 3);
            
            parent.Controls.Add(mainLayout);
            
            // Inicializar cliente Ollama
            InitializeOllamaClient();
            
            // Mensaje de bienvenida
            AppendChatMessage("SISTEMA", 
                "🤖 Chat IA Activado\n\n" +
                "Comandos disponibles:\n" +
                "• busca [autor] - Buscar libros\n" +
                "• estado - Ver estado de descargas\n" +
                "• estadísticas - Ver estadísticas\n" +
                "• ayuda - Mostrar ayuda completa\n\n" +
                "También puedes hacer preguntas en lenguaje natural.",
                Color.FromArgb(100, 200, 255));
        }

        private void InitializeOllamaClient()
        {
            try
            {
                ollamaClient = new SlskDown.Core.AI.OllamaClient(ollamaUrl, ollamaModel);
                
                // Inicializar TurboMode
                turboMode = new SlskDown.Core.AI.TurboMode(ollamaClient)
                {
                    Enabled = true,
                    FallbackModel = ollamaModel
                };
                
                // Inicializar ModelPreloader
                modelPreloader = new SlskDown.Core.AI.ModelPreloader(ollamaUrl);
                
                // Verificar disponibilidad en background
                _ = Task.Run(async () =>
                {
                    var available = await ollamaClient.IsAvailableAsync();
                    SafeInvoke(() =>
                    {
                        if (lblChatStatus != null)
                        {
                            if (available)
                            {
                                lblChatStatus.Text = $"✅ Ollama conectado | Modelo: {ollamaModel}";
                                lblChatStatus.ForeColor = Color.LightGreen;
                            }
                            else
                            {
                                lblChatStatus.Text = "⚠️ Ollama no disponible - Usando modo local";
                                lblChatStatus.ForeColor = Color.Orange;
                            }
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Log($"Error inicializando Ollama: {ex.Message}");
            }
        }

        private Button CreateQuickButton(string text, Color color)
        {
            return new Button
            {
                Text = text,
                Width = 140,
                Height = 40,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
        }

        private void TxtChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                _ = SendChatMessage();
            }
            else if (e.KeyCode == Keys.Up && string.IsNullOrWhiteSpace(txtChatInput.Text))
            {
                e.Handled = true;
                NavigateHistory(-1);
            }
            else if (e.KeyCode == Keys.Down && string.IsNullOrWhiteSpace(txtChatInput.Text))
            {
                e.Handled = true;
                NavigateHistory(1);
            }
        }

        private void NavigateHistory(int direction)
        {
            if (commandHistory.Count == 0) return;
            
            historyIndex += direction;
            historyIndex = Math.Max(-1, Math.Min(commandHistory.Count - 1, historyIndex));
            
            if (historyIndex >= 0 && historyIndex < commandHistory.Count)
            {
                txtChatInput.Text = commandHistory[historyIndex];
                txtChatInput.SelectionStart = txtChatInput.Text.Length;
            }
            else
            {
                txtChatInput.Text = "";
            }
        }

        private async Task SendChatMessage()
        {
            if (txtChatInput == null || txtChatHistory == null) return;
            if (isChatProcessing) return;
            
            var userMessage = txtChatInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(userMessage)) return;
            
            // Agregar a historial
            commandHistory.Insert(0, userMessage);
            if (commandHistory.Count > 50) commandHistory.RemoveAt(50);
            historyIndex = -1;
            
            // Mostrar mensaje del usuario
            AppendChatMessage("TÚ", userMessage, Color.FromArgb(100, 200, 255));
            txtChatInput.Clear();
            
            isChatProcessing = true;
            btnChatSend.Enabled = false;
            
            try
            {
                var lower = userMessage.ToLower();
                
                // Comandos locales (sin IA)
                if (lower == "ayuda" || lower == "help")
                {
                    ShowHelp();
                }
                else if (lower == "estado" || lower == "status")
                {
                    ShowStatus();
                }
                else if (lower == "estadísticas" || lower == "stats")
                {
                    ShowStatistics();
                }
                else if (lower.StartsWith("busca ") || lower.StartsWith("search "))
                {
                    var query = userMessage.Substring(userMessage.IndexOf(' ') + 1);
                    await PerformSearch(query);
                }
                else
                {
                    // Intentar con Ollama
                    await ProcessWithOllama(userMessage);
                }
            }
            catch (Exception ex)
            {
                AppendChatMessage("ERROR", $"Error: {ex.Message}", Color.FromArgb(255, 100, 100));
                Log($"Error en chat: {ex.Message}");
            }
            finally
            {
                isChatProcessing = false;
                btnChatSend.Enabled = true;
            }
        }

        private async Task ProcessWithOllama(string userMessage)
        {
            if (ollamaClient == null)
            {
                AppendChatMessage("SISTEMA", "IA no disponible. Usa comandos como: ayuda, estado, estadísticas, busca [autor]", Color.Orange);
                return;
            }
            
            try
            {
                var available = await ollamaClient.IsAvailableAsync();
                if (!available)
                {
                    AppendChatMessage("SISTEMA", 
                        "⚠️ Ollama no está disponible.\n\n" +
                        "Para usar IA:\n" +
                        "1. Instala Ollama: https://ollama.ai\n" +
                        "2. Ejecuta: ollama pull llama3.2:3b\n" +
                        "3. Reinicia la aplicación\n\n" +
                        "Mientras tanto, usa comandos: ayuda, estado, estadísticas", 
                        Color.Orange);
                    return;
                }
                
                // Iniciar animación de "pensando"
                var thinkingTimer = new System.Windows.Forms.Timer();
                int dotCount = 0;
                string[] thinkingFrames = new[] { "🤔 Pensando", "🤔 Pensando.", "🤔 Pensando..", "🤔 Pensando..." };
                
                AppendChatMessage("IA", thinkingFrames[0], Color.FromArgb(150, 150, 150));
                int thinkingMessageStart = txtChatHistory.Text.LastIndexOf("IA: " + thinkingFrames[0]);
                
                thinkingTimer.Interval = 500; // Actualizar cada 500ms
                thinkingTimer.Tick += (s, e) =>
                {
                    SafeInvoke(() =>
                    {
                        dotCount = (dotCount + 1) % thinkingFrames.Length;
                        
                        // Reemplazar el mensaje de "pensando" con la siguiente animación
                        if (thinkingMessageStart >= 0)
                        {
                            int endPos = txtChatHistory.Text.IndexOf("\n\n", thinkingMessageStart);
                            if (endPos > thinkingMessageStart)
                            {
                                txtChatHistory.Select(thinkingMessageStart, endPos - thinkingMessageStart);
                                txtChatHistory.SelectedText = "IA: " + thinkingFrames[dotCount];
                                txtChatHistory.SelectionStart = txtChatHistory.Text.Length;
                                txtChatHistory.ScrollToCaret();
                            }
                        }
                    });
                };
                thinkingTimer.Start();
                
                try
                {
                    var systemPrompt = "Eres un asistente para una aplicación P2P de descarga de libros. " +
                                     "Ayuda al usuario con búsquedas, descargas y gestión de biblioteca. " +
                                     "Sé conciso y útil.";
                    
                    var response = await ollamaClient.GetCompletionAsync(userMessage, systemPrompt);
                    
                    // Detener animación
                    thinkingTimer.Stop();
                    thinkingTimer.Dispose();
                    
                    if (!string.IsNullOrEmpty(response))
                    {
                        // Eliminar todos los mensajes de "pensando"
                        foreach (var frame in thinkingFrames)
                        {
                            txtChatHistory.Text = txtChatHistory.Text.Replace("IA: " + frame + "\n\n", "");
                        }
                        
                        AppendChatMessage("IA", response, Color.FromArgb(150, 255, 150));
                    }
                    else
                    {
                        // Eliminar todos los mensajes de "pensando"
                        foreach (var frame in thinkingFrames)
                        {
                            txtChatHistory.Text = txtChatHistory.Text.Replace("IA: " + frame + "\n\n", "");
                        }
                        
                        AppendChatMessage("SISTEMA", "No se pudo obtener respuesta de la IA.", Color.Orange);
                    }
                }
                catch
                {
                    // Detener animación en caso de error
                    thinkingTimer.Stop();
                    thinkingTimer.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Limpiar mensajes de "pensando" en caso de error
                txtChatHistory.Text = txtChatHistory.Text.Replace("IA: 🤔 Pensando\n\n", "")
                                                         .Replace("IA: 🤔 Pensando.\n\n", "")
                                                         .Replace("IA: 🤔 Pensando..\n\n", "")
                                                         .Replace("IA: 🤔 Pensando...\n\n", "");
                
                AppendChatMessage("ERROR", $"Error con Ollama: {ex.Message}", Color.FromArgb(255, 100, 100));
            }
        }

        private void ShowHelp()
        {
            var help = new StringBuilder();
            help.AppendLine("🤖 AYUDA DEL CHAT IA\n");
            help.AppendLine("COMANDOS DISPONIBLES:");
            help.AppendLine("• busca [autor] - Buscar libros de un autor");
            help.AppendLine("• estado - Ver estado de descargas");
            help.AppendLine("• estadísticas - Ver estadísticas del sistema");
            help.AppendLine("• ayuda - Mostrar esta ayuda\n");
            help.AppendLine("PREGUNTAS EN LENGUAJE NATURAL:");
            help.AppendLine("También puedes hacer preguntas como:");
            help.AppendLine("• '¿Cuántos libros he descargado?'");
            help.AppendLine("• 'Recomiéndame autores de ciencia ficción'");
            help.AppendLine("• '¿Cómo busco libros en español?'\n");
            help.AppendLine("ATAJOS DE TECLADO:");
            help.AppendLine("• Enter - Enviar mensaje");
            help.AppendLine("• Shift+Enter - Nueva línea");
            help.AppendLine("• ↑/↓ - Navegar historial");
            
            AppendChatMessage("SISTEMA", help.ToString(), Color.FromArgb(100, 200, 255));
        }

        private void ShowStatus()
        {
            int total = 0;
            
            try
            {
                lock (downloadQueueLock)
                {
                    total = downloadQueue?.Count ?? 0;
                }
            }
            catch { }
            
            var status = new StringBuilder();
            status.AppendLine("📊 ESTADO DEL SISTEMA\n");
            status.AppendLine($"Total descargas: {total}");
            status.AppendLine($"Sesión activa: {sessionId}");
            status.AppendLine($"\nUsa la pestaña 'Descargas' para ver detalles completos");
            
            AppendChatMessage("SISTEMA", status.ToString(), Color.FromArgb(100, 200, 255));
        }

        private void ShowStatistics()
        {
            int total = 0;
            
            try
            {
                lock (downloadQueueLock)
                {
                    total = downloadQueue?.Count ?? 0;
                }
            }
            catch { }
            
            var stats = new StringBuilder();
            stats.AppendLine("📊 ESTADÍSTICAS\n");
            stats.AppendLine($"Total archivos en cola: {total}");
            stats.AppendLine($"📅 Sesión: {sessionId}");
            stats.AppendLine($"\nBúsquedas totales: {totalSearches}");
            stats.AppendLine($"Descargas totales: {totalDownloads}");
            
            AppendChatMessage("SISTEMA", stats.ToString(), Color.FromArgb(100, 200, 255));
        }

        private async Task PerformSearch(string query)
        {
            AppendChatMessage("SISTEMA", $"🔍 Buscando: {query}...", Color.FromArgb(100, 200, 255));
            
            // Simular búsqueda (aquí conectarías con tu lógica real de búsqueda)
            await Task.Delay(500);
            
            AppendChatMessage("SISTEMA", 
                $"Búsqueda iniciada para: {query}\n" +
                "Ve a la pestaña 'Búsqueda' para ver los resultados.", 
                Color.FromArgb(100, 200, 255));
        }

        private void AppendChatMessage(string sender, string message, Color color)
        {
            if (txtChatHistory == null) return;
            
            SafeInvoke(() =>
            {
                txtChatHistory.SelectionStart = txtChatHistory.TextLength;
                txtChatHistory.SelectionLength = 0;
                
                txtChatHistory.SelectionColor = Color.White;
                txtChatHistory.SelectionFont = new Font(txtChatHistory.Font, FontStyle.Bold);
                txtChatHistory.AppendText($"{sender}: ");
                
                txtChatHistory.SelectionColor = color;
                txtChatHistory.SelectionFont = new Font(txtChatHistory.Font, FontStyle.Regular);
                txtChatHistory.AppendText($"{message}\n\n");
                
                txtChatHistory.ScrollToCaret();
            });
        }

        private void ClearChatHistory()
        {
            if (txtChatHistory != null)
            {
                txtChatHistory.Clear();
                AppendChatMessage("SISTEMA", "Historial limpiado. Escribe 'ayuda' para ver comandos disponibles.", Color.FromArgb(100, 200, 255));
            }
        }
    }
}
