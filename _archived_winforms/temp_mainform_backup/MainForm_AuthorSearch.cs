using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using Soulseek;
using System.IO;

namespace SlskDown
{
    public class MainFormAuthorSearch : Form
    {
        private TabControl tabControl;
        private ListBox authorsListBox;
        private Button startAuthorSearchButton;
        private RichTextBox logTextBox;
        private bool isAuthorSearchRunning = false;
        private SoulseekClient? client;
        private string downloadDir = @"c:\p2p\downloads";
        
        public MainFormAuthorSearch()
        {
            Console.WriteLine("[MainFormAuthorSearch] ðŸ—ï¸ INICIADO");
            
            // Configurar formulario
            this.Size = new Size(1100, 700);
            this.Text = "SlskDown - BÃºsqueda por Autor";
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Crear TabControl
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            // PestaÃ±a de BÃºsqueda por Autor
            var authorTab = new TabPage("ðŸ“š BÃºsqueda por Autor")
            {
                BackColor = Color.FromArgb(30, 30, 30)
            };
            
            // Panel de control
            var controlPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 200,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            
            // Lista de autores
            var authorsLabel = new Label
            {
                Text = "Lista de Autores:",
                Location = new Point(20, 20),
                Size = new Size(150, 25),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            
            authorsListBox = new ListBox
            {
                Location = new Point(20, 50),
                Size = new Size(400, 120),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Consolas", 9),
                SelectionMode = SelectionMode.MultiExtended
            };
            
            // BotÃ³n de bÃºsqueda
            startAuthorSearchButton = new Button
            {
                Text = "ðŸš€ Iniciar BÃºsqueda Ultra-RÃ¡pida",
                Location = new Point(440, 50),
                Size = new Size(250, 40),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            startAuthorSearchButton.Click += StartAuthorSearch_Click;
            
            // Agregar algunos autores de ejemplo
            authorsListBox.Items.AddRange(new string[] {
                "Stephen King",
                "J.K. Rowling",
                "George R.R. Martin",
                "Isaac Asimov",
                "Philip K. Dick"
            });
            
            controlPanel.Controls.AddRange(new Control[] {
                authorsLabel, authorsListBox, startAuthorSearchButton
            });
            
            // Panel de log
            var logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 25)
            };
            
            var logLabel = new Label
            {
                Text = "ðŸ“‹ Log de BÃºsqueda:",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 5, 0, 0)
            };
            
            logTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            logPanel.Controls.AddRange(new Control[] { logLabel, logTextBox });
            
            authorTab.Controls.AddRange(new Control[] { controlPanel, logPanel });
            
