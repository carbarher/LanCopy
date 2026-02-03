using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown.UI
{
    /// <summary>
    /// Ventana de chat interactivo con IA (Ollama)
    /// </summary>
    public class ChatWindow : Form
    {
        private readonly string ollamaUrl;
        private readonly string ollamaModel;
        private readonly HttpClient httpClient;
        private readonly Action<AICommandParser.ParsedCommand> executeCommandCallback;
        
        private RichTextBox txtChatHistory;
        private TextBox txtInput;
        private Button btnSend;
        private Button btnClear;
        private Label lblStatus;
        private Panel inputPanel;
        
        private bool isProcessing = false;

        public ChatWindow(string ollamaUrl, string ollamaModel, Action<AICommandParser.ParsedCommand> executeCommand = null)
        {
            this.ollamaUrl = ollamaUrl;
            this.ollamaModel = ollamaModel;
            this.httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            this.executeCommandCallback = executeCommand;
            
            InitializeUI();
            AddWelcomeMessage();
        }

        private void InitializeUI()
        {
            // Configuración de la ventana
            this.Text = "Chat con IA - Ollama";
            this.Size = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(500, 400);

            // Panel principal
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(10)
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Chat history
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));  // Status
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));  // Input

            // Historial de chat
            txtChatHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10)
            };

            // Label de estado
            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = $"Modelo: {ollamaModel} | Servidor: {ollamaUrl}",
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };

            // Panel de entrada
            inputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(5)
            };

            // TextBox de entrada
            txtInput = new TextBox
            {
                Multiline = true,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                Height = 60
            };
            txtInput.KeyDown += TxtInput_KeyDown;

            // Panel de botones
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(0, 5, 0, 0)
            };

            // Botón Enviar
            btnSend = new Button
            {
                Text = "Enviar",
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 30),
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.Click += async (s, e) => await SendMessage();

            // Botón Limpiar
            btnClear = new Button
            {
                Text = "Limpiar",
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 30),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 5, 0)
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) => ClearChat();

            buttonPanel.Controls.Add(btnSend);
            buttonPanel.Controls.Add(btnClear);

            inputPanel.Controls.Add(txtInput);
            inputPanel.Controls.Add(buttonPanel);

            // Agregar controles al panel principal
            mainPanel.Controls.Add(txtChatHistory, 0, 0);
            mainPanel.Controls.Add(lblStatus, 0, 1);
            mainPanel.Controls.Add(inputPanel, 0, 2);

            this.Controls.Add(mainPanel);
        }

        private void AddWelcomeMessage()
        {
            AppendMessage("SISTEMA", 
                "¡Hola! Soy tu asistente de IA para SlskDown.\n\n" +
                "Puedes preguntarme sobre:\n" +
                "• Recomendaciones de autores y libros\n" +
                "• Información sobre géneros literarios\n" +
                "• Sugerencias de búsqueda\n" +
                "• Cualquier duda sobre literatura\n\n" +
                "COMANDOS ACCIONABLES:\n" +
                "• \"bájate todas las obras de [autor] en español\"\n" +
                "• \"busca libros de [autor]\"\n" +
                "• \"descarga [autor] en formato epub\"\n\n" +
                "¿En qué puedo ayudarte?",
                Color.FromArgb(100, 200, 255));
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            // Enviar con Ctrl+Enter
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                _ = SendMessage();
            }
        }

        private async Task SendMessage()
        {
            if (isProcessing) return;

            string userMessage = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(userMessage)) return;

            try
            {
                isProcessing = true;
                btnSend.Enabled = false;
                txtInput.Enabled = false;
                lblStatus.Text = "Pensando...";

                // Mostrar mensaje del usuario
                AppendMessage("TÚ", userMessage, Color.FromArgb(50, 255, 50));
                txtInput.Clear();

                // Detectar si es un comando accionable
                var command = AICommandParser.Parse(userMessage);
                
                if (command.Type != AICommandParser.CommandType.None)
                {
                    // Es un comando - pedir confirmación
                    var confirmMsg = AICommandParser.GenerateConfirmationMessage(command);
                    AppendMessage("SISTEMA", 
                        $"He detectado un comando:\n\n{confirmMsg}\n\n¿Quieres que lo ejecute?",
                        Color.FromArgb(255, 200, 100));
                    
                    var result = MessageBox.Show(
                        $"Comando detectado:\n\n{confirmMsg}\n\n¿Ejecutar?",
                        "Confirmar Comando",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );
                    
                    if (result == DialogResult.Yes)
                    {
                        // Ejecutar comando
                        if (executeCommandCallback != null)
                        {
                            AppendMessage("SISTEMA", "⏳ Ejecutando comando...", Color.FromArgb(100, 200, 255));
                            executeCommandCallback(command);
                            AppendMessage("SISTEMA", "✅ Comando ejecutado. Revisa la pestaña de Búsqueda o Descargas.", Color.FromArgb(50, 255, 50));
                        }
                        else
                        {
                            AppendMessage("ERROR", "No se pudo ejecutar el comando (callback no configurado)", Color.FromArgb(255, 100, 100));
                        }
                    }
                    else
                    {
                        AppendMessage("SISTEMA", "Comando cancelado.", Color.FromArgb(150, 150, 150));
                    }
                    
                    lblStatus.Text = $"Modelo: {ollamaModel} | Listo";
                }
                else
                {
                    // No es comando - enviar a Ollama para respuesta normal
                    string response = await GetOllamaResponse(userMessage);
                    AppendMessage("IA", response, Color.FromArgb(100, 200, 255));
                    lblStatus.Text = $"Modelo: {ollamaModel} | Listo";
                }
            }
            catch (Exception ex)
            {
                AppendMessage("ERROR", 
                    $"No se pudo obtener respuesta: {ex.Message}\n\n" +
                    "Verifica que Ollama esté corriendo:\n" +
                    "• Ejecuta: ollama serve\n" +
                    "• Verifica: http://localhost:11434",
                    Color.FromArgb(255, 100, 100));
                lblStatus.Text = "Error de conexión";
            }
            finally
            {
                isProcessing = false;
                btnSend.Enabled = true;
                txtInput.Enabled = true;
                txtInput.Focus();
            }
        }

        private async Task<string> GetOllamaResponse(string prompt)
        {
            var requestBody = new
            {
                model = ollamaModel,
                prompt = prompt,
                stream = false,
                options = new
                {
                    temperature = 0.7,
                    top_p = 0.9
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{ollamaUrl}/api/generate", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (responseObj.TryGetProperty("response", out JsonElement responseText))
            {
                return responseText.GetString() ?? "Sin respuesta";
            }

            return "Error: No se pudo obtener respuesta";
        }

        private void AppendMessage(string sender, string message, Color color)
        {
            if (txtChatHistory.InvokeRequired)
            {
                txtChatHistory.Invoke(new Action(() => AppendMessage(sender, message, color)));
                return;
            }

            // Agregar timestamp
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            
            txtChatHistory.SelectionStart = txtChatHistory.TextLength;
            txtChatHistory.SelectionLength = 0;

            // Timestamp
            txtChatHistory.SelectionColor = Color.FromArgb(150, 150, 150);
            txtChatHistory.SelectionFont = new Font(txtChatHistory.Font, FontStyle.Regular);
            txtChatHistory.AppendText($"[{timestamp}] ");

            // Sender
            txtChatHistory.SelectionColor = color;
            txtChatHistory.SelectionFont = new Font(txtChatHistory.Font, FontStyle.Bold);
            txtChatHistory.AppendText($"{sender}:\n");

            // Message
            txtChatHistory.SelectionColor = Color.White;
            txtChatHistory.SelectionFont = new Font(txtChatHistory.Font, FontStyle.Regular);
            txtChatHistory.AppendText($"{message}\n\n");

            // Scroll al final
            txtChatHistory.SelectionStart = txtChatHistory.TextLength;
            txtChatHistory.ScrollToCaret();
        }

        private void ClearChat()
        {
            var result = MessageBox.Show(
                "¿Limpiar todo el historial de chat?",
                "Confirmar",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                txtChatHistory.Clear();
                AddWelcomeMessage();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                httpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