            // PestaÃ±a de resultados (placeholder)
            var resultsTab = new TabPage("ðŸ“Š Resultados")
            {
                BackColor = Color.FromArgb(30, 30, 30)
            };
            resultsTab.Controls.Add(new Label 
            { 
                Text = "Resultados de bÃºsqueda aparecerÃ¡n aquÃ­", 
                ForeColor = Color.White,
                Font = new Font("Arial", 16),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            
            // Agregar pestaÃ±as
            tabControl.TabPages.Add(authorTab);
            tabControl.TabPages.Add(resultsTab);
            
            // Agregar TabControl al formulario
            this.Controls.Add(tabControl);
            
            // Mensaje inicial en el log
            AddLogMessage("ðŸ“‹ Sistema de Log Iniciado", Color.Lime);
            AddLogMessage("â³ Esperando conexiÃ³n a Soulseek...", Color.Yellow);
            
            // Inicializar cliente Soulseek
            Task.Run(async () => await InitializeSoulseekClient());
            
            Console.WriteLine("[MainFormAuthorSearch] âœ… COMPLETADO");
        }
        
        private async Task InitializeSoulseekClient()
        {
            try
            {
                AddLogMessage("ðŸ”Œ Conectando a Soulseek...", Color.Yellow);
                
                client = new SoulseekClient();
                await client.ConnectAsync("carbar", "Carlos66*");
                
                AddLogMessage("âœ… Conectado a Soulseek", Color.Lime);
                AddLogMessage($"ðŸ“‚ Carpeta de descargas: {downloadDir}", Color.White);
            }
            catch (Exception ex)
            {
                AddLogMessage($"âŒ Error conectando: {ex.Message}", Color.Red);
            }
        }
        
        private async void StartAuthorSearch_Click(object? sender, EventArgs e)
        {
            if (isAuthorSearchRunning) return;
            
            var selectedAuthors = authorsListBox.SelectedItems.Cast<string>().ToList();
            if (selectedAuthors.Count == 0)
            {
                MessageBox.Show("Por favor selecciona al menos un autor.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            isAuthorSearchRunning = true;
            startAuthorSearchButton.Text = "â¸ï¸ Buscando...";
            startAuthorSearchButton.BackColor = Color.FromArgb(255, 140, 0);
            
            AddLogMessage("ðŸš€ MODO ULTRA-RÃPIDO ACTIVADO", Color.Cyan);
            AddLogMessage($"ðŸ“š Autores seleccionados: {selectedAuthors.Count}", Color.White);
            AddLogMessage("âš¡ Iniciando bÃºsqueda optimizada...", Color.Yellow);
            
            // BÃºsqueda real con Soulseek
            await Task.Run(async () =>
            {
                if (client == null)
                {
                    this.Invoke(new Action(() => AddLogMessage("âŒ No conectado a Soulseek", Color.Red)));
                    return;
                }
                
                int totalFiles = 0;
                
                foreach (var author in selectedAuthors)
                {
                    this.Invoke(new Action(() => AddLogMessage($"ðŸ” Buscando: {author}...", Color.LightBlue)));
                    
                    try
                    {
                        var results = new List<SearchResponse>();
                        var search = await client.SearchAsync(
                            SearchQuery.FromText(author),
                            scope: SearchScope.Network,
                            options: new SearchOptions(
                                searchTimeout: 30000,
                                responseLimit: 50,
                                fileLimit: 100
                            ),
                            cancellationToken: CancellationToken.None
                        );
                        
                        // Esperar un breve tiempo para acumular respuestas adicionales si llegan
                        await Task.Delay(500);
                        
                        foreach (var response in search.Responses)
                        {
                            results.Add(response);
                        }
                        
                        var fileCount = results.Sum(r => r.Files.Count);
                        totalFiles += fileCount;
                        
                        this.Invoke(new Action(() => 
                        {
                            AddLogMessage($"âœ… {author} - {fileCount} archivos encontrados", Color.LightGreen);
                            
                            // Mostrar algunos archivos encontrados (filtrados)
                            var bookExtensions = new[] { ".pdf", ".epub", ".mobi", ".azw", ".azw3", ".djvu", ".fb2", ".lit", ".pdb" };
                            int shown = 0;
                            
                            foreach (var response in results.Take(3))
                            {
                                var books = response.Files.Where(f => 
                                {
                                    var ext = Path.GetExtension(f.Filename).ToLowerInvariant();
                                    return bookExtensions.Contains(ext);
                                }).Take(2);
                                
                                foreach (var file in books)
                                {
                                    AddLogMessage($"   ðŸ“„ {Path.GetFileName(file.Filename)} ({FormatFileSize(file.Size)})", Color.Gray);
                                    shown++;
                                }
                                
                                if (shown >= 5) break;
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(new Action(() => AddLogMessage($"âŒ Error en {author}: {ex.Message}", Color.Red)));
                    }
                }
                
                this.Invoke(new Action(() => AddLogMessage($"ðŸ† BÃºsqueda completada - Total: {totalFiles} archivos", Color.Lime)));
            });
            
            isAuthorSearchRunning = false;
            startAuthorSearchButton.Text = "ðŸš€ Iniciar BÃºsqueda Ultra-RÃ¡pida";
            startAuthorSearchButton.BackColor = Color.FromArgb(0, 120, 215);
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        private void AddLogMessage(string message, Color color)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => AddLogMessage(message, color)));
                return;
            }
            
            logTextBox.SelectionColor = color;
            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            logTextBox.SelectionStart = logTextBox.TextLength;
            logTextBox.ScrollToCaret();
        }
    }
}

